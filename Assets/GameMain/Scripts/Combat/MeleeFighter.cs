using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

//枚举 攻击的四个阶段
public enum E_AttackState
{
    idle,
    Windup,//前摇
    Impact,//生效
    Cooldown,//后摇
}

/// <summary>
/// 近战攻击方 + 动画机。"被打"整体交给 Health 组件。
/// 伤害只通过 DamageRouter → Health；受击表现订阅 GameEvents.OnUnitDamaged。
/// </summary>
public class MeleeFighter : MonoBehaviour, IAttackSource, IParryTarget
{
    [SerializeField] private int weaponID = -1;
    private int upgradeLevel = 1;
    [SerializeField] List<AttackData> attacks;

    WeaponConfig currentWeapConfig;

    [SerializeField] private Health health;

    [Header("攻击设置")]
    [Tooltip("命中时暂停时间（秒）")]
    public float hitStopDuration = 0.1f;

    [Tooltip("命中的时间缩放,0 = 完全暂停,0.1=慢动作")]
    [Range(0f,1f)]
    public float hitStopTimeScale = 0f;

    private GameObject currentWeapon;

    SphereCollider leftHandeConllider, rightHandeConllider, leftFootConllider, rightFootConllider;

    [Header("预置命中盒（敌人等没走 WeaponManager 的单位）")]
    [Tooltip("预置命中盒。留空 = 运行时自动查找（骨骼名 → Hitbox 标签子物体）")]
    [SerializeField] private BoxCollider preplacedHitbox;

    [Tooltip("【默认关闭】按武器网格顶点自动测量命中盒尺寸。\n" +
             "⚠️ 目前是**未完成**的实验特性：网格顶点到底活在哪个坐标系里还没查清，\n" +
             "实测会选错网格、把盒放大到角色体外。开启前请先读\n" +
             "Moveset_System_Design.md §10 阶段 0.1 里记录的 bindpose 陷阱")]
    [SerializeField] private bool autoFitWeaponHitbox = false;

    [Tooltip("自动测得的盒长边超过该值即判定测量失败（米）")]
    [SerializeField] private float autoFitMaxLength = 3f;

    [Tooltip("自动测得的长边超过手工尺寸长边的这个倍数就拒绝（0 = 不检查）。\n" +
             "防的是\"自动定尺选错网格、把盒放大到角色体外\"这类反向优化")]
    [SerializeField] private float autoFitMaxOversizeRatio = 1.3f;

    [Tooltip("每次绑定命中盒时输出日志（排查\"敌人砍不掉血\"时打开）")]
    [SerializeField] private bool logHitboxBinding = false;

    public E_AttackState AttackState { get; private set; }

    BoxCollider WeaponCollider;
    Animator animator;
    RuntimeAnimatorController originalController;
    public bool IsAttackingHit { get; private set; } = false; //是否处于被攻击状态
    public bool inAction { get; private set; } = false;//是否处于攻击中
    public bool inCounter { get; set; } = false;//反击演出阶段
    bool doCombo;//连击标志
    int combocount = 0;//连技计数

    MeleeFighter currentTarget;

    /// <summary>兼容旧读取方：内部无存储，转发到 Health</summary>
    public float Health => health != null ? health.CurrentHealth : 0f;
    public float MaxHealth => health != null ? health.MaxHealth : 0f;

    /// <summary>血量组件转发，外部零成本取 Health</summary>
    public Health HealthComponent => health;

    public MeleeFighter CurrentTarget => currentTarget;

    // IAttackSource
    public AttackData CurrentAttack
    {
        get
        {
            if (AttackState != E_AttackState.Impact) return null;
            var list = ActiveAttacks;
            if (list == null || list.Count == 0) return null;
            int idx = Mathf.Clamp(combocount, 0, list.Count - 1);
            return list[idx];
        }
    }

    public int WeaponID => weaponID;
    public int UpgradeLevel => upgradeLevel;

    // IParryTarget
    public bool IsInParryWindow
    {
        get
        {
            if (AttackState != E_AttackState.Windup) return false;
            var list = ActiveAttacks;
            if (list == null || list.Count == 0) return true;
            int idx = Mathf.Clamp(combocount, 0, list.Count - 1);
            return list[idx] == null || list[idx].Parryable;
        }
    }

    public void OnParried(GameObject parrier)
    {
        StopAllCoroutines();
        AttackState = E_AttackState.idle;
        inAction = false;
        DisableAllHitxboxes();
        // P3：进入硬直状态
    }

    /// <summary>
    /// 翻滚打断攻击（魂类的翻滚取消）。清理：攻击协程、战斗层动画、命中盒、连击状态。
    /// 调用前提：不在受击硬直、不在处决演出（由 PlayerDodge.TryDodge 把关）。
    /// </summary>
    public void CancelActionByDodge()
    {
        if (!inAction || inCounter) return;

        StopAllCoroutines();    // 停掉 Attack 协程（此时不可能在受击/处决，入口已拦）
        AttackState = E_AttackState.idle;
        inAction = false;
        doCombo = false;
        combocount = 0;
        currentTarget = null;
        DisableAllHitxboxes();

        // 战斗层（Override Layer, index 1）淡回 Empty：
        // 攻击动画在层 1 以权重 1 覆盖基础层；只停协程不回 Empty 的话，
        // 攻击姿势还盖在翻滚上（"人在滚、上半身还在挥剑"）
        animator.CrossFade("Empty", 0.1f, 1);
    }

    List<AttackData> ActiveAttacks =>
        (currentWeapConfig != null && currentWeapConfig.attacks != null && currentWeapConfig.attacks.Count > 0)
            ? currentWeapConfig.attacks
            : attacks;

    void Awake()
    {
        animator = GetComponent<Animator>();
        originalController = animator.runtimeAnimatorController;
        if (health == null) health = GetComponent<Health>();
        InitBoneCollider();
    }

    private void OnEnable()
    {
        GameEvents.OnUnitDamaged += HandleUnitDamaged;
        GameEvents.OnPlayerDeath += HandlePlayerDeath;
        GameEvents.OnEnemyKilled += HandleEnemyKilled;
    }

    private void OnDisable()
    {
        GameEvents.OnUnitDamaged -= HandleUnitDamaged;
        GameEvents.OnPlayerDeath -= HandlePlayerDeath;
        GameEvents.OnEnemyKilled -= HandleEnemyKilled;
    }

    void Start()
    {
        // 敌人等未走 SetWeapon 的单位：自动接上预置命中盒。
        // 放在协程里（末尾 yield return null）：命中盒挂在骨骼上，
        // 必须等 Animator 至少求值过一次，自动测量才量得到正确的骨骼姿态
        StartCoroutine(BindPreplacedWeaponAtEndOfFrame());
        DisableAllHitxboxes();
    }

    IEnumerator BindPreplacedWeaponAtEndOfFrame()
    {
        yield return null;

        if (currentWeapon != null) yield break;
        TryBindPreplacedWeapon();
    }

    /// <summary>
    /// 绑定预置于层级中的武器命中盒。
    ///
    /// 原实现只认名字【完全等于 "Sword"】且自带 BoxCollider 的子物体，而
    /// Enemy.prefab 里的武器叫 Paladin_J_Nordstrom_Sword（SkinnedMeshRenderer，无碰撞体），
    /// 真正能挂命中盒的骨骼叫 mixamorig:Sword_joint
    /// → WeaponCollider 恒为 null，敌人攻击永远不掉血。
    ///
    /// 现按三级策略查找，任一命中即返回：
    ///   1. Inspector 上手动指定的 preplacedHitbox（最明确，优先）
    ///   2. 已知骨骼名精确匹配（Sword_joint 等）：这类物体上常刻意为省事不挂 Tag
    ///   3. 兜底：任意带 "Hitbox" 标签的子物体（与玩家武器 Prefab 同一套约定）
    ///
    /// 全部找不到时只警告、不抛异常：缺命中盒的表现就是"打不掉血"，
    /// 没有日志的话这条链路完全不可观测（本次修的正是这类静默失败）。
    /// </summary>
    void TryBindPreplacedWeapon()
    {
        // ① Inspector 直接指定
        if (preplacedHitbox != null)
        {
            BindPreplacedHitbox(preplacedHitbox.gameObject, preplacedHitbox, "Inspector 指定");
            return;
        }

        var transforms = GetComponentsInChildren<Transform>(true);

        // ② 已知骨骼名。⚠️ 不要改回按 "Sword" 精确匹配：
        // Enemy.prefab 里叫 "Sword" 的是 SkinnedMeshRenderer 网格节点，
        // 给它加 BoxCollider 是错的（网格顶点由骨骼驱动，节点自身不动）
        string[] knownBoneNames = { "Sword_joint", "mixamorig:Sword_joint" };
        for (int i = 0; i < knownBoneNames.Length; i++)
        {
            foreach (var t in transforms)
            {
                if (t == null || t.name != knownBoneNames[i]) continue;
                var box = t.GetComponent<BoxCollider>();
                if (box == null) continue;
                BindPreplacedHitbox(t.gameObject, box, "骨骼名匹配");
                return;
            }
        }

        // ③ 兜底：Hitbox 标签
        foreach (var t in transforms)
        {
            if (t == null || !t.CompareTag("Hitbox")) continue;
            var box = t.GetComponent<BoxCollider>();
            if (box == null) continue;
            BindPreplacedHitbox(t.gameObject, box, "Hitbox 标签");
            return;
        }

        Debug.LogWarning(
            "[MeleeFighter] 没找到预置命中盒：本单位的攻击不会造成伤害。\n" +
            "修法：给武器骨骼（如 mixamorig:Sword_joint）挂一个 IsTrigger 的 BoxCollider，\n" +
            "并把该物体设到 Enemyhitbox(9) 层、打上 Hitbox 标签，或直接在 Inspector 上指定 preplacedHitbox。", this);
    }

    /// <summary>接管一个预置命中盒：存档引用、清残留刚体、按需自动定尺、先关掉等开窗</summary>
    void BindPreplacedHitbox(GameObject hitboxObject, BoxCollider box, string matchedBy)
    {
        currentWeapon = hitboxObject;
        WeaponCollider = box;

        // 命中盒常驻开启 → 走路时剑身刮到玩家就掉血（玩家侧 WeaponManager 有同样的注释）。
        // 这里统一关掉，由 EnableHitbox / DisableAllHitxboxes 精确开窗
        box.enabled = false;

        // 残留 Rigidbody 会让 Trigger 检测失效：命中盒变成独立刚体，
        // 往往打不到目标身上的 CharacterController（玩家侧 WeaponManager 处理的是同一件事）
        var rb = box.GetComponent<Rigidbody>();
        if (rb != null) Destroy(rb);

        TryAdoptWeaponMeshFit(box);

        if (logHitboxBinding)
        {
            Debug.Log(
                $"[MeleeFighter] {name} 命中盒已绑定：{hitboxObject.name}（{matchedBy}，" +
                $"尺寸 {box.size}，中心 {box.center}）", this);
        }
    }

    /// <summary>
    /// 【默认关闭 · 实验性】按武器网格顶点自动测量命中盒尺寸。
    ///
    /// 出发点：骨骼上的命中盒尺寸只能靠肉眼试，想用网格顶点把剑身量出来。
    /// 现状：**没做成**。网格顶点到底活在哪个坐标系里尚未查清 ——
    /// `bindposes[i].inverse` 并没有给出预期的骨骼局部空间
    /// （实测身体量出 1.22 米，而角色实际两米多），于是会选错网格、
    /// 把命中盒放大到角色体外（实测 (2.04, 2.07, 0.41)、中心离骨骼 2.88 米）。
    ///
    /// 结论与后续排查方向记在 `Moveset_System_Design.md` §10 阶段 0.1 的实施记录里。
    /// 当前**正确做法是手工填 preplacedHitbox 的 Size / Center**，并用
    /// OnDrawGizmosSelected 画的线框在 Scene 里目视校准。
    ///
    /// 保留这套代码而不是删掉，是因为候选扫描、防呆、距离判据这几块是对的，
    /// 一旦坐标系问题查清就能直接接上。
    /// </summary>
    void TryAdoptWeaponMeshFit(BoxCollider box)
    {
        if (!autoFitWeaponHitbox || box == null) return;

        BoneBoundsMetric metric;
        if (!TryMeasureWeaponMesh(box.transform, out Bounds local, out metric))
        {
            // 自动定尺是可选功能，但它失败的原因必须看得见：
            // 默认尺寸恰好"看起来合理"时，人不会怀疑它其实一直没生效
            if (logHitboxBinding)
            {
                Debug.LogWarning(
                    $"[MeleeFighter] {name} 命中盒自动定尺未生效，沿用预制体尺寸。\n原因：{metric.reason}", this);
            }
            return;
        }

        Vector3 size = local.size;
        float longest = Mathf.Max(size.x, Mathf.Max(size.y, size.z));
        if (!IsFinite(size) || longest <= 0f || longest > autoFitMaxLength)
        {
            Debug.LogWarning(
                $"[MeleeFighter] {name} 命中盒自动测量结果不合理（局部尺寸 {size}），" +
                "已放弃自动定尺，沿用预制体上的尺寸。可在 Inspector 手动微调 preplacedHitbox。", this);
            return;
        }

        // 反向保护：自动定尺是来**改进**命中盒的，不该把盒放大到角色体外。
        // 预制体上的手工尺寸是人工确认过的基准，超出它太多就说明这次测量选错了网格
        Vector3 authored = box.size;
        float authoredLongest = Mathf.Max(authored.x, Mathf.Max(authored.y, authored.z));
        if (autoFitMaxOversizeRatio > 0f && authoredLongest > 0f
            && longest > authoredLongest * autoFitMaxOversizeRatio)
        {
            Debug.LogWarning(
                $"[MeleeFighter] {name} 命中盒自动测得的长边 {longest:F2} 米，" +
                $"是预制体手工尺寸 {authoredLongest:F2} 米的 {longest / authoredLongest:F2} 倍，" +
                "判定为选错网格，沿用预制体尺寸。\n" +
                $"（{metric.reason}）", this);
            return;
        }

        box.size = size;
        box.center = local.center;

        if (logHitboxBinding)
        {
            // ⚠️ 不能写 {metric.meshLongest:F2}：UnityEngine.Vector3 没实现 IFormattable，
            // 但 float 实现了 —— 这里只是拼字符串，用 ToString("F2") 更稳
            Debug.Log(
                $"[MeleeFighter] {name} 命中盒已按武器网格定尺：{size}，中心 {local.center}" +
                $"（{metric.reason}，该网格自身长边 {metric.meshLongest.ToString("F2")}）", this);
        }
    }

    /// <summary>自动定尺的诊断信息。失败原因要能直接读出来，而不是靠猜</summary>
    struct BoneBoundsMetric
    {
        /// <summary>失败原因 / 成功时的测量路径</summary>
        public string reason;

        /// <summary>被选中网格自身的 AABB 长边（成功时有效，用于判断量出来的盒是否离谱）</summary>
        public float meshLongest;
    }

    /// <summary>
    /// 在骨骼所属的模型里挑出"蒙皮到该骨骼的那个网格"，并量出顶点在骨骼局部空间下的包围盒。
    ///
    /// ⚠️ 不能从骨骼往下找（bone.GetComponentInChildren&lt;SkinnedMeshRenderer&gt;）：
    /// Enemy.prefab 的层级是
    ///     Paladin WProp J Nordstrom
    ///     ├── mixamorig:Hips → … → mixamorig:Sword_joint   （骨骼，本身是空物体）
    ///     └── Paladin_J_Nordstrom_Sword                     （网格，骨骼的**兄弟节点**）
    /// 骨骼出现在网格的 m_Bones 里，只代表"网格蒙皮到它"，不代表网格是它的子物体。
    ///
    /// 所以改为**从模型根扫描全部候选网格**，逐个用 bindpose 的逆矩阵把该网格的顶点
    /// 搬进骨骼局部空间量一遍，再用防呆规则（点云不可能比网格自身轴对齐盒还大）
    /// 筛掉"没蒙皮到这个骨骼"的网格，最后取体积最小的那个。
    /// 但"体积最小"单独用会被骗：盾/头盔这类**刚性挂件**在骨骼空间里会塌成一个小点
    /// （它们虽然声明蒙皮到这个骨骼，实际不跟着动），体积比剑还小，位置却离骨骼好几米。
    /// 所以再加一条距离防呆：命中盒中心到骨骼原点的距离不该显著超过盒子自身尺寸 ——
    /// 剑是握在手上的，它的包围盒中心必然贴着骨骼。
    /// </summary>
    static bool TryMeasureWeaponMesh(Transform bone, out Bounds best, out BoneBoundsMetric metric)
    {
        best = default;
        metric = default;

        Transform modelRoot = FindModelRoot(bone);
        if (modelRoot == null)
        {
            metric.reason = "找不到模型根（骨骼上方没有 Animator 或 SkinnedMeshRenderer）";
            return false;
        }

        var renderers = modelRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        if (renderers == null || renderers.Length == 0)
        {
            metric.reason = $"{modelRoot.name} 下没有任何 SkinnedMeshRenderer";
            return false;
        }

        var report = new List<string>();
        int accepted = 0;
        float bestVolume = float.MaxValue;
        float bestMeshLongest = 0f;
        string bestName = null;

        for (int i = 0; i < renderers.Length; i++)
        {
            Mesh mesh = renderers[i].sharedMesh;
            string meshName = mesh != null ? mesh.name : renderers[i].name;
            if (mesh == null)
            {
                report.Add($"{meshName}: sharedMesh 为空");
                continue;
            }

            // 网格自身的 AABB 与顶点无关、永远可读，是"这个网格多大"的旁证，
            // 也是判断顶点测量结果是否跑偏的基准
            Vector3 meshSize = mesh.bounds.size;
            float meshLongest = Mathf.Max(meshSize.x, Mathf.Max(meshSize.y, meshSize.z));

            if (!mesh.isReadable)
            {
                report.Add($"{meshName}: 未勾选 Read/Write");
                continue;
            }

            if (!TryMeasureMeshInBoneSpace(renderers[i], bone, modelRoot, out Bounds local,
                    out float boneLongest, out string failReason))
            {
                report.Add($"{meshName}: {failReason}");
                continue;
            }

            // 距离防呆：盒中心离骨骼太远 = 这个网格不是握在手上的东西
            float centerDistance = local.center.magnitude;
            if (centerDistance > boneLongest * 2f)
            {
                report.Add(
                    $"{meshName}: 测得 {local.size}，但中心离骨骼 {centerDistance:F2} 米" +
                    "（远超盒自身尺寸，不是握持物）");
                continue;
            }

            accepted++;
            report.Add($"{meshName}: 测得 {local.size}，中心距骨骼 {centerDistance:F2} 米 ← 候选");

            float volume = local.size.x * local.size.y * local.size.z;
            if (volume < bestVolume)
            {
                bestVolume = volume;
                best = local;
                bestMeshLongest = meshLongest;
                bestName = meshName;
            }
        }

        string detail = string.Join("；", report);

        if (accepted == 0)
        {
            metric.reason = $"没有网格能作为 {bone.name} 的握持物（逐个候选：{detail}）";
            return false;
        }

        metric.meshLongest = bestMeshLongest;
        metric.reason = $"bindpose 路线，{accepted} 个候选里取体积最小者 {bestName}（全部候选：{detail}）";
        return true;
    }

    /// <summary>单个网格：顶点 → 骨骼局部空间的包围盒。失败原因写进 failReason</summary>
    static bool TryMeasureMeshInBoneSpace(SkinnedMeshRenderer smr, Transform bone, Transform modelRoot,
        out Bounds local, out float boneLongest, out string failReason)
    {
        local = default;
        boneLongest = 0f;
        failReason = null;

        Mesh mesh = smr.sharedMesh;
        if (mesh == null)
        {
            failReason = "sharedMesh 为空";
            return false;
        }

        Vector3[] vertices = mesh.vertices;
        if (vertices == null || vertices.Length < 3)
        {
            failReason = "顶点数不足";
            return false;
        }

        int boneIndex = IndexOfBone(smr, bone);

        Matrix4x4 meshToBone;
        if (boneIndex >= 0 && mesh.bindposes != null && boneIndex < mesh.bindposes.Length)
        {
            // 蒙皮公式就是 bone.localToWorldMatrix * bindpose * vertex，
            // 所以 bindpose 的逆矩阵正好把网格顶点搬到骨骼局部空间 —— 精确
            meshToBone = mesh.bindposes[boneIndex].inverse;
        }
        else if (bone.IsChildOf(modelRoot))
        {
            // 退化路线：网格顶点本来就写在模型根空间时（道具网格常见）同样正确。
            // 骨骼不在 SkinnedMeshRenderer.bones 里时才走这里
            var path = new List<string>();
            for (Transform t = bone; t != null && t != modelRoot; t = t.parent) path.Add(t.name);
            if (path.Count == 0)
            {
                failReason = "骨骼不在 bones 列表里，也拼不出到模型根的路径";
                return false;
            }
            path.Reverse();

            Transform boneInModelSpace = modelRoot.Find(string.Join("/", path));
            if (boneInModelSpace == null)
            {
                failReason = "骨骼不在 bones 列表里，且在模型根下找不到同名路径";
                return false;
            }
            meshToBone = boneInModelSpace.worldToLocalMatrix;
        }
        else
        {
            failReason = "骨骼既不在 bones 列表里，也不在模型根层级下";
            return false;
        }

        Vector3 min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
        Vector3 max = new Vector3(float.MinValue, float.MinValue, float.MinValue);
        for (int i = 0; i < vertices.Length; i++)
        {
            Vector3 v = meshToBone.MultiplyPoint3x4(vertices[i]);
            min = Vector3.Min(min, v);
            max = Vector3.Max(max, v);
        }

        local = new Bounds((min + max) * 0.5f, max - min);

        // 防呆：点云不可能比网格自身的轴对齐盒更大（旋转不改变点集直径）。
        // 超了说明这个网格根本没蒙皮到这个骨骼 —— 比如拿头盔的 bindpose 去搬头盔顶点，
        // 或者剑的 bindpose 在身体网格里指向的是"整具身体"。这类候选必须剔除，
        // 否则命中盒会被放大成一个人那么大（"隔着半个屏幕被打"）
        boneLongest = Mathf.Max(local.size.x, Mathf.Max(local.size.y, local.size.z));
        Vector3 meshSize = mesh.bounds.size;
        float meshLongest = Mathf.Max(meshSize.x, Mathf.Max(meshSize.y, meshSize.z));
        if (boneLongest > meshLongest * 2f)
        {
            failReason = $"测得 {local.size} 超过网格自身尺寸 {meshSize} 的两倍（未蒙皮到该骨骼）";
            return false;
        }

        return true;
    }

    /// <summary>沿父链找模型根：优先 Animator 所在节点（Avatar 的根），退化时找 SkinnedMeshRenderer 宿主</summary>
    static Transform FindModelRoot(Transform from)
    {
        Transform fallback = null;
        for (Transform t = from; t != null; t = t.parent)
        {
            if (t.GetComponent<Animator>() != null) return t;
            if (fallback == null && t.GetComponent<SkinnedMeshRenderer>() != null) fallback = t;
        }
        return fallback;
    }

    /// <summary>骨骼在 SkinnedMeshRenderer.bones 里的下标（找不到返回 -1）</summary>
    static int IndexOfBone(SkinnedMeshRenderer smr, Transform bone)
    {
        Transform[] bones = smr.bones;
        if (bones == null) return -1;

        for (int i = 0; i < bones.Length; i++)
        {
            if (bones[i] == bone) return i;
        }
        return -1;
    }

    static bool IsFinite(Vector3 v)
    {
        return !float.IsNaN(v.x) && !float.IsNaN(v.y) && !float.IsNaN(v.z)
            && !float.IsInfinity(v.x) && !float.IsInfinity(v.y) && !float.IsInfinity(v.z);
    }

    public void SetWeapon(GameObject newWeapon)
    {
        currentWeapon = newWeapon;
        if(newWeapon != null)
        {
            WeaponCollider = newWeapon.GetComponent<BoxCollider>();
            DisableAllHitxboxes();
        }
        else
        {
            currentWeapon = null;
            WeaponCollider = null;
        }
    }

    public void SetWeaponConfig(WeaponConfig config)
    {
        currentWeapConfig = config;

        // ⚠️ 已知语义偏差（本阶段 0.1 不动它，见 Moveset_System_Design.md §1.3 / §10 阶段 2.4）：
        // animOverride 是个【动画覆盖器】却被当成【整个控制器】直接赋值。
        // 改走 MovesetResolver + "参数快照 → 赋值 → 等一帧 → 回灌" 是阶段 2 的任务，
        // 现在动它会把跑步/翻滚中的动画打断，且没有替代链路
        if (config != null && config.animOverride != null)
        {
            animator.runtimeAnimatorController = config.animOverride;
        }
        else
        {
            animator.runtimeAnimatorController = originalController;
        }
    }

    private void InitBoneCollider()
    {
        if(animator == null)return;
        leftHandeConllider = animator.GetBoneTransform(HumanBodyBones.LeftHand)?.GetComponent<SphereCollider>();
        rightHandeConllider = animator.GetBoneTransform(HumanBodyBones.RightHand)?.GetComponent<SphereCollider>();
        leftFootConllider = animator.GetBoneTransform(HumanBodyBones.LeftFoot)?.GetComponent<SphereCollider>();
        rightFootConllider = animator.GetBoneTransform(HumanBodyBones.RightFoot)?.GetComponent<SphereCollider>();
    }

    public void ToTryAttack(MeleeFighter target = null)
    {
        // 演出中（处决受害者 / 正在处决的玩家）不发起攻击：
        // Attack 协程第一件事就是把招式 CrossFade 到 Override Layer(1)，
        // 会把受害动画顶掉 —— 表现就是"被处决的敌人站起来反击"。
        if (inCounter) return;

        if (!inAction)
        {
            StartCoroutine(Attack(target));
        }
        else if (AttackState == E_AttackState.Impact || AttackState == E_AttackState.Cooldown)
        {
            doCombo = true;
        }
    }

    IEnumerator Attack(MeleeFighter target = null)
    {
        inAction = true;

        currentTarget = target;
        AttackState = E_AttackState.Windup;

        var activeAttacks = ActiveAttacks;
        if (activeAttacks == null || activeAttacks.Count == 0)
        {
            inAction = false;
            yield break;
        }

        animator.CrossFade(activeAttacks[combocount].AnimName, 0.2f, 1);

        yield return null;

        var animState = animator.GetNextAnimatorStateInfo(1);

        float timer = 0f;
        float length = Mathf.Max(animState.length, 0.0001f);
        while (timer <= length)
        {
            if (IsAttackingHit)
            {
                break;
            }

            timer += Time.deltaTime;

            float normalizedTime = timer / length;

            if (AttackState == E_AttackState.Windup)
            {
                if (inCounter)
                    break;

                if (normalizedTime >= activeAttacks[combocount].ImpactStartTime)
                {
                    AttackState = E_AttackState.Impact;
                    EnableHitbox(activeAttacks[combocount]);
                }
            }
            else if (AttackState == E_AttackState.Impact)
            {
                if (normalizedTime >= activeAttacks[combocount].ImpactEndTime)
                {
                    AttackState = E_AttackState.Cooldown;
                    DisableAllHitxboxes();
                }
            }
            else if (AttackState == E_AttackState.Cooldown)
            {
                if (doCombo)
                {
                    doCombo = false;
                    combocount = (combocount + 1) % activeAttacks.Count;

                    StartCoroutine(Attack(target));
                    yield break;
                }
            }

            yield return null;
        }

        AttackState = E_AttackState.idle;
        DisableAllHitxboxes();
        combocount = 0;
        inAction = false;
        currentTarget = null;
    }

    /// <summary>
    /// 本回调在【受击方】身上触发，other 是【攻击方】的 hitbox 碰撞体。
    /// </summary>
    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Hitbox")) return;
        if (IsAttackingHit || inCounter) return;

        var attackerFighter = other.GetComponentInParent<MeleeFighter>();
        if (attackerFighter == null || attackerFighter == this) return;

        // 尸体 / 已禁用单位不得造成伤害：
        // 攻击方被 DeadState 禁用后自己的 OnTriggerEnter 不再回调，但它的命中盒碰撞体还在，
        // 伤害会从受击方这条回调里漏进来（"死人打死人"）。这里做攻击方存活校验兜住。
        if (!attackerFighter.enabled) return;
        if (attackerFighter.HealthComponent != null && attackerFighter.HealthComponent.IsDead) return;

        // 攻击方已锁定别的目标时不误伤
        if (attackerFighter.currentTarget != null && attackerFighter.currentTarget != this) return;

        // ⚠️ target 必须是"我自己"的 Health —— 绝不能把 other（攻击方 hitbox）当 target
        if (health == null) health = GetComponent<Health>();
        if (health == null) return;

        // 命中点取"我身体上离攻击方 hitbox 最近的点"
        var myCollider = GetComponent<Collider>();
        Vector3 hitPoint = myCollider != null
            ? myCollider.ClosestPoint(other.transform.position)
            : transform.position;

        var atk = attackerFighter.CurrentAttack;
        bool applied = DamageRouter.Apply(health, attackerFighter.gameObject, hitPoint,
            atk == null || atk.Parryable);
        if (!applied) return;

        // 攻击方驱动的反馈：卡肉参数取自招式配置
        if (HitStopManager.Instance != null)
        {
            if (atk != null) HitStopManager.Instance.Play(atk.HitStopDuration, atk.HitStopTimeScale);
            else HitStopManager.Instance.Play(hitStopDuration, hitStopTimeScale);
        }

        // 保持原有语义：【玩家被打】时震屏
        if (CompareTag("Player")) CameraManager.Instance?.ShakeScreen();
    }

    /// <summary>
    /// 受击表现（玩家与敌人共用）。替代被删除的 PlayerHitReaction 直调。
    /// 敌人的 FSM 状态切换由 EnemyController.HandleUnitDamaged 负责。
    /// </summary>
    private void HandleUnitDamaged(string victimUnitId, DamageInfo info)
    {
        if (health == null || victimUnitId != health.UnitId) return;
        if (health.IsDead) return;
        if (inCounter) return;
        StartCoroutine(HitReaction(info.Source != null ? info.Source.transform : null));
    }

    private IEnumerator HitReaction(Transform attacker)
    {
        inAction = true;
        IsAttackingHit = true;

        if (attacker != null)
        {
            var dispVec = attacker.position - transform.position;
            dispVec.y = 0f;
            if (dispVec != Vector3.zero) transform.rotation = Quaternion.LookRotation(dispVec);
        }

        animator.CrossFade("Melee_Impact", 0.2f, 1);
        yield return null;

        var animState = animator.GetNextAnimatorStateInfo(1);
        yield return new WaitForSeconds(animState.length * 0.60f);

        GameEvents.RaiseHitReactionComplete(health.UnitId);
        inAction = false;
        IsAttackingHit = false;
    }

    private void HandleEnemyKilled(string killedUnitId)
    {
        if (health == null || killedUnitId != health.UnitId) return;
        StopAllCoroutines();
        // 死亡/受击/攻击状态都在 Override Layer(1)
        animator.CrossFade("Melee_FallBackDeath", 0.2f, 1);
        DisableAllHitxboxes();
    }

    private void HandlePlayerDeath()
    {
        if (!CompareTag("Player")) return;
        StopAllCoroutines();
        animator.CrossFade("Melee_FallBackDeath", 0.2f, 1);
    }

    /// <summary>
    /// 反击/处决演出（双人配对动画：玩家 Melee_CounterAttack / 敌人 Melee_CounterVictim）。
    ///
    /// ⚠️ 原实现两个致命点（本次修复）：
    ///   1. 先给敌人 SetInvulnerable(Counter, true)，处决致死伤害却在末尾才结算，
    ///      而 Health.TakeDamage 的第一道闸就是 `if (IsInvulnerable) return;`
    ///      → 9999 伤害被静默丢弃：敌人不掉血、不进 DeadState。
    ///   2. 收尾只清除了玩家自己的 "counter" 无敌（health? 指向自己），
    ///      敌人身上那条 reason 永久残留 → 这只敌人之后再也不可能被打死。
    /// 三个可见症状都源于此：处决后站起来继续攻击 / 看着被打死的敌人过一会又起来 / 敌人永不消失
    /// （消失逻辑只写在 DeadState.Enter 的 Destroy 里，没进 Dead 就永远不会销毁）。
    ///
    /// 修复原则：
    ///   - 演出期间双方免伤（防止第三方打断配对动画），但【结算致死伤害前必须先解除受害者无敌】；
    ///   - 所有状态清理放 finally：协程被 StopAllCoroutines 打断时也不留残留无敌 / 残留 inCounter。
    /// </summary>
    public IEnumerator PerformCounterAttack(EnemyController opponet)
    {
        if (opponet == null || opponet.Fighter == null) yield break;

        var victimHealth = opponet.Fighter.HealthComponent;

        // 已死目标不再拉进演出：否则会把 Death 姿态切回 Melee_CounterVictim，看起来像复活
        if (victimHealth == null || victimHealth.IsDead) yield break;

        inAction = true;
        inCounter = true;
        opponet.Fighter.inCounter = true;

        // 锁死受害者的 FSM 与导航：处决是双人配对动画，受害者不该在这期间自己走位/出手，
        // 否则演出中它会顶着受害动画追玩家（"处决完先朝我跑一下"）
        opponet.SetPerformanceLock(true);

        // 演出期间双方免伤，避免演出中被二次命中
        health?.SetInvulnerable(InvulnReasons.Counter, true);
        victimHealth.SetInvulnerable(InvulnReasons.Counter, true);

        try
        {
            var disVec = opponet.transform.position - transform.position;
            disVec.y = 0;

            transform.rotation = Quaternion.LookRotation(disVec);
            opponet.transform.rotation = Quaternion.LookRotation(-disVec);

            var targetPos = opponet.transform.position - disVec.normalized * 2f;

            animator.CrossFade("Melee_CounterAttack", 0.2f, 1);
            opponet.Animator.CrossFade("Melee_CounterVictim", 0.2f, 1);

            yield return null;

            var animState = animator.GetNextAnimatorStateInfo(1);

            float timer = 0f;
            float length = Mathf.Max(animState.length, 0.0001f);
            while (timer <= length)
            {
                transform.position = Vector3.MoveTowards(transform.position, targetPos, 2 * Time.deltaTime);
                yield return null;
                timer += Time.deltaTime;
            }

            // 目标在演出期间被销毁（场景卸载、外部清理等）：不再结算，
            // 直接 yield break 走 finally 清理；否则下面会抛 MissingReferenceException
            if (opponet == null || victimHealth == null) yield break;

            // ① 结算前先解除受害者无敌：否则下面的致死伤害会被 Health.TakeDamage 直接 return 掉
            victimHealth.SetInvulnerable(InvulnReasons.Counter, false);

            // ② 致死只走伤害唯一路径，不再直接 ChangeState(Dead)
            var enemyCol = opponet.GetComponent<Collider>();
            if (enemyCol != null)
            {
                DamageRouter.ApplyAmount(
                    enemyCol, gameObject, opponet.transform.position,
                    9999f, E_DamageSource.Player, parryable: false, attackId: "counter_execute");
            }

            // ③ 兜底：若还有别的无敌来源挡下（霸体/剧情保护等），也必须进死亡终态，
            //    绝不允许"处决完敌人还站着、还能打你"
            if (!victimHealth.IsDead)
            {
                Debug.LogWarning($"[Counter] 处决伤害被拦截，强制进入死亡终态：{opponet.name}", opponet);
                opponet.ForceDeath();
            }
        }
        finally
        {
            // 正常结束 / 抛异常 / 协程被 Stop，都走这里：不留残留状态
            inCounter = false;
            inAction = false;
            if (opponet != null && opponet.Fighter != null) opponet.Fighter.inCounter = false;

            health?.SetInvulnerable(InvulnReasons.Counter, false);
            // 目标可能已被销毁：finally 里抛异常会盖掉原异常，这里必须判一次
            if (victimHealth != null) victimHealth.SetInvulnerable(InvulnReasons.Counter, false);

            // 解锁受害者的演出锁。放在最后：若它已经死了，NavAgent 早被 DeadState 禁用，
            // 这里只会复位标志位，不会把尸体重新拉回战斗
            if (opponet != null) opponet.SetPerformanceLock(false);
        }
    }

    /// <summary>
    /// 死亡清理：停协程、复位攻击状态、关闭全部命中盒、清掉连击与目标。
    ///
    /// ⚠️ DeadState 必须在 `enabled = false` 之前调用它：
    ///   组件一被禁用就触发 OnDisable 退订 OnEnemyKilled，而 Health.Die() 是在
    ///   RaiseUnitDamaged 之后才发 OnEnemyKilled 的 —— MeleeFighter.HandleEnemyKilled
    ///   里那句 DisableAllHitxboxes() 永远等不到，尸体会带着"攻击生效中(Impact)"的
    ///   命中盒躺在地上，被活人撞上还会结算一次伤害。
    /// </summary>
    public void ForceStopCombat()
    {
        StopAllCoroutines();

        AttackState = E_AttackState.idle;
        inAction = false;
        inCounter = false;
        doCombo = false;
        combocount = 0;
        currentTarget = null;

        DisableAllHitxboxes();
    }

    void EnableHitbox(AttackData attack)
    {
        // 每次开窗前重新取一次：装备/切换武器后可能已经换了一个碰撞体
        if (WeaponCollider == null && currentWeapon != null)
            WeaponCollider = currentWeapon.GetComponent<BoxCollider>();

        switch (attack.HitboxToUse)
        {
            case E_AttackHitbox.LeftHande:
                if(leftHandeConllider != null) leftHandeConllider.enabled = true;
                break;
            case E_AttackHitbox.RightHande:
                if(rightHandeConllider !=null) rightHandeConllider.enabled = true;
                break;
            case E_AttackHitbox.LeftFoot:
                if(leftFootConllider != null)leftFootConllider.enabled = true;
                break;
            case E_AttackHitbox.RightFoot:
                if(rightFootConllider != null) rightFootConllider.enabled = true;
                break;
            case E_AttackHitbox.Weapon:
                // ⚠️ 这一段以前会把右手 SphereCollider 也一起打开。手部判定是"拳/掌"用的，
                // 剑招开手部判定等于凭空多出一个贴身判定盒 —— 现在只开武器盒
                if (WeaponCollider != null) WeaponCollider.enabled = true;
                break;
            default:
                break;
        }
    }

    void DisableAllHitxboxes()
    {
        if (leftHandeConllider != null) leftHandeConllider.enabled = false;
        if (rightHandeConllider != null) rightHandeConllider.enabled = false;
        if (leftFootConllider != null) leftFootConllider.enabled = false;
        if (rightFootConllider != null) rightFootConllider.enabled = false;
        if (WeaponCollider != null) WeaponCollider.enabled = false;
    }

    public List<AttackData> Attacks => attacks;

    public bool IsCounterable => AttackState == E_AttackState.Windup && combocount == 0;

    public void SetUpgradeLevel(int level) => upgradeLevel = level;

    public void SetWeaponID(int id) => weaponID = id;

    /// <summary>
    /// 选中本物体时画出武器命中盒。
    ///
    /// 为什么值得为这一个盒子写 Gizmos：命中盒的尺寸与朝向只能靠肉眼确认，
    /// 而它平时是 disabled 的（只在判定窗口内开），Scene 里根本看不到 ——
    /// "敌人挥剑却打不到人"这类问题没有可视化就只能靠猜。
    /// 黄色 = 判定窗口已开，灰色 = 当前关闭（正常待机状态）。
    /// </summary>
    private void OnDrawGizmosSelected()
    {
        if (WeaponCollider == null) return;

        Gizmos.color = WeaponCollider.enabled
            ? new Color(1f, 0.85f, 0.1f, 0.9f)
            : new Color(0.6f, 0.6f, 0.6f, 0.5f);

        Gizmos.matrix = WeaponCollider.transform.localToWorldMatrix;
        Gizmos.DrawWireCube(WeaponCollider.center, WeaponCollider.size);
    }
}

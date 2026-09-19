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

    private void Start()
    {
        // 敌人等未走 SetWeapon 的单位：自动接上预置武器
        if (currentWeapon == null)
        {
            TryBindPreplacedWeapon();
        }

        if (currentWeapon != null && WeaponCollider == null)
        {
            WeaponCollider = currentWeapon.GetComponent<BoxCollider>();
        }
        DisableAllHitxboxes();
    }

    void TryBindPreplacedWeapon()
    {
        var transforms = GetComponentsInChildren<Transform>(true);
        foreach (var t in transforms)
        {
            if (t == null || t.name != "Sword") continue;
            var box = t.GetComponent<BoxCollider>();
            if (box == null) continue;
            currentWeapon = t.gameObject;
            WeaponCollider = box;
            var rb = t.GetComponent<Rigidbody>();
            if (rb != null) Destroy(rb);
            return;
        }
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
        if(config != null && config.animOverride != null)
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
                if (rightHandeConllider != null) rightHandeConllider.enabled = true;
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
}

using System.Collections;
using UnityEngine;

/// <summary>
/// 弹反 / 格挡。T1 起作为【受击方伤害拦截层】接进 DamageRouter（实现 IDamageInterceptor）。
///
/// 分工：
///   - 格挡（持续按住）：按 Parry.block_damage_reduction 减伤，不打断敌人、不给硬直；
///   - 弹反（按下后的 parry_window 内被可弹反的招式命中）：
///     打断敌人当前招式 + 敌人硬直 enemy_stagger_duration，玩家自身短暂无敌。
///
/// 【数值来源】Parry 表（Gameplay.xlsx），按角色根对象 CharacterConfigIdentity 的配置 ID 取行，
///   Awake 解析一次（导表后需重新 Play 才生效，运行中不查表）。
///   【缺配置时的行为】报错并整体不生效，绝不静默退回默认值 —— 与 PlayerStamina 一致，
///   避免"改了表却没生效"看起来像正常运行。
///
/// 【与 T3 的分界】格挡持续扣耐力在 Update 的 DrainBlockStamina 里做（T3 已接）；
///   弹反失败惩罚（parry_fail_cost）仍未接。
/// </summary>
public class ParrySystem : MonoBehaviour, IDamageInterceptor
{
    [Header("组件引用")]
    [Tooltip("耐力组件。格挡持续耗耐用；留空则 Awake 自动 GetComponent")]
    [SerializeField] private PlayerStamina stamina;

    [Tooltip("自身血量组件，弹反成功后的短暂无敌挂在这里；留空则 Awake 自动 GetComponent")]
    [SerializeField] private Health health;

    [Tooltip("动画机。举盾姿态要靠它来播；留空则 Awake 自动 GetComponent")]
    [SerializeField] private Animator animator;

    [Tooltip("自身的近战组件。用来判断\"此刻是不是在挥刀/挨打\"，避免举盾把攻击动画顶掉")]
    [SerializeField] private MeleeFighter meleeFighter;

    /// <summary>举盾姿态协程句柄（收盾时要停掉，避免切循环把收盾动作顶回来）</summary>
    private Coroutine m_blockPoseRoutine;

    [Header("举盾动画")]
    [Tooltip("举盾起手状态名（PlayerMove.controller 的 Override Layer 里已存在）")]
    [SerializeField] private string blockStartState = "Block_Start";

    [Tooltip("举盾循环状态名。留空 = 不切循环，停在起手姿态")]
    [SerializeField] private string blockLoopState = "Block_Loop";

    [Tooltip("起手动画播多久后切循环（秒）")]
    [SerializeField] private float blockStartToLoopDelay = 0.25f;

    /// <summary>战斗层（Override Layer）索引：攻击/受击/死亡/举盾都在这一层</summary>
    private const int CombatLayer = 1;

    /// <summary>本次格挡的按下时刻（真实时间）。弹反窗口从这一刻起算</summary>
    private float blockStartTime = float.MinValue;

    /// <summary>
    /// 弹反无敌截止时刻（真实时间）。⚠️ 不能用 Invoke / WaitForSeconds：
    /// 它们走缩放时间，卡肉(0.05) 会把 0.3s 拉成 6s，暂停时更是永远不清。
    /// </summary>
    private float parryInvulnUntilRealtime = float.MinValue;

    /// <summary>当前生效的 Parry 表行（可能为 null；缺配置时解析入口已报错）</summary>
    private Parry m_configRow;

    /// <summary>当前生效的 Parry 表行（调试 / T3 读取用）</summary>
    public Parry Config => m_configRow;

    /// <summary>是否正在格挡（按住状态）</summary>
    public bool IsBlocking { get; private set; }

    /// <summary>是否处于弹反窗口内（按下后的 parry_window 秒）</summary>
    public bool IsInParryWindow =>
        m_configRow != null && IsBlocking &&
        (Time.unscaledTime - blockStartTime) <= m_configRow.ParryWindow;

    /// <summary>弹反/格挡是否生效（缺 Parry 配置时整体失效）</summary>
    public bool IsAvailable => m_configRow != null;

    // ---------- T5：弹反成功后的处决窗口 ----------
    /// <summary>刚被弹反的敌人（处决目标）。窗口过期 / 被消费后清空</summary>
    private EnemyController m_riposteTarget;

    /// <summary>处决窗口截止时刻（真实时间）</summary>
    private float riposteDeadlineRealtime = float.MinValue;

    /// <summary>此刻是否有可处决目标（将来做处决提示 UI 时直接读它）</summary>
    public bool HasRiposteTarget => GetRiposteTarget() != null;

    private void Awake()
    {
        if (stamina == null) stamina = GetComponent<PlayerStamina>();
        if (health == null) health = GetComponent<Health>();
        if (animator == null) animator = GetComponent<Animator>();
        if (meleeFighter == null) meleeFighter = GetComponent<MeleeFighter>();

        m_configRow = CharacterConfigIdentity.GetParryRow(this);

        // 配置就绪日志（每个 Play 一次）：缺行时格挡/弹反会整体静默失效，
        // 这两条是排查"按了没反应"的第一现场，由 LogParry 统一开关
        if (LogParry)
        {
            Debug.Log(m_configRow != null
                ? $"[Parry] 配置就绪：id={m_configRow.Id}，窗口 {m_configRow.ParryWindow}s / " +
                  $"硬直 {m_configRow.EnemyStaggerDuration}s / 减伤 {m_configRow.BlockDamageReduction:0.##} / " +
                  $"处决窗口 {m_configRow.RiposteWindow}s"
                : "[Parry] 【严重】读不到 Parry 配置行，格挡与弹反都不会生效", this);
        }
    }

    private void Update()
    {
        // 弹反无敌到期：真实时间判定，卡肉与暂停都不会把无敌拖长
        if (parryInvulnUntilRealtime != float.MinValue && Time.unscaledTime >= parryInvulnUntilRealtime)
        {
            parryInvulnUntilRealtime = float.MinValue;
            health?.SetInvulnerable(InvulnReasons.ParrySuccess, false);
        }

        // 死亡 / 复活演出期间不保留格挡：否则复活后没按右键也一直白拿一份减伤
        if (IsBlocking && health != null && health.IsDead) SetBlocking(false);

        // 格挡持续耗耐（T3）
        if (IsBlocking) DrainBlockStamina();
    }

    /// <summary>
    /// 格挡持续耗耐（T3）。数值来自 `Stamina.block_cost_per_second`（6/秒），
    /// 与轻攻击/翻滚同源；缺配置时 `TryConsume` 自己会告警，这里不再兜底默认值。
    ///
    /// 【为什么扣不动要直接收盾】
    ///   不收的话就是"耐力空了盾还举着" = 永久 50% 减伤，
    ///   把「耐力管理」这个核心体验直接抹平（也就是这次 T3 要堵的洞）。
    ///   暂停时 Time.deltaTime 为 0，自然不扣；卡肉会按比例减慢，属可接受。
    /// </summary>
    private void DrainBlockStamina()
    {
        if (stamina == null) return;

        Stamina row = stamina.Row;
        if (row == null) return;

        float cost = row.BlockCostPerSecond * Time.deltaTime;
        if (cost <= 0f) return;

        if (stamina.TryConsume(cost)) return;

        // 耐力见底 / 已力竭 / 输入锁定：破防收盾
        SetBlocking(false);
        if (LogParry) Debug.Log("[Parry] 耐力不足，格挡被破", this);
    }

    /// <summary>
    /// 按下 / 松开格挡键。输入层（PlayerController.GetBlockInput）直接转发。
    /// 只在 false→true 的那一帧重记窗口起点，按住期间 IsInParryWindow 自然过期。
    /// 同时驱动举盾姿态（Override Layer，攻击/受击/死亡也在这一层）。
    /// </summary>
    public void SetBlocking(bool value)
    {
        if (IsBlocking == value) return;
        IsBlocking = value;
        if (value) blockStartTime = Time.unscaledTime;

        // 起手/循环姿态用协程切换；收盾要立刻把覆盖层切回 Empty
        if (m_blockPoseRoutine != null)
        {
            StopCoroutine(m_blockPoseRoutine);
            m_blockPoseRoutine = null;
        }

        if (value)
        {
            m_blockPoseRoutine = StartCoroutine(PlayBlockPose());
        }
        else if (!IsAnimatingCombatAction())
        {
            // 正在挥刀/挨打时不抢覆盖层：那一层此刻由 MeleeFighter 在管
            PlayEmptyPose(0.15f);
        }
    }

    /// <summary>
    /// 举盾姿态：先播起手，delay 后若还在格挡就切循环。
    ///
    /// ⚠️ 覆盖层（layer 1）不会自己复位 —— 收盾/破防/死亡时都必须显式切回 Empty，
    /// 否则角色会一直端着盾走路（与死亡动作那条坑同源）。
    /// 状态名在 PlayerMove.controller 里已存在；缺失时 `CrossFade` 会被 HasState 挡掉，不会刷警告。
    /// </summary>
    private IEnumerator PlayBlockPose()
    {
        if (!IsAnimatingCombatAction()) CrossFadeCombat(blockStartState, 0.12f);

        yield return new WaitForSeconds(Mathf.Max(0f, blockStartToLoopDelay));

        if (IsBlocking && !IsAnimatingCombatAction()) CrossFadeCombat(blockLoopState, 0.12f);
        m_blockPoseRoutine = null;
    }

    /// <summary>把战斗层切回空状态（收盾 / 破防 / 被打断）</summary>
    private void PlayEmptyPose(float fade)
    {
        CrossFadeCombat("Empty", fade);
    }

    /// <summary>按状态名 CrossFade（状态不存在时跳过，避免刷警告）</summary>
    private void CrossFadeCombat(string stateName, float fade)
    {
        if (animator == null || string.IsNullOrEmpty(stateName)) return;
        if (!animator.HasState(CombatLayer, Animator.StringToHash(stateName))) return;
        animator.CrossFade(stateName, fade, CombatLayer);
    }

    /// <summary>
    /// 是否正在播"别的"战斗动作（挥刀 / 受击 / 处决）。
    /// 这些动作和举盾抢同一层，谁先起手谁说话 —— 举盾不能把它们顶掉。
    /// </summary>
    private bool IsAnimatingCombatAction()
    {
        return meleeFighter != null &&
               (meleeFighter.inAction || meleeFighter.IsAttackingHit || meleeFighter.inCounter);
    }

    // ==================== IDamageInterceptor ====================

    /// <summary>
    /// 伤害拦截入口：DamageRouter 在扣血前调用。
    ///
    /// 顺序是「先试弹反，再退普通格挡」：两者共用同一个按键，
    /// 时机没踩中（按早了 / 这招不可弹反）时至少要拿到减伤，
    /// 否则按早了就是纯挨打，手感与可读性都很差。
    /// </summary>
    public bool TryIntercept(ref DamageInfo info)
    {
        if (m_configRow == null || !IsBlocking) return false;

        // 处决 / 环境 / 背刺等不可弹反伤害：既不弹反也不减伤，保持原样
        if (!info.IsParryable) return false;

        IParryTarget target = info.Source != null ? info.Source.GetComponent<IParryTarget>() : null;

        // ① 弹反：时机成立 + 攻击方这一招此刻可被弹反
        if (target != null && target.IsInParryWindow && IsParryTiming(target))
        {
            if (TryParry(target))
            {
                info.Amount = 0f;   // 弹反成功 = 本次伤害不结算（也不会触发受击/卡肉）
                return true;
            }
        }

        // ② 普通格挡：按配置减伤，不硬直、不打断敌人
        info.Amount *= 1f - Mathf.Clamp01(m_configRow.BlockDamageReduction);
        return true;
    }

    /// <summary>
    /// 弹反时机，两种打法都算，缺任何一条都会退化成「只能普通格挡」：
    ///
    ///   ① 精确弹反：按下格挡后 parry_window（0.15s）内被命中 —— 按在命中之前的那一瞬间。
    ///   ② 见招拆招：格挡是在【本次攻击的前摇开始之后】才按下的，并一直按到命中。
    ///
    /// 为什么要 ②：敌人前摇通常 0.5–1s，而 ① 只有 0.15s ——
    /// 照「看到抬刀就架格挡」这种最自然的打法，命中时距按下早过了窗口，
    /// 结果是永远只算普通格挡、弹反永远不触发（实测症状：敌人从不硬直，`[Parry]` 一条都没有）。
    /// 反过来，如果你在敌人出手【之前】就一直按着格挡，前摇开始时你已经在格挡了，
    /// ② 不成立 —— 这种"提前架着"只给减伤，避免按住不放就白嫖弹反。
    /// </summary>
    private bool IsParryTiming(IParryTarget target)
    {
        return IsInParryWindow || IsGuardRaisedAfterWindup(target);
    }

    /// <summary>格挡是否是在对方这次攻击进入前摇之后才按下的（含"看到起手才架刀"）</summary>
    private bool IsGuardRaisedAfterWindup(IParryTarget target)
    {
        return target != null
               && target.WindupStartRealtime != float.MinValue
               && blockStartTime >= target.WindupStartRealtime;
    }

    /// <summary>
    /// 弹反成功：打断目标招式 + 发事件 + 给自己短暂无敌。
    ///
    /// 硬直时长由本系统按 Parry 表给出、随 OnParried 传过去 ——
    /// "弹反成功敌人硬直多久"是弹反方的数值，敌人只负责执行，
    /// 免得以后每种敌人都要在自己的表里再配一份同样的数字。
    /// </summary>
    public bool TryParry(IParryTarget target)
    {
        if (m_configRow == null || target == null) return false;

        target.OnParried(gameObject, m_configRow.EnemyStaggerDuration);

        // T5：记下处决目标。窗口时长来自 Parry.riposte_window（2s），
        // 处决演出本身由 PlayerCombat 在"轻攻击"里触发（见 ResolveExecutionTarget）。
        SetRiposteTarget(target);

        // 弹反后短暂无敌，避免被同一招的后续判定二次命中
        health?.SetInvulnerable(InvulnReasons.ParrySuccess, true);
        parryInvulnUntilRealtime = Time.unscaledTime + m_configRow.ParryInvulnDuration;

        GameEvents.RaiseParrySuccess();

        if (LogParry)
        {
            Debug.Log($"[Parry] 弹反成功：敌方 {target} 硬直 {m_configRow.EnemyStaggerDuration:0.##}s，" +
                      $"自身无敌 {m_configRow.ParryInvulnDuration:0.##}s，" +
                      $"处决窗口 {m_configRow.RiposteWindow:0.##}s", this);
        }
        return true;
    }

    // ==================== T5：弹反后的处决窗口 ====================

    /// <summary>
    /// 记录处决目标。`IParryTarget` 由 MeleeFighter 实现，敌人本体在同一 GameObject 上。
    /// </summary>
    private void SetRiposteTarget(IParryTarget target)
    {
        m_riposteTarget = null;
        riposteDeadlineRealtime = float.MinValue;

        if (target is MonoBehaviour behaviour)
            m_riposteTarget = behaviour.GetComponent<EnemyController>();

        if (m_riposteTarget == null) return;

        // riposte_window = 0 视为不限制（表注释就是这么写的），>0 才设截止时刻
        float window = m_configRow.RiposteWindow;
        riposteDeadlineRealtime = window > 0f ? Time.unscaledTime + window : float.MaxValue;
    }

    /// <summary>
    /// 取当前的处决目标（只读，不消费）。过期 / 已死 / 已站起来的都会自动清空。
    /// 由 `PlayerCombat` 在轻攻击时调用；将来做处决提示 UI 也可读 `HasRiposteTarget`。
    /// </summary>
    public EnemyController GetRiposteTarget()
    {
        if (m_riposteTarget == null) return null;

        if (Time.unscaledTime > riposteDeadlineRealtime
            || m_riposteTarget.Health == null
            || m_riposteTarget.Health.IsDead
            || !m_riposteTarget.IsStaggered)   // 硬直已经结束（敌人站起来了）就不再算处决目标
        {
            ClearRiposteTarget();
            return null;
        }

        return m_riposteTarget;
    }

    /// <summary>处决已发起 / 窗口作废时清掉目标</summary>
    public void ClearRiposteTarget()
    {
        m_riposteTarget = null;
        riposteDeadlineRealtime = float.MinValue;
    }

    /// <summary>
    /// 是否把弹反/格挡打到 Console。排查"到底弹没弹反"时看这两条：
    /// 弹反成功会打 [Parry]，普通格挡只在 DamageRouter 的 [Damage] 里看到半伤数字。
    /// </summary>
    public static bool LogParry = true;

    private void OnDisable()
    {
        // 组件被禁用 = 拦截层关闭，但状态要清干净：否则重新启用后仍是"按住格挡"
        IsBlocking = false;
        parryInvulnUntilRealtime = float.MinValue;
        health?.SetInvulnerable(InvulnReasons.ParrySuccess, false);
    }
}

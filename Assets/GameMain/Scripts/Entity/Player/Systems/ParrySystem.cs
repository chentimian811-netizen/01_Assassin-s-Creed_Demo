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
/// 【与 T3 的分界】格挡持续扣耐力（Stamina.block_cost_per_second）属 T3，本文件暂不消耗；
///   这里只留 stamina 引用，T3 直接接上即可。
/// </summary>
public class ParrySystem : MonoBehaviour, IDamageInterceptor
{
    [Header("组件引用")]
    [Tooltip("耐力组件。格挡持续耗耐（T3）要用；留空则 Awake 自动 GetComponent")]
    [SerializeField] private PlayerStamina stamina;

    [Tooltip("自身血量组件，弹反成功后的短暂无敌挂在这里；留空则 Awake 自动 GetComponent")]
    [SerializeField] private Health health;

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

    private void Awake()
    {
        if (stamina == null) stamina = GetComponent<PlayerStamina>();
        if (health == null) health = GetComponent<Health>();

        m_configRow = CharacterConfigIdentity.GetParryRow(this);
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
    }

    /// <summary>
    /// 按下 / 松开格挡键。输入层（PlayerController.GetBlockInput）直接转发。
    /// 只在 false→true 的那一帧重记窗口起点，按住期间 IsInParryWindow 自然过期。
    /// </summary>
    public void SetBlocking(bool value)
    {
        if (IsBlocking == value) return;
        IsBlocking = value;
        if (value) blockStartTime = Time.unscaledTime;
    }

    // ==================== IDamageInterceptor ====================

    /// <summary>
    /// 伤害拦截入口：DamageRouter 在扣血前调用。
    ///
    /// 顺序是「先试弹反，再退普通格挡」：两者共用同一个按键，
    /// 精确操作没命中（按早了 / 这招不可弹反）时至少要拿到减伤，
    /// 否则按早了就是纯挨打，手感与可读性都很差。
    /// </summary>
    public bool TryIntercept(ref DamageInfo info)
    {
        if (m_configRow == null || !IsBlocking) return false;

        // 处决 / 环境 / 背刺等不可弹反伤害：既不弹反也不减伤，保持原样
        if (!info.IsParryable) return false;

        // ① 弹反：玩家在按下窗口内，且攻击方这一招此刻可被弹反
        if (IsInParryWindow)
        {
            IParryTarget target = info.Source != null ? info.Source.GetComponent<IParryTarget>() : null;
            if (target != null && target.IsInParryWindow && TryParry(target))
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

        // 弹反后短暂无敌，避免被同一招的后续判定二次命中
        health?.SetInvulnerable(InvulnReasons.ParrySuccess, true);
        parryInvulnUntilRealtime = Time.unscaledTime + m_configRow.ParryInvulnDuration;

        GameEvents.RaiseParrySuccess();

        if (LogParry)
        {
            Debug.Log($"[Parry] 弹反成功：敌方 {target} 硬直 {m_configRow.EnemyStaggerDuration:0.##}s，" +
                      $"自身无敌 {m_configRow.ParryInvulnDuration:0.##}s", this);
        }
        return true;
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

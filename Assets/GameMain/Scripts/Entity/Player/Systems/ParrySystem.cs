using UnityEngine;

/// <summary>
/// 弹反 / 格挡。P0 空壳骨架；P3 实装窗口判定与敌人硬直。
///
/// 分工：
///   - 格挡（持续按住）：减伤，扣格挡耐力
///   - 弹反（按下瞬间的窗口内被攻击）：敌人硬直 + 玩家可反击
/// </summary>
public class ParrySystem : MonoBehaviour
{
    [SerializeField] private PlayerStamina stamina;
    [SerializeField] private Health health;

    // 【数值来源】Parry 表（Gameplay.xlsx），Awake 时解析一次。
    //   注意：本工程当前没有 ParrySystem 实例，也没有 ParryConfig 资产，
    //   表里的 player_main 行只是"未启用功能的开发初始值"，不代表弹反玩法已验证。
    private Parry m_configRow;

    private float blockStartTime = float.MinValue;

    // 无敌截止时刻（真实时间）。⚠️ 不能用 Invoke：它走缩放时间，
    // 卡肉(0.05) 会把 0.3s 拉成 6s，暂停时永远不清 —— 违反红线 11。
    private float parryInvulnUntilRealtime = float.MinValue;

    /// <summary>是否正在格挡（按住状态）</summary>
    public bool IsBlocking { get; private set; }

    /// <summary>是否处于弹反窗口内（按下后的 parryWindow 秒）</summary>
    public bool IsInParryWindow =>
        m_configRow != null && IsBlocking &&
        (Time.unscaledTime - blockStartTime) <= m_configRow.ParryWindow;

    private void Awake()
    {
        m_configRow = CharacterConfigIdentity.GetParryRow(this);
    }

    private void Update()
    {
        if (parryInvulnUntilRealtime != float.MinValue && Time.unscaledTime >= parryInvulnUntilRealtime)
        {
            parryInvulnUntilRealtime = float.MinValue;
            health?.SetInvulnerable(InvulnReasons.ParrySuccess, false);
        }
    }

    public void SetBlocking(bool value)
    {
        if (IsBlocking == value) return;
        IsBlocking = value;
        if (value) blockStartTime = Time.unscaledTime;
    }

    /// <summary>由承伤流程调用：判断能否弹反当前这次攻击</summary>
    public bool TryParry(IParryTarget target)
    {
        if (m_configRow == null || target == null) return false;
        if (!IsInParryWindow || !target.IsInParryWindow) return false;

        target.OnParried(gameObject);

        // 弹反成功后短暂无敌，避免被同一招的后续判定二次命中
        health?.SetInvulnerable(InvulnReasons.ParrySuccess, true);
        parryInvulnUntilRealtime = Time.unscaledTime + m_configRow.ParryInvulnDuration;

        GameEvents.RaiseParrySuccess();
        return true;
    }

    /// <summary>格挡减伤：由 Health 在扣血前查询，返回减伤后的伤害</summary>
    public float ApplyBlockReduction(float incoming)
    {
        if (!IsBlocking || m_configRow == null) return incoming;
        if (IsInParryWindow) return 0f;   // 弹反窗口内完全免伤
        return incoming * (1f - m_configRow.BlockDamageReduction);
    }

    private void OnDisable()
    {
        parryInvulnUntilRealtime = float.MinValue;
        health?.SetInvulnerable(InvulnReasons.ParrySuccess, false);
    }
}

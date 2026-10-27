using UnityEngine;

/// <summary>
/// 玩家法力（蓝条）系统。挂在玩家根节点（与 PlayerStamina 同物体）。
///
/// 【数值来源】Mana 表（Gameplay.xlsx 的 Mana Sheet），按根对象
/// CharacterConfigIdentity 的配置 ID 取行 —— 与 Stamina 表同一套约定。
///
/// 【缺失配置时的行为】报出具体配置 ID 并拒绝消耗，绝不静默退回默认值：
/// 静默兜底会让"改了表却没生效"看起来像正常，排查成本极高。
///
/// 【当前消耗方】无（技能系统尚未实现）。TryConsume 预留给后续技能/法术，
/// 现在只有自然回蓝与 UI 显示在用它。
/// </summary>
[DisallowMultipleComponent]
public class PlayerMana : MonoBehaviour
{
    [Header("配置身份")]
    [Tooltip("留空则沿父链自动查找角色根对象上的 CharacterConfigIdentity")]
    [SerializeField] private CharacterConfigIdentity identity;

    /// <summary>当前生效的 Mana 表行（唯一数值来源，可能为 null）</summary>
    private Mana m_row;

    private float m_current;
    private bool m_regenPaused;

    // 最后一次消耗的时间（受 timeScale 影响，与游戏节奏一致）
    private float m_lastConsumeTime = float.MinValue;

    // 已发布的法力快照，用于"只在变化时发事件"
    private float m_lastPublished = float.NaN;

    public float Current => m_current;

    /// <summary>法力上限。配置缺失时为 0（配合错误日志，不做默认值兜底）</summary>
    public float Max => m_row != null ? m_row.MaxMana : 0f;

    public float Normalized => Max > 0f ? m_current / Max : 0f;

    /// <summary>当前生效的配置行；缺失时为 null，调用方需自行判空</summary>
    public Mana Row => m_row;

    /// <summary>当前使用的配置 ID（调试/日志用）</summary>
    public string ConfigId => m_row != null ? m_row.Id : null;

    private void Awake()
    {
        ResolveConfig();
        m_current = Max;
    }

    private void OnEnable()
    {
        // 重置"已发布"快照，确保重新启用后 HUD 会收到一次刷新
        m_lastPublished = float.NaN;
    }

    /// <summary>
    /// 解析 Mana 行。放在 Awake 里解析一次：数值只在导表后重新 Play 才生效，
    /// 运行中不查表，避免每帧字典查找。
    /// </summary>
    private void ResolveConfig()
    {
        if (identity == null)
        {
            identity = CharacterConfigIdentity.Resolve(this);
        }

        if (identity == null || !identity.HasConfigId)
        {
            Debug.LogError(
                "[PlayerMana] 角色根对象缺少 CharacterConfigIdentity 或配置 ID 为空，" +
                "无法读取 Mana 表；所有法力消耗都会被拒绝", this);
            m_row = null;
            return;
        }

        m_row = DataRepository.GetMana(identity.ConfigId);
        if (m_row == null)
        {
            Debug.LogError($"[PlayerMana] 读不到 Mana 配置行：id={identity.ConfigId}，" +
                           "请检查 Gameplay.xlsx 的 Mana Sheet", this);
        }
    }

    private void Update()
    {
        Regenerate();
        PublishIfChanged();
    }

    /// <summary>自然恢复：受 regenDelayAfterUse 限制，用 deltaTime 与游戏节奏一致</summary>
    private void Regenerate()
    {
        if (m_row == null || m_regenPaused) return;
        if (m_current >= Max) return;

        if (Time.time - m_lastConsumeTime < m_row.RegenDelayAfterUse) return;

        m_current = Mathf.Min(Max, m_current + m_row.RegenPerSecond * Time.deltaTime);
    }

    /// <summary>扣法力。返回 false 表示数值不足 / 缺配置，调用方应拒绝技能</summary>
    public bool TryConsume(float amount)
    {
        if (m_row == null)
        {
            Debug.LogWarning("[PlayerMana] 缺少 Mana 配置行，拒绝消耗以免静默失败", this);
            return false;
        }
        if (amount <= 0f) return true;
        if (m_current < amount) return false;

        m_current -= amount;
        m_lastConsumeTime = Time.time;
        PublishIfChanged();
        return true;
    }

    public void SetRegenPaused(bool value) => m_regenPaused = value;

    /// <summary>回满并恢复回蓝（篝火/复活用）</summary>
    public void RestoreFull()
    {
        m_current = Max;
        m_regenPaused = false;
        PublishIfChanged();
    }

    /// <summary>只在数值变化时发事件（每帧无条件发会导致 HUD 空转重绘）</summary>
    private void PublishIfChanged()
    {
        if (Mathf.Approximately(m_lastPublished, m_current)) return;
        m_lastPublished = m_current;
        GameEvents.RaisePlayerManaChanged(m_current, Max);
    }
}

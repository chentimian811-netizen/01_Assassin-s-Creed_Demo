using UnityEngine;

/// <summary>
/// 耐力系统。挂在 Player 根节点（与 MeleeFighter 同物体）。
///
/// 【数值来源】Stamina 表（Gameplay.xlsx 的 Stamina Sheet），按根对象
/// CharacterConfigIdentity 的配置 ID 取行 —— 不再读 StaminaConfig SO。
/// SO 文件在迁移期仍保留用于回退对照，但本组件一行都不读它（计划 Task 4 步骤 7）。
///
/// 【缺失配置时的行为】报出具体配置 ID 并拒绝消耗，绝不静默退回 100 之类的默认值：
/// 静默兜底会让"改了表却没生效"看起来像正常，排查成本极高。
///
/// 关键设计：力竭 / 输入锁定 / 暂停恢复 三种状态互相独立，
/// 不用一个 bool 全关，否则会出现"演出结束后耐力永远不恢复"。
/// </summary>
[DisallowMultipleComponent]
public class PlayerStamina : MonoBehaviour, IStaminaUser
{
    [Header("配置身份")]
    [Tooltip("留空则沿父链自动查找角色根对象上的 CharacterConfigIdentity")]
    [SerializeField] private CharacterConfigIdentity identity;

    /// <summary>当前生效的 Stamina 表行（唯一数值来源，可能为 null）</summary>
    private Stamina m_row;

    private float current;
    private bool isExhausted;
    private bool isInputLocked;
    private bool isRegenPaused;

    // 最后一次消耗的时间（受 timeScale 影响，与游戏节奏一致）
    private float lastConsumeTime = float.MinValue;
    // 力竭锁定截止时间（用真实时间，避免卡肉把力竭无限延长）
    private float exhaustedUntilRealtime = float.MinValue;

    // 已发布的耐力快照，用于"只在变化时发事件"
    private float lastPublished = float.NaN;

    public float Current => current;

    /// <summary>耐力上限。配置缺失时为 0（配合错误日志，不做默认值兜底）</summary>
    public float Max => m_row != null ? m_row.MaxStamina : 0f;

    public bool IsExhausted => isExhausted;
    public bool IsInputLocked => isInputLocked;
    public float Normalized => Max > 0f ? current / Max : 0f;

    /// <summary>当前生效的配置行；缺失时为 null，调用方需自行判空</summary>
    public Stamina Row => m_row;

    /// <summary>当前使用的配置 ID（调试/日志用）</summary>
    public string ConfigId => m_row != null ? m_row.Id : null;

    private void Awake()
    {
        ResolveConfig();
        current = Max;
    }

    private void OnEnable()
    {
        lastPublished = float.NaN;
    }

    /// <summary>
    /// 解析 Stamina 行。放在 Awake 里解析一次：数值只在导表后重新 Play 才生效，
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
                "[PlayerStamina] 角色根对象缺少 CharacterConfigIdentity 或配置 ID 为空，" +
                "无法读取 Stamina 表；所有耐力消耗都会被拒绝", this);
            m_row = null;
            return;
        }

        m_row = DataRepository.GetStamina(identity.ConfigId);
        if (m_row == null)
        {
            Debug.LogError($"[PlayerStamina] 读不到 Stamina 配置行：id={identity.ConfigId}，" +
                           "请检查 Gameplay.xlsx 的 Stamina Sheet", this);
        }
    }

    private void Update()
    {
        // 力竭锁定用真实时间判定，避免被卡肉/暂停冻结
        if (isExhausted && Time.unscaledTime >= exhaustedUntilRealtime)
        {
            isExhausted = false;
        }

        Regenerate();
        PublishIfChanged();
    }

    /// <summary>自然恢复：受 regenDelayAfterUse 与力竭锁限制，用 deltaTime 与游戏节奏一致</summary>
    private void Regenerate()
    {
        if (m_row == null || isRegenPaused || isExhausted) return;
        if (current >= Max) return;

        if (Time.time - lastConsumeTime < m_row.RegenDelayAfterUse) return;

        current = Mathf.Min(Max, current + m_row.RegenPerSecond * Time.deltaTime);
    }

    public bool TryConsume(float amount)
    {
        if (m_row == null)
        {
            Debug.LogWarning("[PlayerStamina] 缺少 Stamina 配置行，拒绝消耗以免静默失败", this);
            return false;
        }
        if (isInputLocked || isExhausted) return false;
        if (amount <= 0f) return true;
        if (current < amount) return false;

        current -= amount;
        lastConsumeTime = Time.time;

        if (current <= 0f)
        {
            current = 0f;
            isExhausted = true;
            exhaustedUntilRealtime = Time.unscaledTime + m_row.ExhaustedLockDuration;
        }

        PublishIfChanged();
        return true;
    }

    public void SetInputLocked(bool value) => isInputLocked = value;

    public void SetRegenPaused(bool value) => isRegenPaused = value;

    public void RestoreFull()
    {
        current = Max;
        isExhausted = false;
        isRegenPaused = false;
        PublishIfChanged();
    }

    /// <summary>只在数值变化时发事件（每帧无条件发会导致 HUD 空转重绘）</summary>
    private void PublishIfChanged()
    {
        if (Mathf.Approximately(lastPublished, current)) return;
        lastPublished = current;
        GameEvents.RaisePlayerStaminaChanged(current, Max);
    }
}

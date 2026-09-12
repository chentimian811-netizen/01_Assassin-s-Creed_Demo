using UnityEngine;

/// <summary>
/// 耐力系统。挂在 Player 根节点（与 MeleeFighter 同物体）。
/// P0：编译通过 + 事件占位 + 恢复逻辑骨架；P1：接 PlayerCombat 的消耗调用。
///
/// 关键设计：力竭 / 输入锁定 / 暂停恢复 三种状态互相独立，
/// 不用一个 bool 全关，否则会出现"演出结束后耐力永远不恢复"。
/// </summary>
[DisallowMultipleComponent]
public class PlayerStamina : MonoBehaviour, IStaminaUser
{
    [SerializeField] private StaminaConfig config;

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
    public float Max => config != null ? config.maxStamina : 100f;
    public bool IsExhausted => isExhausted;
    public bool IsInputLocked => isInputLocked;
    public float Normalized => Max > 0f ? current / Max : 0f;

    /// <summary>供 PlayerCombat 等读取消耗数值，避免各处再挂一份 SO</summary>
    public StaminaConfig Config => config;

    private void Awake()
    {
        if (config == null) Debug.LogWarning("[PlayerStamina] 未配置 StaminaConfig，将使用默认值", this);
        current = Max;
    }

    private void OnEnable()
    {
        lastPublished = float.NaN;
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
        if (config == null || isRegenPaused || isExhausted) return;
        if (current >= Max) return;

        if (Time.time - lastConsumeTime < config.regenDelayAfterUse) return;

        current = Mathf.Min(Max, current + config.regenPerSecond * Time.deltaTime);
    }

    public bool TryConsume(float amount)
    {
        if (config == null)
        {
            Debug.LogWarning("[PlayerStamina] 缺少 StaminaConfig，拒绝消耗以免静默失败", this);
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
            exhaustedUntilRealtime = Time.unscaledTime + config.exhaustedLockDuration;
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

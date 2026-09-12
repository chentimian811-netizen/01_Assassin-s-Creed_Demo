/// <summary>
/// 需要消耗耐力的操作的统一能力收口。
/// 注意：这里刻意不提供"整体开关"API ——
/// 禁用消耗（演出/暂停）与暂停恢复是两个独立状态，混在一起会导致耐力永远回不满。
/// </summary>
public interface IStaminaUser
{
    float Current { get; }
    float Max { get; }

    /// <summary>是否处于力竭（耐力耗尽后的锁定窗口）</summary>
    bool IsExhausted { get; }

    /// <summary>是否被外部锁定（演出/UI/死亡），锁定时拒绝消耗但不影响恢复</summary>
    bool IsInputLocked { get; }

    /// <summary>尝试消耗；不足或被锁返回 false 且不扣</summary>
    bool TryConsume(float amount);

    /// <summary>锁定/解锁消耗</summary>
    void SetInputLocked(bool value);

    /// <summary>暂停/恢复自然恢复（不影响力竭计时）</summary>
    void SetRegenPaused(bool value);

    /// <summary>直接回满（篝火）</summary>
    void RestoreFull();
}

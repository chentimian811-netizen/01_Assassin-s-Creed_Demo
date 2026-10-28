using UnityEngine;

/// <summary>
/// 世界可交互物的统一入口（篝火 / 掉落魂 / 雾门 / 捷径门）。
///
/// 【为什么需要这一层】
///   E 键原先只认「最近的商店 NPC」与「最近的武器」，两者在 PlayerController 上
///   各占一个并列字段（SetNearestShopNPC / SetNearestPickup）。关卡片子再逐个加
///   SetNearestXxx，输入层就会变成一堆并列分支，而且每个分支都要自己判优先级。
///   本接口只暴露三件事：提示语、当前能否交互、交互动作。
///
/// 【约定】
///   1. 实现方保留自己的业务方法名（Bonfire.Interact / SoulsPickup.Collect /
///      FogWall.Enter / ShortcutGate.Unlock），接口方法只做转发 —— 调用点仍可按原有
///      语义词调用，日志与论文对照都不用改名；
///   2. 触发范围与提示交给 InteractableSensor，实现方不要自己再写 OnTriggerEnter，
///      否则一个物体上会出现两份「玩家进入范围」的判定，提示互相打架；
///   3. 提示文本自己带按键前缀（如「[E] 点燃篝火」），因为按键归属属于交互物本身，
///      输入层只负责转发，不该硬编码「按 E」。
/// </summary>
public interface IInteractable
{
    /// <summary>交互提示文本（如「[E] 点燃篝火」）。空串 = 不显示提示</summary>
    string InteractPrompt { get; }

    /// <summary>当前是否可交互（已开的捷径门、已清掉的雾门返回 false，E 会落到别的目标上）</summary>
    bool CanInteract { get; }

    /// <summary>提示锚点。当前只用于取位置/距离，将来做世界空间提示会用到朝向</summary>
    Transform InteractAnchor { get; }

    /// <summary>执行交互。由 E 键输入层调用</summary>
    void Interact();
}

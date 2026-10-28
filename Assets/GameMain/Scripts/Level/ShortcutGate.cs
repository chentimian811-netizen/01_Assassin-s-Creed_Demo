using UnityEngine;

/// <summary>
/// 捷径门：从内侧打开后永久通行。
///
/// 【本阶段只做「能开」】Unlock() 仍然只置内存标记并发 OnShortcutUnlocked；
/// 真正的单向打通（门物体换位/禁用碰撞）与 PlayerPrefs 持久化属于 §14.3 C4，
/// 下一步再补 —— 现在退出 Play 会重置，属已知边界。
/// 交互范围与提示由同物体上的 InteractableSensor 负责。
/// </summary>
public class ShortcutGate : MonoBehaviour, IInteractable
{
    [SerializeField] private string gateId = "gate_01";
    [SerializeField] private bool isUnlocked;

    public string GateId => gateId;
    public bool IsUnlocked => isUnlocked;

    // ==================== IInteractable ====================

    public string InteractPrompt => "按 E 打开捷径门";

    /// <summary>已开过的门不再参与 E 键竞争（否则站在门口按 E 只会弹一次无用提示）</summary>
    public bool CanInteract => !isUnlocked;

    public Transform InteractAnchor => transform;

    public void Interact() => Unlock();

    /// <summary>由交互层调用（保留原方法名）</summary>
    public void Unlock()
    {
        if (isUnlocked) return;
        isUnlocked = true;

        GameEvents.RaiseShortcutUnlocked(gateId);
        ToastService.Show($"捷径已打通：{gateId}");
        Debug.Log($"[ShortcutGate] 捷径已开启：{gateId}");
    }
}

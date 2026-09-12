using UnityEngine;

/// <summary>
/// 捷径门：从内侧打开后永久通行。P5 实装。
/// </summary>
public class ShortcutGate : MonoBehaviour
{
    [SerializeField] private string gateId = "gate_01";
    [SerializeField] private bool isUnlocked;

    public string GateId => gateId;
    public bool IsUnlocked => isUnlocked;

    public void Unlock()
    {
        if (isUnlocked) return;
        isUnlocked = true;
        GameEvents.RaiseShortcutUnlocked(gateId);
    }
}

using UnityEngine;

/// <summary>
/// 雾门：Boss 战边界。穿过即进入 Boss 场、不可返回，Boss 死亡后解除。
///
/// 【本阶段只做「能进」】Enter() 仍然只发 OnFogWallEntered 事件；
/// 传送 / 单向封锁 / Clear 的时机属于 §14.3 C5，等 Boss 场实例存在后再接。
/// 交互范围与提示由同物体上的 InteractableSensor 负责。
/// </summary>
public class FogWall : MonoBehaviour, IInteractable
{
    [SerializeField] private string fogWallId = "fog_01";

    public string FogWallId => fogWallId;

    // ==================== IInteractable ====================

    public string InteractPrompt => "按 E 穿过雾门";

    public bool CanInteract => isActiveAndEnabled;

    public Transform InteractAnchor => transform;

    public void Interact() => Enter();

    /// <summary>由交互层调用（保留原方法名）</summary>
    public void Enter()
    {
        GameEvents.RaiseFogWallEntered(fogWallId);
        Debug.Log($"[FogWall] 进入雾门：{fogWallId}");
    }

    public void Clear()
    {
        GameEvents.RaiseFogWallCleared(fogWallId);
        Debug.Log($"[FogWall] 雾门已解除：{fogWallId}");
        gameObject.SetActive(false);
    }
}

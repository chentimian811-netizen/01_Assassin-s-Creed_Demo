using UnityEngine;

/// <summary>
/// 雾门：Boss 战边界。P5/P6 实装进出检测与击杀后清除。
/// </summary>
public class FogWall : MonoBehaviour
{
    [SerializeField] private string fogWallId = "fog_01";

    public string FogWallId => fogWallId;

    public void Enter()
    {
        GameEvents.RaiseFogWallEntered(fogWallId);
    }

    public void Clear()
    {
        GameEvents.RaiseFogWallCleared(fogWallId);
        gameObject.SetActive(false);
    }
}

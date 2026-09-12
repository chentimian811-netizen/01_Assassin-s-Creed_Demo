using UnityEngine;

/// <summary>
/// 篝火：交互后回满状态、设重生点、通知敌人重生。P5 实装存档与敌人生成。
/// </summary>
public class Bonfire : MonoBehaviour
{
    [SerializeField] private string bonfireId = "bonfire_01";
    [SerializeField] private float interactRadius = 2f;

    [Header("重生点")]
    [Tooltip("为空则用篝火自身位置偏移")]
    [SerializeField] private Transform respawnPoint;
    [SerializeField] private Vector3 respawnOffset = new Vector3(0f, 0.1f, 0f);

    public string BonfireId => bonfireId;
    public Vector3 RespawnPosition =>
        respawnPoint != null ? respawnPoint.position : transform.position + respawnOffset;

    /// <summary>由交互系统（拾取/交互输入）调用</summary>
    public void Interact()
    {
        // P1/P5：回满血量与耐力
        var health = FindPlayerHealth();
        health?.ResetToFull();

        var stamina = health != null ? health.GetComponent<PlayerStamina>() : null;
        stamina?.RestoreFull();

        // P5：CheckpointManager.SetRespawn(this) + 敌人重生 + 存档
        GameEvents.RaiseBonfireLit(bonfireId);
        Debug.Log($"[Bonfire] Lit: {bonfireId}");
    }

    private static Health FindPlayerHealth()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        return player != null ? player.GetComponent<Health>() : null;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, interactRadius);
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(RespawnPosition, 0.3f);
    }
}

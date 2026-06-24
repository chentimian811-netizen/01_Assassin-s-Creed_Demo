using UnityEngine;

/// <summary>
/// 门触发器 - 挂载在门物体上
/// 当 Boss 被击败且玩家靠近门时，弹出游戏获胜界面
/// </summary>
public class GateTrigger : MonoBehaviour
{
    [Header("设置")]
    [Tooltip("Boss 控制器引用（用于检测 Boss 是否死亡）")]
    [SerializeField] private BossController bossController;

    [Tooltip("触发范围（球形检测半径）")]
    [SerializeField] private float triggerRadius = 3f;

    [Tooltip("触发器所在的物体位置（可选，默认使用自身）")]
    [SerializeField] private Transform triggerPoint;

    private bool hasTriggered = false; // 防止重复触发

    private void Update()
    {
        // 已触发过，不再检测
        if (hasTriggered) return;

        // Boss 还没死，不检测
        if (bossController != null && !IsBossDead()) return;

        // 检测玩家是否进入范围
        CheckPlayerProximity();
    }

    /// <summary>
    /// 检测 Boss 是否已死亡
    /// </summary>
    private bool IsBossDead()
    {
        // Boss 的 MeleeFighter 血量为 0 即为死亡
        if (bossController.Fighter != null)
        {
            return bossController.Fighter.Health <= 0;
        }
        return false;
    }

    /// <summary>
    /// 检测玩家是否在门附近
    /// </summary>
    private void CheckPlayerProximity()
    {
        // 查找场景中的玩家
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player == null) return;

        // 计算距离
        Vector3 checkPos = triggerPoint != null ? triggerPoint.position : transform.position;
        float distance = Vector3.Distance(player.transform.position, checkPos);

        // 玩家进入触发范围
        if (distance <= triggerRadius)
        {
            TriggerVictory();
        }
    }

    /// <summary>
    /// 触发游戏获胜
    /// </summary>
    private void TriggerVictory()
    {
        hasTriggered = true;

        // 打开获胜面板
        if (UIManager.Instance != null)
        {
            UIManager.Instance.OpenPanel(UIconst.VictoryPanel);
        }

        Debug.Log("[GateTrigger] 游戏获胜！玩家到达了门");
    }

    /// <summary>
    /// 在 Scene 视图中绘制检测范围（仅编辑器可见）
    /// </summary>
    private void OnDrawGizmosSelected()
    {
        Vector3 checkPos = triggerPoint != null ? triggerPoint.position : transform.position;
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(checkPos, triggerRadius);
    }
}

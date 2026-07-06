using UnityEngine;

/// <summary>
/// 巡逻路径点 —— 放在场景中标记巡逻位置
/// 挂载到空物体上，放在 NavMesh 可达的地面上
/// </summary>
public class PatrolPiont : MonoBehaviour
{
    //到达路径点后的等待时间
    [SerializeField]private float waitTime = 0f;

    //路径点的停留等待时间（供patrolState 读取）
    public float WaiteTime => waitTime;

    //在场景中绘制路近点标记，方便调试
    private void OnDrawGizmos()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position,0.3f);
        Gizmos .color = Color.white;
        Gizmos.DrawWireSphere(transform.position,0.15f);       
    }
}

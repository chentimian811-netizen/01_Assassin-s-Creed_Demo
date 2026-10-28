using UnityEngine;

/// <summary>
/// 篝火：交互后回满状态、登记重生点、点亮自身。
///
/// 【本阶段（P0 第 2 步）范围】
///   只做「能点 + 回满 + 成为重生点 + 日志/Toast」。敌人重生（下一步）、
///   掉魂（第 4 步）、点亮状态持久化（第 5 步）都还没接，见
///   `毕业设计/毕业设计方案.md` §14.3 的 C1–C5。
///
/// 【谁调用 Interact】
///   不自己写 OnTriggerEnter / Update 距离判定：范围与提示统一交给
///   InteractableSensor（挂在同一物体上），由玩家 E 键 → PlayerController
///   → IInteractable.Interact 进来。这样篝火、掉魂、雾门、捷径门只有一份范围逻辑。
///
/// 【为什么不在这里直接复活玩家】
///   篝火只负责「登记落点」，复活动作归 CheckpointManager（死亡出口调它），
///   否则「点篝火」会被顺带做成一次复活，玩家在篝火旁按 E 就等于回满 + 瞬移。
/// </summary>
public class Bonfire : MonoBehaviour, IInteractable
{
    [Header("标识")]
    [SerializeField] private string bonfireId = "bonfire_01";

    [Header("重生点")]
    [Tooltip("为空则用篝火自身位置 + respawnOffset")]
    [SerializeField] private Transform respawnPoint;
    [SerializeField] private Vector3 respawnOffset = new Vector3(0f, 0.1f, 0f);

    [Header("表现")]
    [Tooltip("点亮后要打开的光源。留空则 Awake 时自动取子物体上的 Light（含未激活的）")]
    [SerializeField] private Light bonfireLight;

    /// <summary>是否已点燃过。点燃 = 重生点已登记；休息不改变这个状态</summary>
    private bool m_IsLit;

    public string BonfireId => bonfireId;
    public bool IsLit => m_IsLit;

    /// <summary>复活落点（世界坐标）</summary>
    public Vector3 RespawnPosition =>
        respawnPoint != null ? respawnPoint.position : transform.position + respawnOffset;

    /// <summary>复活朝向。有独立落点就跟随它，否则跟随篝火自身朝向（面向篝火而立）</summary>
    public Quaternion RespawnRotation =>
        respawnPoint != null ? respawnPoint.rotation : transform.rotation;

    // ==================== IInteractable ====================

    /// <summary>
    /// 点燃前提示「点燃」，点燃后提示「休息」—— 提示由 InteractableSensor 显示。
    /// 提示语要短：一眼扫过去就知道按什么、会做什么；篝火 id 只留在日志里，不占提示条。
    /// </summary>
    public string InteractPrompt => m_IsLit ? "按 E 在篝火休息" : "按 E 点燃篝火";

    public bool CanInteract => true;

    public Transform InteractAnchor => transform;

    public void Interact()
    {
        var playerObject = GameObject.FindGameObjectWithTag("Player");
        var health = playerObject != null ? playerObject.GetComponent<Health>() : null;
        var stamina = playerObject != null ? playerObject.GetComponent<PlayerStamina>() : null;

        if (health == null)
        {
            Debug.LogError("[Bonfire] 找不到玩家（Tag=Player）或玩家身上没有 Health，交互中止", this);
            return;
        }

        // 1. 回满血 / 耐力。
        //    血瓶（C1 里的「血瓶回满」）目前没有瓶数系统，等做道具/消耗品时再接。
        health.ResetToFull();
        stamina?.RestoreFull();

        // 2. 登记重生点：死亡出口（DeathForm → ProcedureGame）读它决定复活落点
        CheckpointManager.Instance.SetCheckpoint(bonfireId, RespawnPosition, RespawnRotation);

        // 3. 点亮表现 + 状态
        bool firstLight = !m_IsLit;
        m_IsLit = true;
        if (bonfireLight != null) bonfireLight.enabled = true;

        // 4. 事件与日志。
        //    OnBonfireLit 目前没有订阅方（HUD 还没做篝火提示），先留着给音效/任务用。
        GameEvents.RaiseBonfireLit(bonfireId);

        string message = firstLight
            ? $"篝火已点燃：{bonfireId}（重生点已登记）"
            : $"已休息：{bonfireId}（血量与耐力回满）";
        ToastService.Show(message);
        Debug.Log($"[Bonfire] {message}，落点 {RespawnPosition}");
    }

    private void Awake()
    {
        // 部署出来的点光初始是关着的（未点燃），所以必须连未激活的一起找
        if (bonfireLight == null) bonfireLight = GetComponentInChildren<Light>(true);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, 0.5f);
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(RespawnPosition, 0.3f);
    }
}

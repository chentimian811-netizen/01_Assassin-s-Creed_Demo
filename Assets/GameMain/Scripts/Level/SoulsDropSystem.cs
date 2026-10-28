using UnityEngine;

/// <summary>
/// 掉魂 / 拾魂调度者（§14.3 C3）。
///
/// 【职责边界】
///   - 订阅 `GameEvents.OnPlayerDeath`：把玩家**当前全部金币**留在死亡位置，金币归零；
///   - 同一时刻只保留一份魂：再死一次先销毁旧魂（ER 口径 = 未拾回就永久消失）；
///   - 拾取加钱由 <see cref="SoulsPickup.Collect"/> 自己做，本类不参与。
///
/// 【为什么挂在场景物体上，而不是静态类】
///   静态类要自己订阅静态事件，就得和 `GameEvents.ResetStatics`（SubsystemRegistration）抢初始化顺序，
///   关闭 Domain Reload 时很容易变成「订阅了两次 / 一次都没订上」。
///   场景物体的 OnEnable/OnDisable 天然一一对应，和 EnemyManager 同类。
///
/// 【为什么掉的是「全部金币」】
///   本工程金币就是魂（`CurrencyManager` 读写 `PlayerPrefs["PlayerGold"]`）。
///   掉落额 = 死亡瞬间的金币数；身上为 0 时只销毁旧魂、不再生成（全损口径）。
///
/// 【为什么落点要打射线】
///   死亡时玩家可能在斜坡/台阶上、或正被击飞；直接用 transform.position 可能埋进地面或悬空。
/// </summary>
public class SoulsDropSystem : MonoBehaviour
{
    [Header("掉落物")]
    [Tooltip("掉魂用的预制体（Assets/GameMain/Entities/Level/SoulsPickup.prefab）")]
    [SerializeField] private SoulsPickup m_PickupPrefab;

    [Header("落点")]
    [Tooltip("向下探测地面的最大距离")]
    [SerializeField] private float m_GroundProbeDistance = 6f;

    [Tooltip("打不到地面时，掉落物相对玩家位置的抬高量")]
    [SerializeField] private float m_FallbackHeight = 0.6f;

    /// <summary>当前场上那一份魂（同一时刻只允许一份）</summary>
    private SoulsPickup m_Current;

    /// <summary>供调试查看：当前掉落物的金额（没有则 0）</summary>
    public int CurrentAmount => m_Current != null ? m_Current.Amount : 0;

    private void OnEnable()
    {
        GameEvents.OnPlayerDeath += HandlePlayerDeath;
    }

    private void OnDisable()
    {
        GameEvents.OnPlayerDeath -= HandlePlayerDeath;
    }

    private void HandlePlayerDeath()
    {
        PlayerController player = FindPlayer();
        if (player == null)
        {
            Debug.LogError("[SoulsDrop] 找不到玩家（Tag=Player），掉魂中止", this);
            return;
        }

        Vector3 dropPosition = ResolveDropPosition(player.transform.position);

        // 1) 旧魂作废：ER 口径 —— 未拾回就再死一次，旧魂永久消失
        if (m_Current != null)
        {
            Debug.Log($"[SoulsDrop] 旧魂作废（{m_Current.Amount} 魂未拾回就再次死亡）");
            Destroy(m_Current.gameObject);
            m_Current = null;
        }

        // 2) 掉落额 = 死亡瞬间的全部金币
        int amount = CurrencyManager.Instance.Gold;
        if (amount <= 0)
        {
            Debug.Log("[SoulsDrop] 身上没有魂，不再生成掉落物");
            return;
        }

        if (m_PickupPrefab == null)
        {
            Debug.LogError("[SoulsDrop] m_PickupPrefab 未绑定，掉魂失败（用 ACDemo/Level/Deploy Phase 4 Props 重新部署）", this);
            return;
        }

        CurrencyManager.Instance.SetGold(0);

        m_Current = Instantiate(m_PickupPrefab, dropPosition, Quaternion.identity);
        m_Current.name = $"Souls_{amount}";
        m_Current.Initialize(amount);

        GameEvents.RaiseSoulsDropped(amount);
        Debug.Log($"[SoulsDrop] 掉落 {amount} 魂 @ {dropPosition}（金币已归零）");
    }

    /// <summary>落点：从玩家位置向下打射线落到地面；打不到就抬高点，至少别埋在地里</summary>
    private Vector3 ResolveDropPosition(Vector3 playerPosition)
    {
        // 排除玩家与敌人层：死时玩家可能正贴着敌人，射线打到角色胶囊会把魂放到半空
        int mask = ~LayerMask.GetMask("Player", "Enemy");

        Vector3 rayStart = playerPosition + Vector3.up * 0.5f;
        if (Physics.Raycast(rayStart, Vector3.down, out RaycastHit hit, m_GroundProbeDistance, mask, QueryTriggerInteraction.Ignore))
        {
            return hit.point + Vector3.up * 0.1f;
        }

        return playerPosition + Vector3.up * m_FallbackHeight;
    }

    private static PlayerController FindPlayer()
    {
        GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
        return playerObject != null ? playerObject.GetComponent<PlayerController>() : null;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.45f, 0.8f, 1f);
        Gizmos.DrawWireSphere(transform.position, 0.4f);
    }
}

using UnityEngine;

/// <summary>
/// 掉落魂拾取物：玩家死亡后在死亡地点生成，按 E 拾回（§14.3 C3）。
///
/// 【谁生成它、谁销毁它】
///   生成 / 替换 / 旧魂作废都在 <see cref="SoulsDropSystem"/>（订阅 OnPlayerDeath）；
///   本类只管「拾取时把魂加回货币，然后销毁自己」。
///
/// 【为什么金额由外部 Initialize 赋值】
///   掉落额 = 死亡瞬间的全部金币，只有掉魂那一刻才知道；prefab 上的 amount 只是占位默认值，
///   直接摆进场景调试时也能用。
///
/// 【为什么实现 IInteractable 而不自己写触发】
///   交互范围与提示由同物体上的 InteractableSensor 负责（见该脚本注释）。
///   本类只回答「提示什么、能不能交互、交互做什么」。
/// </summary>
public class SoulsPickup : MonoBehaviour, IInteractable
{
    [SerializeField] private int amount = 100;

    /// <summary>已拾取：Destroy 要到帧末才生效，不加这道闸连按 E 会重复发事件（重复加钱）</summary>
    private bool m_Collected;

    public int Amount => amount;

    /// <summary>由掉魂系统在生成后调用：写入掉落额并刷新提示</summary>
    public void Initialize(int value)
    {
        amount = Mathf.Max(0, value);
    }

    // ==================== IInteractable ====================

    public string InteractPrompt => $"按 E 拾取 {amount} 魂";

    public bool CanInteract => !m_Collected;

    public Transform InteractAnchor => transform;

    public void Interact() => Collect();

    /// <summary>
    /// 拾取：加回货币 → 发事件 → 提示 → 销毁自己。
    /// 加钱走 CurrencyManager.Earn（它自己写 PlayerPrefs），不要在这里碰 PlayerPrefs。
    /// </summary>
    public void Collect()
    {
        if (m_Collected) return;
        m_Collected = true;

        int before = CurrencyManager.Instance.Gold;
        CurrencyManager.Instance.Earn(amount);

        GameEvents.RaiseSoulsPicked(amount);
        ToastService.Show($"拾回 {amount} 魂");
        Debug.Log($"[SoulsPickup] 拾回 {amount} 魂（金币 {before} → {CurrencyManager.Instance.Gold}）");

        Destroy(gameObject);
    }
}

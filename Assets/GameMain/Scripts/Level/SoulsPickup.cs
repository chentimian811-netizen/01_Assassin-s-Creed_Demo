using UnityEngine;

/// <summary>
/// 掉落魂拾取物。玩家死亡后在死亡地点生成（P5 第 4 步才部署）。
///
/// 【本阶段只做「进得去、拾得走」】
///   Collect() 目前只发 OnSoulsPicked 事件；把魂加进货币、以及「再死一次旧魂作废」
///   属于下一步（§14.3 C3），本步不碰货币。
///
/// 【为什么实现 IInteractable 而不自己写触发】
///   交互范围与提示由同物体上的 InteractableSensor 负责（见该脚本注释）。
///   本类只回答「提示什么、能不能交互、交互做什么」。
/// </summary>
public class SoulsPickup : MonoBehaviour, IInteractable
{
    [SerializeField] private int amount = 100;

    /// <summary>已拾取：Destroy 要到帧末才生效，不加这道闸连按 E 会重复发事件</summary>
    private bool m_Collected;

    public int Amount => amount;

    // ==================== IInteractable ====================

    public string InteractPrompt => $"按 E 拾取 {amount} 魂";

    public bool CanInteract => !m_Collected;

    public Transform InteractAnchor => transform;

    public void Interact() => Collect();

    /// <summary>由交互层调用（保留原方法名，日志/论文里仍叫 Collect）</summary>
    public void Collect()
    {
        if (m_Collected) return;
        m_Collected = true;

        GameEvents.RaiseSoulsPicked(amount);
        ToastService.Show($"拾回 {amount} 魂");
        Debug.Log($"[SoulsPickup] 拾回 {amount} 魂");

        Destroy(gameObject);
    }
}

using UnityEngine;

/// <summary>
/// 掉落魂拾取物。P5 与 DeathPenaltySystem 配合。
/// </summary>
public class SoulsPickup : MonoBehaviour
{
    [SerializeField] private int amount = 100;

    public int Amount => amount;

    public void Collect()
    {
        GameEvents.RaiseSoulsPicked(amount);
        Destroy(gameObject);
    }
}

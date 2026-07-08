using UnityEngine;

[CreateAssetMenu(menuName = "Item/ItemConfig")]
public class ItemConfig_SO : ScriptableObject
{
    [Header("基本信息")]
    public int itemId;
    public string itemName;

    [Header("资产引用")]
    public Sprite icon;
    public GameObject prefab;
}
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class ShopItemData
{
    public int itemID;

    public int price;

    public int stock = -1;

    [Tooltip("ÕÛ¿Û±¶ÂÊ")]
    [Range(0f, 1f)]
    public float discount = 1f;
}


[CreateAssetMenu(menuName ="Shop/ShopConfig",fileName ="NewShopConfig")]
public class ShopConfig : ScriptableObject
{
    [SerializeField]
    private List<ShopItemData> items = new List<ShopItemData>();
    public IReadOnlyList<ShopItemData> Items => items;
    
}



using System.Collections.Generic;
using UnityEngine;

public class ShopManager : MonoBehaviour
{
    private static ShopManager _instance;
    public static ShopManager Instance => _instance;

    // 库存追踪 
    private Dictionary<(int keeperId, string itemAssetId), int> stockMap
        = new Dictionary<(int keeperId, string itemAssetId), int>();
    
    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
    }
    /// <summary>
    /// 购买物品
    /// </summary>
    public bool BuyItem(int shopKeeperId, string itemAssetId)
    {
        var shopList = DataRepository.GetShopItems(shopKeeperId);
        DRShop shopData = null;
        foreach (var s in shopList)
        {
            if (s.ItemAssetId == itemAssetId) { shopData = s; break; }
        }
        if (shopData == null)
        {
            ToastMessage.Show("商品不存在！");
            return false;
        }

        int currentStock = GetStock(shopKeeperId, itemAssetId);
        if (currentStock == 0)
        {
            ToastMessage.Show("库存不足！");
            return false;
        }

        int finalPrice = Mathf.RoundToInt(shopData.Price * shopData.Discount);
        if (!CurrencyManager.Instance.CanAfford(finalPrice))
        {
            ToastMessage.Show("金币不足！需要 " + finalPrice + " 金币");
            return false;
        }

        CurrencyManager.Instance.Spend(finalPrice);

        DRItem itemData = DataRepository.GetItemByAssetId(itemAssetId);
        if (itemData == null)
        {
            CurrencyManager.Instance.Earn(finalPrice);
            ToastMessage.Show("商品数据错误！");
            return false;
        }

        string uid = InventoryManager.Instance.AddItem(itemData.Id);
        if (uid == null)
        {
            CurrencyManager.Instance.Earn(finalPrice);
            ToastMessage.Show("物品添加失败！");
            return false;
        }

        if (currentStock > 0) ReduceStock(shopKeeperId, itemAssetId);

        ToastMessage.Show("购买成功！获得 " + itemData.Name);
        return true;
    }

    /// <summary>
    /// 出售物品
    /// </summary>
    public bool SellItem(string uid)
    {
        if (string.IsNullOrEmpty(uid)) return false;

        // 1. 找到背包中的物品
        PackageLocalData.PackageLocalItem localItem = GameManager.Instance.GetPackageLocalItemByUid(uid);
        if (localItem == null)
        {
            ToastMessage.Show("物品不存在！");
            return false;
        }

        // 2. 检查是否已装备
        if (localItem.isEquipped)
        {
            ToastMessage.Show("装备中的物品不能出售！");
            return false;
        }

        // 3. 计算出售价格
        int sellPrice = GetSellPrice(localItem.id);

        // 4. 移除物品
        if (!InventoryManager.Instance.RemoveItem(uid))
        {
            ToastMessage.Show("物品移除失败！");
            return false;
        }

        // 5. 增加金币
        CurrencyManager.Instance.Earn(sellPrice);

        DRItem itemData = GameManager.Instance.GetPackageItemById(localItem.id);
        ToastMessage.Show("出售成功！+ " + sellPrice + " 金币");

        return true;
    }

    public List<DRShop> GetShopItems(int shopKeeperId)
        => DataRepository.GetShopItems(shopKeeperId);

    public int GetStock(int keeperId, string itemAssetId)
    {
        var key = (keeperId, itemAssetId);
        if (stockMap.TryGetValue(key, out int remaining))
            return remaining;

        foreach (var s in DataRepository.GetShopItems(keeperId))
        {
            if (s.ItemAssetId == itemAssetId) return s.Stock;
        }
        return 0;
    }

    public int GetSellPrice(int itemID)
    {
        if (DataRepository.ItemTable.TryGetValue(itemID, out var item))
            return Mathf.Max(1, item.Star * 10);
        return 0;
    }

    private void ReduceStock(int keeperId, string itemAssetId)
    {
        var key = (keeperId, itemAssetId);
        if (stockMap.TryGetValue(key, out int remaining))
        {
            stockMap[key] = remaining - 1;
        }
        else
        {
            foreach (var s in DataRepository.GetShopItems(keeperId))
            {
                if (s.ItemAssetId == itemAssetId && s.Stock > 0)
                {
                    stockMap[key] = s.Stock - 1;
                    return;
                }
            }
        }
    }
}
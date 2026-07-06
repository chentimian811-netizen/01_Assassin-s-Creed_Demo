using System.Collections.Generic;
using UnityEngine;

public class ShopManager : MonoBehaviour
{
    // ==================== 单例 ====================
    private static ShopManager _instance;
    public static ShopManager Instance => _instance;

    // ==================== 库存追踪 ====================
    private Dictionary<ShopConfig, Dictionary<int, int>> stockMap
        = new Dictionary<ShopConfig, Dictionary<int, int>>();

    // ==================== 生命周期 ====================
    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
    }

    // ==================== 核心方法 ====================

    /// <summary>
    /// 购买物品
    /// </summary>
    public bool BuyItem(ShopConfig config, int itemID)
    {
        if (config == null) return false;

        // 1. 找到商品数据
        ShopItemData shopItem = GetShopItemData(config, itemID);
        if (shopItem == null)
        {
            ToastMessage.Show("商品不存在！");
            return false;
        }

        // 2. 检查库存
        int currentStock = GetStock(config, itemID);
        if (currentStock == 0)
        {
            ToastMessage.Show("库存不足！");
            return false;
        }

        // 3. 计算折扣后价格
        int finalPrice = GetDiscountedPrice(shopItem);

        // 4. 检查余额
        if (!CurrencyManager.Instance.CanAfford(finalPrice))
        {
            ToastMessage.Show("金币不足！需要 " + finalPrice + " 金币");
            return false;
        }

        // 5. 扣款
        CurrencyManager.Instance.Spend(finalPrice);

        // 6. 添加物品到背包
        string uid = InventoryManager.Instance.AddItem(itemID);
        if (uid == null)
        {
            // 添加失败，退款
            CurrencyManager.Instance.Earn(finalPrice);
            ToastMessage.Show("物品添加失败！");
            return false;
        }

        // 7. 扣减库存
        if (currentStock > 0)
        {
            ReduceStock(config, itemID);
        }

        // 8. 提示成功
        PackageTableItem tableItem = GameManager.Instance.GetPackageItemById(itemID);
        string itemName = tableItem?.name ?? "未知物品";
        ToastMessage.Show("购买成功！获得 " + itemName);

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
        bool removed = InventoryManager.Instance.RemoveItem(uid);
        if (!removed)
        {
            ToastMessage.Show("物品移除失败！");
            return false;
        }

        // 5. 增加金币
        CurrencyManager.Instance.Earn(sellPrice);

        // 6. 提示成功
        PackageTableItem tableItem = GameManager.Instance.GetPackageItemById(localItem.id);
        string itemName = tableItem?.name ?? "未知物品";
        ToastMessage.Show("出售成功！+ " + sellPrice + " 金币");

        return true;
    }

    // ==================== 查询方法 ====================

    /// <summary>
    /// 获取某商店的商品列表（附带实时库存和折扣价）
    /// </summary>
    public List<ShopItemDisplay> GetShopItems(ShopConfig config)
    {
        List<ShopItemDisplay> result = new List<ShopItemDisplay>();
        if (config == null) return result;

        foreach (ShopItemData item in config.Items)
        {
            ShopItemDisplay display = new ShopItemDisplay
            {
                itemData = item,
                currentStock = GetStock(config, item.itemID),
                finalPrice = GetDiscountedPrice(item)
            };
            result.Add(display);
        }
        return result;
    }

    /// <summary>
    /// 根据 ShopConfig 和 itemID 查找商品数据
    /// </summary>
    public ShopItemData GetShopItemData(ShopConfig config, int itemID)
    {
        if (config == null) return null;

        foreach (ShopItemData item in config.Items)
        {
            if (item.itemID == itemID)
                return item;
        }
        return null;
    }

    /// <summary>
    /// 获取商品的实时库存
    /// </summary>
    public int GetStock(ShopConfig config, int itemID)
    {
        ShopItemData shopItem = GetShopItemData(config, itemID);
        if (shopItem == null) return 0;

        // 无限库存
        if (shopItem.stock == -1) return -1;

        // 检查运行时消耗后的库存
        if (stockMap.TryGetValue(config, out var shopStock))
        {
            if (shopStock.TryGetValue(itemID, out int remaining))
                return remaining;
        }

        // 还没消耗过，返回配置值
        return shopItem.stock;
    }

    /// <summary>
    /// 计算折扣后价格
    /// </summary>
    public int GetDiscountedPrice(ShopItemData item)
    {
        if (item == null) return 0;
        return Mathf.RoundToInt(item.price * item.discount);
    }

    /// <summary>
    /// 计算出售价格（星级 x 10）
    /// </summary>
    public int GetSellPrice(int itemID)
    {
        PackageTableItem tableItem = GameManager.Instance.GetPackageItemById(itemID);
        if (tableItem == null) return 0;

        return Mathf.Max(1, tableItem.star * 10);
    }

    // ==================== 内部方法 ====================

    /// <summary>
    /// 扣减库存
    /// </summary>
    private void ReduceStock(ShopConfig config, int itemID)
    {
        if (!stockMap.ContainsKey(config))
        {
            stockMap[config] = new Dictionary<int, int>();
        }

        var shopStock = stockMap[config];

        if (shopStock.TryGetValue(itemID, out int remaining))
        {
            shopStock[itemID] = remaining - 1;
        }
        else
        {
            ShopItemData shopItem = GetShopItemData(config, itemID);
            if (shopItem != null && shopItem.stock > 0)
            {
                shopStock[itemID] = shopItem.stock - 1;
            }
        }
    }
}

/// <summary>
/// 商店商品展示数据（逻辑层 → UI 层的桥梁）
/// </summary>
[System.Serializable]
public class ShopItemDisplay
{
    public ShopItemData itemData;
    public int currentStock;
    public int finalPrice;
    public string uid;//出售模式下使用 标识背包的里面的具体物品
}

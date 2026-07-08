using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class DataRepository
{
    public static Dictionary<int,DRItem> ItemTable{get;private set;}
    public static Dictionary<int,List<DRShop>> ShopByKeeper{get;private set;}

    public static Dictionary<int,DRShop> ShopTable {get;private set;}
    public static Dictionary<int,ItemConfig_SO> ItemConfigCache{get;private set;}
    public static Dictionary<string, DRItem> ItemByAssetId { get; private set; }

    public static void Initialize()
    {
        var itemText = Resources.Load<TextAsset>("DataTables/Item");
        ItemTable = ParseItemTable(itemText.text);

        var shopText = Resources.Load<TextAsset>("DataTables/Shop");
        (ShopTable, ShopByKeeper) = ParseShopTable(shopText.text);

        ItemConfigCache = new Dictionary<int, ItemConfig_SO>();
        ItemByAssetId = new Dictionary<string, DRItem>();
        foreach (var kv in ItemTable)
        {
            ItemByAssetId[kv.Value.AssetId] = kv.Value;
            var so = Resources.Load<ItemConfig_SO>($"ItemConfigs/{kv.Value.AssetId}");
            if (so != null)
            {
                ItemConfigCache[kv.Key] = so;
            }
        }
        Debug.Log($"[DataRepository] 加载完成：{ItemTable.Count} items, {ShopTable.Count} shop rows");
    }

    public static ItemConfig_SO GetItemConfig(int itemId)
        => ItemConfigCache.TryGetValue(itemId, out var so) ? so : null;

    public static ItemConfig_SO GetItemConfig(string assetId)
    {
        var item = GetItemByAssetId(assetId);
        return item != null ? GetItemConfig(item.Id) : null;
    }

    public static DRItem GetItemByAssetId(string assetId)
        => ItemByAssetId.TryGetValue(assetId, out var item) ? item : null;

    public static List<DRShop> GetShopItems(int shopKeeperId)
        => ShopByKeeper.TryGetValue(shopKeeperId, out var list) ? list : new List<DRShop>();
    private static Dictionary<int,DRItem> ParseItemTable(string text)
    {
        var rows = TSVParser.Parse(text);
        var dict = new Dictionary<int,DRItem>();
        foreach(var cols in rows)
        {
            var item = new DRItem
            {
                Id = int.Parse(cols[0]),
                Type = int.Parse(cols[1]),
                Star = int.Parse(cols[2]),
                Name = cols[3],
                Description = cols[4],
                SkillDescription = cols[5],
                AssetId = cols[6]

            };
            dict[item.Id] = item;
        }
        return dict;
    }
    private static (Dictionary<int, DRShop> byId, Dictionary<int, List<DRShop>> byKeeper) ParseShopTable(string text)
    {
        var rows = TSVParser.Parse(text);
        var byId = new Dictionary<int, DRShop>();
        var byKeeper = new Dictionary<int, List<DRShop>>();
        foreach (var cols in rows)
        {
            var shop = new DRShop
            {
                Id = int.Parse(cols[0]),
                ShopKeeperId = int.Parse(cols[1]),
                ItemAssetId = cols[2],
                Price = int.Parse(cols[3]),
                Stock = int.Parse(cols[4]),
                Discount = float.Parse(cols[5])
            };
            byId[shop.Id] = shop;

            if (!byKeeper.ContainsKey(shop.ShopKeeperId))
                byKeeper[shop.ShopKeeperId] = new List<DRShop>();
            byKeeper[shop.ShopKeeperId].Add(shop);
        }
        return (byId, byKeeper);
    }
}

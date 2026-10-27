using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static PackageLocalData;

public class GameManager : MonoBehaviour
{
    private static GameManager _instance;

    public E_SortMode CurrentSorMode{get;set;}=E_SortMode.Default;

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
        DontDestroyOnLoad(gameObject);
        DataRepository.Initialize();
    }

    public static GameManager Instance => _instance;

    private void OnDestroy()
    {
        if (_instance == this) _instance = null;
    }

    public void DeletePackageItem(List<string> uids)
    {
        foreach (string uid in uids)
            DeletePackageItem(uid, false);
        PackageLocalData.Instance.SavePackage();
    }

    private void DeletePackageItem(string uid, bool needSave = true)
    {
        PackageLocalItem packageLocalItem = GetPackageLocalItemByUid(uid);
        if (packageLocalItem == null) return;
        PackageLocalData.Instance.items.Remove(packageLocalItem);
        if (needSave) PackageLocalData.Instance.SavePackage();
    }

    public List<DRItem> GetPackageDataByType(int type)
    {
        List<DRItem> result = new List<DRItem>();
        foreach (var item in DataRepository.ItemTable.Values)
        {
            if (item.Type == type) result.Add(item);
        }
        return result;
    }

    public bool TryGetLotteryRandom1(int cost, out PackageLocalItem result)
    {
        result = null;
        if(cost >0 && !CurrencyManager.Instance.CanAfford(cost))return false;

        if(cost > 0)
        {
            CurrencyManager.Instance.Spend(cost);
        }
        List<DRItem> packageItems = GetPackageDataByType(GameConst.PackageTypeWeapon);
        int index = Random.Range(0, packageItems.Count);
        DRItem packageItem = packageItems[index];
        result = new PackageLocalItem
        {
            uid = System.Guid.NewGuid().ToString(),
            id = packageItem.Id,
            num = 1,
            level = 1,
            isNew = CheckWeaponIsNew(packageItem.Id)
        };
        PackageLocalData.Instance.items.Add(result);
        PackageLocalData.Instance.SavePackage();
        return true;
    }

    public bool TryGetLotteryRandom10(int costPerPull, out List<PackageLocalItem> results)
    {
        results = new List<PackageLocalItem>();
        int totalCost = costPerPull * 10;
        if(totalCost >0 && !CurrencyManager.Instance.CanAfford(totalCost)) return false;

        if(totalCost > 0)
        {
            CurrencyManager.Instance.Spend(totalCost);
        }

        for(int i = 0; i < 10; i++)
        {
            TryGetLotteryRandom1(0,out var item);
            results.Add(item);
        }
        results.Sort(new PackageItemComparer{ sorMode = E_SortMode.Default});
        return true;
    }

    public bool CheckWeaponIsNew(int id)
    {
        foreach (PackageLocalItem item in GetPackageLocalData())
        {
            if (item.id == id) return false;
        }
        return true;
    }

    public List<PackageLocalItem> GetPackageLocalData()
    => PackageLocalData.Instance.LoadPackage();

    public DRItem GetPackageItemById(int id)
    {
        if (DataRepository.ItemTable == null) return null;
        DataRepository.ItemTable.TryGetValue(id, out var item);
        return item;
    }

    public PackageLocalItem GetPackageLocalItemByUid(string uid)
    {
        if (string.IsNullOrEmpty(uid)) return null;
        List<PackageLocalItem> list = GetPackageLocalData();
        if (list == null) return null;
        foreach (PackageLocalItem item in list)
        {
            if (item != null && item.uid == uid) return item;
        }
        return null;
    }

    public List<PackageLocalItem> GetSortPackageLocalData()
    {
        List<PackageLocalItem> localItems = PackageLocalData.Instance.LoadPackage();
        localItems.Sort(new PackageItemComparer(){sorMode = CurrentSorMode});
        return localItems;
    }
}

public class GameConst
{
    public const int PackageTypeWeapon = 1;
    public const int PackageTypeFood = 2;
    /// <summary>盾（动作集阶段 1.5 / 步骤 5 约定：Item.txt Type=3）</summary>
    public const int PackageTypeShield = 3;
}
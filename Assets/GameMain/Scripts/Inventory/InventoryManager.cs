using System;
using System.Collections.Generic;
using UnityEngine;
using static PackageLocalData;

public class InventoryManager : MonoBehaviour
{
    private static InventoryManager _instance;
    public static InventoryManager Instance => _instance;

    WeaponManager weaponManager;
    public event Action<PackageLocalItem> OnItemAdded;
    public event Action<PackageLocalItem> OnItemRemoved;
    public event Action<PackageLocalItem> OnItemEquipped;
    public event Action<PackageLocalItem> OnItemUnequipped;
    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        else
        {
            _instance = this;
        }
    }

    void Start()
    {
        weaponManager = FindFirstObjectByType<WeaponManager>();
        if(weaponManager == null)
        {
            Debug.LogError("WeaponManager not found in the scene.");
        }
        RestoreEquippedState();

        // 仅「背包从未有内容」时自动发默认近战。
        // 玩家主动卸光后重进 Play：背包里仍有物品 → 不得回装（否则表现为“卸了又有了”）。
        if (GetEquippedWeapon() == null && ShouldAutoEquipDefault())
        {
            EquipDefaultMeleeWeapon();
        }
    }

    /// <summary>是否允许自动装备默认近战：仅首次（本地背包为空）时</summary>
    bool ShouldAutoEquipDefault()
    {
        List<PackageLocalItem> items = PackageLocalData.Instance != null
            ? PackageLocalData.Instance.LoadPackage()
            : null;
        return items == null || items.Count == 0;
    }

    /// <summary>
    /// 自动装备背包中第一把近战武器作为初始武器
    /// </summary>
    void EquipDefaultMeleeWeapon()
    {
        var allWeapons = GameManager.Instance.GetPackageDataByType(GameConst.PackageTypeWeapon);
        // 盾（Type=3）与武器同走装备通道，初始化/商店列表需一并纳入
        var allShields = GameManager.Instance.GetPackageDataByType(GameConst.PackageTypeShield);
        if (allShields != null && allShields.Count > 0)
        {
            if (allWeapons == null) allWeapons = new System.Collections.Generic.List<DRItem>();
            allWeapons.AddRange(allShields);
        }
        foreach (var tableItem in allWeapons)
        {
            WeaponConfig config = weaponManager.GetWeaponConfig(tableItem.Id);
            if (config != null && !config.isRanged)
            {
                EquipFromGround(tableItem.Id);
                Debug.Log($"初始装备默认近战武器: {tableItem.Name}");
                return;
            }
        }
    }
    public string AddItem(int itemId, int count = 1)
    {
        var tableItem = GameManager.Instance.GetPackageItemById(itemId); 
        if(tableItem == null) return null;
        PackageLocalItem item = new PackageLocalItem
        {
            uid = Guid.NewGuid().ToString(),
            id = itemId,
            num = count,
            level = 1,
            isNew = true,
            isEquipped = false
        };
        PackageLocalData.Instance.items.Add(item);
        PackageLocalData.Instance.SavePackage();
        OnItemAdded?.Invoke(item);
        return item.uid;
    }
    public bool RemoveItem(string uid)
    {
        PackageLocalItem item = GameManager.Instance.GetPackageLocalItemByUid(uid);
        if (item == null) return false;
        if (item.isEquipped) return false;

        PackageLocalData.Instance.items.Remove(item);
        PackageLocalData.Instance.SavePackage();
        OnItemRemoved?.Invoke(item);
        return true;
    }
    /// <summary>从本地背包按 uid 找物品（不依赖 GameManager，防单例未就绪时 NRE）</summary>
    static PackageLocalItem FindLocalItem(string uid)
    {
        if (string.IsNullOrEmpty(uid)) return null;
        List<PackageLocalItem> list = PackageLocalData.Instance != null
            ? PackageLocalData.Instance.LoadPackage()
            : null;
        if (list == null) return null;
        foreach (PackageLocalItem it in list)
        {
            if (it != null && it.uid == uid) return it;
        }
        return null;
    }

    /// <summary>查表（DataRepository 未初始化时返回 null，不抛）</summary>
    static DRItem FindTableItem(int id)
    {
        if (DataRepository.ItemTable == null)
        {
            // GameManager 未 Awake 时 ItemTable 不会被初始化，这里兜底一次
            try { DataRepository.Initialize(); }
            catch (Exception e) { Debug.LogWarning($"[InventoryManager] DataRepository.Initialize 失败: {e.Message}"); }
        }
        if (GameManager.Instance != null)
        {
            var t = GameManager.Instance.GetPackageItemById(id);
            if (t != null) return t;
        }
        if (DataRepository.ItemTable != null
            && DataRepository.ItemTable.TryGetValue(id, out var item))
            return item;
        return null;
    }

    public bool EquipWeapon(string uid)
    {
        PackageLocalItem item = FindLocalItem(uid);
        if (item == null)
        {
            Debug.LogWarning($"[InventoryManager] 背包中找不到 uid={uid}，无法装备");
            return false;
        }

        if (weaponManager == null)
            weaponManager = FindFirstObjectByType<WeaponManager>();
        if (weaponManager == null)
        {
            Debug.LogWarning("[InventoryManager] 场景中无 WeaponManager，无法装备");
            return false;
        }

        var tableItem = FindTableItem(item.id);

        // 武器或盾均可装备（盾走 isShield / 表 Type=Shield=3）
        WeaponConfig wCfg = weaponManager.GetWeaponConfig(item.id);
        bool isWeapon = tableItem != null && tableItem.Type == GameConst.PackageTypeWeapon;
        bool isShield = wCfg != null && wCfg.isShield;
        if (!isWeapon && !isShield)
        {
            Debug.LogWarning($"[InventoryManager] 物品 {item.id} 不是武器/盾（tableType={(tableItem != null ? tableItem.Type.ToString() : "null")} isShield={(wCfg != null && wCfg.isShield)}）");
            return false;
        }

        // 双持：不再 UnequipAll 清空两手；同槽替换与 EnsureNotDualEquipped 在 WeaponManager 内处理
        bool success = weaponManager.EquipWeapon(uid);
        if (!success) return false;

        SyncEquippedFlags(item);

        int slotIndex = weaponManager.GetSlotIndexByUid(uid);
        if (slotIndex >= 0)
        {
            weaponManager.SwitchToSlot(slotIndex);
        }

        item.isEquipped = true;
        PackageLocalData.Instance.SavePackage();
        OnItemEquipped?.Invoke(item);
        return true;

    }

    /// <summary>双持下同步 isEquipped：仍占槽的保持，已被顶掉的清除</summary>
    void SyncEquippedFlags(PackageLocalItem newItem)
    {
        if (weaponManager == null) return;

        foreach (PackageLocalItem it in PackageLocalData.Instance.items)
        {
            if (it == null || !it.isEquipped) continue;
            if (newItem != null && it.uid == newItem.uid) continue;
            if (weaponManager.GetSlotIndexByUid(it.uid) < 0)
            {
                it.isEquipped = false;
                OnItemUnequipped?.Invoke(it);
            }
        }
    }

    private void UnequipAll()
    {
        if(weaponManager == null) return;
        
        List<PackageLocalItem> toUnequip = new List<PackageLocalItem>();
        foreach(PackageLocalItem oldItem in PackageLocalData.Instance.items)
        {
            if (oldItem.isEquipped)
            {
                toUnequip.Add(oldItem);
            }
        }

        foreach(var oldItem in toUnequip)
        {
            WeaponConfig oldConfig = weaponManager.GetWeaponConfig(oldItem.id);
            if(oldConfig != null)
            {
                var oldType = DataRepository.ItemTable.TryGetValue(oldConfig.weaponID, out var cfgItem) 
                    ? cfgItem.WeaponType: E_WeaponType.Sword;

                weaponManager.UnequipSlotByType(oldType);
            }

            oldItem.isEquipped = false;
            OnItemUnequipped?.Invoke(oldItem);
        }
    }


    public bool EquipFromGround(int weaponId)
    {
        string uid = AddItem(weaponId);

        if (uid == null) return false;

        return EquipWeapon(uid);
    }

    public string AddToBag(int weaponId)
    {
        return AddItem(weaponId);

    }

    public bool Unequip(int slotIndex = 0)
    {
        PackageLocalItem equipped = GetEquippedWeapon();
        if (equipped == null) return false;
        return UnequipItem(equipped.uid);
    }

    /// <summary>按 uid 卸下（双持下卸盾不会误卸剑）</summary>
    public bool UnequipItem(string uid)
    {
        if (weaponManager == null)
            weaponManager = FindFirstObjectByType<WeaponManager>();
        if (weaponManager == null) return false;

        PackageLocalItem item = FindLocalItem(uid);
        if (item == null || !item.isEquipped) return false;

        int slotIndex = weaponManager.GetSlotIndexByUid(uid);
        if (slotIndex >= 0)
        {
            weaponManager.UnequipSlot(slotIndex);
        }
        else
        {
            // 槽位里没有时按类型兜底卸
            WeaponConfig config = weaponManager.GetWeaponConfig(item.id);
            if (config != null)
            {
                var eqType = config.isShield
                    ? E_WeaponType.Shield
                    : (DataRepository.ItemTable != null
                        && DataRepository.ItemTable.TryGetValue(config.weaponID, out var eqItem)
                            ? eqItem.WeaponType
                            : E_WeaponType.Sword);
                weaponManager.UnequipSlotByType(eqType);
            }
        }

        item.isEquipped = false;
        PackageLocalData.Instance.SavePackage();
        OnItemUnequipped?.Invoke(item);

        // 卸下后清残留：预置剑/盾外观 + 槽上已无背包记录的模型
        weaponManager.ClearOrphanWeaponModels();
        return true;
    }

    public PackageLocalItem GetEquippedWeapon()
    {
        foreach (PackageLocalItem item in PackageLocalData.Instance.LoadPackage())
        {
            if (item.isEquipped) return item;
        }
        return null;
    }

    void RestoreEquippedState()
    {
        if (weaponManager == null) return;

        // 恢复所有 isEquipped 的槽（双持：剑+盾都要回来）
        foreach (PackageLocalItem item in PackageLocalData.Instance.LoadPackage())
        {
            if (item != null && item.isEquipped)
                weaponManager.EquipWeapon(item.uid);
        }
    }
}


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

        // 初次游玩无装备时，自动装备默认近战武器
        if (GetEquippedWeapon() == null)
        {
            EquipDefaultMeleeWeapon();
        }
    }

    /// <summary>
    /// 自动装备背包中第一把近战武器作为初始武器
    /// </summary>
    void EquipDefaultMeleeWeapon()
    {
        var allWeapons = GameManager.Instance.GetPackageDataByType(GameConst.PackageTypeWeapon);
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
    public bool EquipWeapon(string uid)
    {
        PackageLocalItem item = GameManager.Instance.GetPackageLocalItemByUid(uid);
        if (item == null) return false;

        var tableItem = GameManager.Instance.GetPackageItemById(item.id); 

        if(tableItem == null || tableItem.Type != GameConst.PackageTypeWeapon)return false;
        if(weaponManager == null)return false;

        UnequipAll();

        bool success = weaponManager.EquipWeapon(uid);
        if(!success)return false;

        int slotIndex = weaponManager.GetSlotIndexByUid(uid);
        if(slotIndex >= 0)
        {
            weaponManager.SwitchToSlot(slotIndex);
        }

        item.isEquipped = true;
        PackageLocalData.Instance.SavePackage();
        OnItemEquipped?.Invoke(item);
        return true;

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
                weaponManager.UnequipSlotByType(oldConfig.weaponType);
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
        if (weaponManager == null) return false;

        PackageLocalItem equipped = GetEquippedWeapon();
        if(equipped== null) return false;
        
        WeaponConfig config = weaponManager.GetWeaponConfig(equipped.id);
        if (config != null)
            weaponManager.UnequipSlotByType(config.weaponType);

        equipped.isEquipped = false;
        PackageLocalData.Instance.SavePackage();
        OnItemUnequipped?.Invoke(equipped);
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
        PackageLocalItem equipped = GetEquippedWeapon();
        if (equipped != null && weaponManager != null)
        {
            weaponManager.EquipWeapon(equipped.uid);
        }
    }
}


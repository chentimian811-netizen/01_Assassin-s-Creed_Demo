using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static PackageLocalData;

public class InventoryManager : MonoBehaviour
{
    private static InventoryManager _instance;//单例模式

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
    }

    public string AddItem(int itemId, int count = 1)
    {
        PackageTableItem tableItem = GameManager.Instance.GetPackageItemById(itemId);
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
        
        PackageTableItem tableItem = GameManager.Instance.GetPackageItemById(item.id);
        if(tableItem == null || tableItem.type != GameConst.PackageTypeWeapon)return false;

        if(weaponManager == null)return false;

        // 卸下同类型旧武器（动态获取类型，不硬编码）
        PackageLocalItem oldEquipped = GetEquippedWeapon();
        if (oldEquipped != null)
        {
            string oldUid = weaponManager.GetMainEquippedUid();
            if (oldUid != null)
            {
                weaponManager.UnequipSlotByType(E_WeaponType.Sword);
            }
            oldEquipped.isEquipped = false;
        }

        bool success = weaponManager.EquipWeapon(uid);
        if(!success)return false;

        item.isEquipped = true;
        PackageLocalData.Instance.SavePackage();

        if (oldEquipped != null)OnItemUnequipped?.Invoke(oldEquipped);
        OnItemEquipped?.Invoke(item);

        return true;

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
        if (equipped == null) return false;

        weaponManager.UnequipSlot(slotIndex);
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

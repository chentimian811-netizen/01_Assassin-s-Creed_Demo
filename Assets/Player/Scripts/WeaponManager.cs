using System;
using System.Collections.Generic;
using UnityEngine;
using static PackageLocalData;

public class WeaponManager : MonoBehaviour
{
    [Header("配置")]
    [SerializeField] string weaponConfigPath = "WeaponConfigs";
    [SerializeField] WeaponSlot[] weaponSlots = new WeaponSlot[0];
    [SerializeField] int mainWeaponSlotIndex = 0;

    Dictionary<int, WeaponConfig> weaponConfigMap;
    MeeleFighter meeleFighter;

    public event Action<WeaponConfig> OnWeaponModelChanged;

    void Awake()
    {
        weaponConfigMap = new Dictionary<int, WeaponConfig>();
        WeaponConfig[] configs = Resources.LoadAll<WeaponConfig>(weaponConfigPath);
        if (configs != null)
        {
            foreach (var config in configs)
            {
                if (config != null)
                    weaponConfigMap[config.weaponID] = config; 
            }
        }
        meeleFighter = GetComponent<MeeleFighter>();
        HidePreplacedWeapons();
    }

    void HidePreplacedWeapons()
    {
        foreach (var slot in weaponSlots)
        {
            if (slot.holdPoint == null) continue;
            foreach (Transform child in slot.holdPoint)
            {
                if (child.name == "Sword")
                {
                    child.gameObject.SetActive(false);
                    break;
                }
            }
        }
    }

    public bool EquipWeapon(string uid)
    {
        PackageLocalItem item = GameManager.Instance.GetPackageLocalItemByUid(uid);
        if (item == null) return false;

        WeaponConfig config;

        if (!weaponConfigMap.TryGetValue(item.id, out config)) return false;

        WeaponSlot targetSlot = FindSlotForWeapon(config.weaponType);
        if (targetSlot == null) return false;

        if (config.weaponType != targetSlot.allowedType) return false;

        if (targetSlot.currentModel != null)
            Destroy(targetSlot.currentModel);

        targetSlot.currentConfig = config;
        targetSlot.equippedUid = uid;

        if (config.weaponPrefab != null)
        {
            targetSlot.currentModel = Instantiate(config.weaponPrefab, targetSlot.holdPoint);
            SetLayerRecursive(targetSlot.currentModel, gameObject.layer);
        }

        SyncFighterWeapon();
        OnWeaponModelChanged?.Invoke(config);
        return true;
    }

    public string UnequipSlot(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= weaponSlots.Length) return null;

        return UnequipSlotInternal(weaponSlots[slotIndex]);
    }

    public string UnequipSlotByType(E_WeaponType type)
    {
        foreach (var slot in weaponSlots)
        {
            if (slot.allowedType == type && slot.currentConfig != null)
                return UnequipSlotInternal(slot);
        }
        return null;
    }

    string UnequipSlotInternal(WeaponSlot slot)
    {
        if (slot == null || slot.currentConfig == null) return null;

        string uid = slot.equippedUid;
        WeaponConfig oldConfig = slot.currentConfig;

        if (slot.currentModel != null)
        {
            Destroy(slot.currentModel);
            slot.currentModel = null;
        }

        slot.currentConfig = null;
        slot.equippedUid = null;

        SyncFighterWeapon();
        OnWeaponModelChanged?.Invoke(oldConfig);
        return uid;
    }

    public string GetEquippedUid(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= weaponSlots.Length) return null;
        return weaponSlots[slotIndex].equippedUid;
    }

    public string GetMainEquippedUid()
    {
        return GetEquippedUid(mainWeaponSlotIndex);
    }

    WeaponSlot FindSlotForWeapon(E_WeaponType type)
    {
        // 先找空槽位
        foreach (var slot in weaponSlots)
        {
            if (slot.allowedType == type && slot.currentConfig == null)
                return slot;
        }
        // 找同类型已占用槽位替换
        foreach (var slot in weaponSlots)
        {
            if (slot.allowedType == type)
                return slot;
        }
        return null;
    }

    void SyncFighterWeapon()
    {
        if (mainWeaponSlotIndex < 0 || mainWeaponSlotIndex >= weaponSlots.Length)
            return;

        WeaponSlot mainSlot = weaponSlots[mainWeaponSlotIndex];
        if (meeleFighter != null)
            meeleFighter.SetWeapon(mainSlot.currentModel);
    }

    void SetLayerRecursive(GameObject obj, int layer)
    {
        obj.layer = layer;
        foreach (Transform child in obj.transform)
            SetLayerRecursive(child.gameObject, layer);
    }
}
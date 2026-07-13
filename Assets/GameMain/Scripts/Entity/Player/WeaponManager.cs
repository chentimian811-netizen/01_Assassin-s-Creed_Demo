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
    MeleeFighter meleeFighter;

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
        meleeFighter = GetComponent<MeleeFighter>();
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

            // 防御性清理：销毁武器模型上可能残留的 WeaponPickup 组件
            // 防止拾取脚本在装备到玩家身上后仍然响应触发器事件，导致错误弹出拾取UI
            WeaponPickup residualPickup = targetSlot.currentModel.GetComponent<WeaponPickup>();
            if (residualPickup != null)
            {
                Destroy(residualPickup);
            }

            SetLayerRecursive(targetSlot.currentModel, gameObject.layer);
        }

        SyncFighterWeapon();
        meleeFighter?.SetWeaponConfig(config);
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
        meleeFighter?.SetWeaponConfig(null);
        OnWeaponModelChanged?.Invoke(oldConfig);
        return uid;
    }

    public string GetEquippedUid(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= weaponSlots.Length) return null;
        return weaponSlots[slotIndex].equippedUid;
    }

    //根据武器的Id获取武器配置
    public WeaponConfig GetWeaponConfig(int weaponId)
    {
        WeaponConfig config;
        weaponConfigMap.TryGetValue(weaponId,out config);
        return config;
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
        if (meleeFighter != null)
            meleeFighter.SetWeapon(mainSlot.currentModel);
    }

    void SetLayerRecursive(GameObject obj, int layer)
    {
        obj.layer = layer;
        foreach (Transform child in obj.transform)
            SetLayerRecursive(child.gameObject, layer);
    }
    /// <summary>
    /// 切换到指定槽位（供 WeaponSwitcher 调用）
    /// </summary>
    public void SwitchToSlot(int slotIndex)
    {
        if(slotIndex < 0 || slotIndex >= weaponSlots.Length)
        {
            Debug.LogWarning($"WeaponManager: 槽位索引无效 [{slotIndex}]");
            return;
        }

        //隐藏所有槽位的武器模型
        foreach(var slot in weaponSlots)
        {
            if(slot.currentModel != null)
            {
                slot.currentModel.SetActive(false);
            }
        }

        //显示目标槽位的武器模型
        WeaponConfig targetConfig = weaponSlots[slotIndex].currentConfig;
        if(targetConfig != null && weaponSlots[slotIndex].currentModel != null)
        {
            weaponSlots[slotIndex].currentModel.SetActive(true);
        }

        //更新 RangedFighter
        RangedFighter rangedFighter = GetComponent<RangedFighter>();
        if(rangedFighter != null)
        {
            if(targetConfig != null && targetConfig.isRanged)
            {
                //装备远程武器
                rangedFighter.SetWeapon(targetConfig);
            }
            else
            {
                //清除远程武器
                rangedFighter.ClearWeapon();
            }
        }

       meleeFighter?.SetWeaponConfig(targetConfig);

        OnWeaponModelChanged?.Invoke(targetConfig);
    }

    /// <summary>
    /// 获取指定槽位的武器配置
    /// </summary>
    public WeaponConfig GetSlotConfig(int slotIndex)
    {
        if(slotIndex < 0 || slotIndex >= weaponSlots.Length)
        {
            return null;
        }
        return weaponSlots[slotIndex].currentConfig;
    }
}
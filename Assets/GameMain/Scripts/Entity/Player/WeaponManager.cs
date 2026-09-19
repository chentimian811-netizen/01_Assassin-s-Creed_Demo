using System;
using System.Collections.Generic;
using UnityEngine;
using static PackageLocalData;

public class WeaponManager : MonoBehaviour
{
    /// <summary>玩家武器命中盒层级：Project Settings 里为 Playehitbox(8)</summary>
    const int PlayeHitboxLayer = 8;

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

        var wType = DataRepository.ItemTable.TryGetValue(config.weaponID, out var wItem) 
        ? wItem.WeaponType : E_WeaponType.Sword;
        
        WeaponSlot targetSlot = FindSlotForWeapon(wType);

        if (targetSlot == null) return false;

        if (wType != targetSlot.allowedType) return false;

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

            // ⚠️ 必须去掉武器上的 Rigidbody：
            // 武器挂在玩家 CharacterController 下。若子级自带 Rigidbody，命中盒会变成独立刚体，
            // Trigger 往往打不到敌人 CharacterController（表现为：剑砍不中，脚/腿反而能中——
            // 因为脚部 SphereCollider 没有 Rigidbody，作为 CC 的子碰撞体可以正常发事件）。
            Rigidbody residualRb = targetSlot.currentModel.GetComponent<Rigidbody>();
            if (residualRb != null)
            {
                Destroy(residualRb);
            }

            // 武器命中盒必须落在 Playehitbox(8)，不能跟角色身体层(Player/Enemy)相同。
            // 否则与自身 CharacterController / 敌友判定搅在一起，命中事件不可靠。
            SetLayerRecursive(targetSlot.currentModel, PlayeHitboxLayer);

            // 命中盒的初始开关状态：武器开着、盾关着。
            // 【为什么盾必须关】盾会挂到左臂上，若碰撞体全程 enabled，
            // 走路时盾身刮到敌人就会触发 OnTriggerEnter —— 表现是"走着走着敌人掉血"。
            // 判定窗口由 MeleeFighter.EnableHitbox / DisableAllHitxboxes 精确开，
            // 那是阶段 4 的 E_AttackHitbox.Shield 分支，在那之前盾不会造成任何伤害。
            ApplyInitialColliderState(targetSlot.currentModel, config);
        }

        SyncFighterWeapon();

        meleeFighter?.SetWeaponID(config.weaponID);
        var localItem = GameManager.Instance.GetPackageLocalItemByUid(uid);
        meleeFighter?.SetUpgradeLevel(localItem?.level ?? 1); 
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
        meleeFighter?.SetWeaponID(-1);
        meleeFighter?.SetUpgradeLevel(1);
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
    /// 装备瞬间统一决定模型上所有碰撞体的初始开关状态。
    ///
    /// 为什么要在这里"统一决定"，而不是各管各的：
    /// 碰撞体默认是开的（Prefab 里如此），于是"盾走路误伤"这类问题取决于
    /// 谁先跑、谁后跑，属于隐式约定。集中成一处之后，规则是显式的、可测的：
    ///   武器 → 开（攻击窗口里 MeleeFighter 会直接操作它，初始状态不该是关的）
    ///   盾   → 关（只允许在盾击判定窗口内开，由后续阶段的 HitboxToUse=Shield 控制）
    /// </summary>
    void ApplyInitialColliderState(GameObject model, WeaponConfig config)
    {
        if (model == null) return;

        bool enable = config == null || !config.isShield;

        var colliders = model.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] != null) colliders[i].enabled = enable;
        }
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
            // 切槽位时同样复位一次碰撞体状态：
            // MeleeFighter.DisableAllHitxboxes 会关掉它认识的那个命中盒，
            // 但盾等其他槽位的碰撞体不归它管，状态会残留
            ApplyInitialColliderState(weaponSlots[slotIndex].currentModel, targetConfig);
        }

        meleeFighter?.SetWeapon(weaponSlots[slotIndex].currentModel);

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

        meleeFighter?.SetWeaponID(targetConfig.weaponID);
        var slotItem = GameManager.Instance.GetPackageLocalItemByUid(weaponSlots[slotIndex].equippedUid);
        meleeFighter?.SetUpgradeLevel(slotItem?.level ?? 1);
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

    public int GetSlotIndexByUid(string uid)
    {
        for (int i = 0; i < weaponSlots.Length; i++)
        {
            if (weaponSlots[i].equippedUid == uid)
                return i;
        }
        return -1;
    }

    public bool HasMeeleWeaponEquipped()
    {
        for(int i = 0 ; i < weaponSlots.Length; i++)
        {
            if(weaponSlots[i].currentConfig!=null&&!weaponSlots[i].currentConfig.isRanged) return true;
        }
        return false;
    }
}
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

    [Header("动作集（步骤 6 路由骨架）")]
    [SerializeField] MovesetConfig movesetUnarmed;
    [SerializeField] MovesetConfig movesetOneHandedSword;
    [SerializeField] MovesetConfig movesetSwordAndShield;

    public MovesetConfig MovesetUnarmed => movesetUnarmed;
    public MovesetConfig MovesetOneHandedSword => movesetOneHandedSword;
    public MovesetConfig MovesetSwordAndShield => movesetSwordAndShield;

    public event Action<WeaponConfig> OnWeaponModelChanged;

    /// <summary>槽位数量（供 WeaponSwitcher 等按索引遍历）</summary>
    public int SlotCount => weaponSlots != null ? weaponSlots.Length : 0;

    void Awake()
    {
        EnsureConfigMap();
        EnsureMovesets();
        meleeFighter = GetComponent<MeleeFighter>();
        HidePreplacedWeapons();
    }

    /// <summary>从 Resources/Movesets 装载三件套（Inspector 未指定时）</summary>
    void EnsureMovesets()
    {
        if (movesetUnarmed == null)
            movesetUnarmed = Resources.Load<MovesetConfig>("Movesets/UnarmedMoveset");
        if (movesetOneHandedSword == null)
            movesetOneHandedSword = Resources.Load<MovesetConfig>("Movesets/OneHandedSwordMoveset");
        if (movesetSwordAndShield == null)
            movesetSwordAndShield = Resources.Load<MovesetConfig>("Movesets/SwordAndShieldMoveset");

        if (movesetUnarmed == null || movesetOneHandedSword == null || movesetSwordAndShield == null)
            Debug.LogWarning("[WeaponManager] Moveset 三件套未找齐（Resources/Movesets/）");
    }

    /// <summary>右手动作集用配置（单手近战；盾/弓不算）</summary>
    public WeaponConfig GetMovesetRightConfig()
    {
        if (weaponSlots == null) return null;
        // 优先 mainWeaponSlotIndex（RightHand），再扫一遍
        if (mainWeaponSlotIndex >= 0 && mainWeaponSlotIndex < weaponSlots.Length)
        {
            var s = weaponSlots[mainWeaponSlotIndex];
            if (s != null && MovesetResolver.IsOneHandedMelee(s.currentConfig))
                return s.currentConfig;
        }
        foreach (var slot in weaponSlots)
        {
            if (slot != null && MovesetResolver.IsOneHandedMelee(slot.currentConfig))
                return slot.currentConfig;
        }
        return null;
    }

    /// <summary>左手动作集用配置（盾）</summary>
    public WeaponConfig GetMovesetLeftConfig()
    {
        if (weaponSlots == null) return null;
        foreach (var slot in weaponSlots)
        {
            if (slot != null && MovesetResolver.IsShield(slot.currentConfig))
                return slot.currentConfig;
        }
        return null;
    }

    /// <summary>确保 weaponConfigMap 已从 Resources/WeaponConfigs 装载（Awake 前/失败后可重试）</summary>
    void EnsureConfigMap()
    {
        if (weaponConfigMap != null && weaponConfigMap.Count > 0) return;

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
        if (weaponConfigMap.Count == 0)
            Debug.LogWarning("[WeaponManager] Resources/WeaponConfigs 下未加载到任何 WeaponConfig");
    }

    void HidePreplacedWeapons()
    {
        if (weaponSlots == null) return;
        foreach (var slot in weaponSlots)
        {
            if (slot == null || slot.holdPoint == null) continue;
            HideWeaponVisualsUnder(slot.holdPoint, slot.currentModel != null ? slot.currentModel.transform : null);
        }
    }

    /// <summary>
    /// 隐藏挂点下的「预置武器外观」（不碰手指等骨骼子节点）。
    /// 为什么不能 SetActive(false) 所有 child：hand_r/hand_l 下是手指骨骼。
    /// </summary>
    void HideWeaponVisualsUnder(Transform root, Transform keep)
    {
        if (root == null) return;
        foreach (Transform child in root)
        {
            if (child == null) continue;
            if (keep != null && (child == keep || child.IsChildOf(keep))) continue;

            if (IsWeaponVisualName(child.name))
            {
                if (child.gameObject.activeSelf)
                    Debug.Log($"[WeaponManager] 隐藏残留/预置武器外观: {child.name}");
                child.gameObject.SetActive(false);
            }
            else
            {
                HideWeaponVisualsUnder(child, keep);
            }
        }
    }

    static bool IsWeaponVisualName(string name)
    {
        if (string.IsNullOrEmpty(name)) return false;
        return name.IndexOf("Sword", System.StringComparison.OrdinalIgnoreCase) >= 0
            || name.IndexOf("Shield", System.StringComparison.OrdinalIgnoreCase) >= 0
            || name.IndexOf("Axe", System.StringComparison.OrdinalIgnoreCase) >= 0
            || name.IndexOf("Bow", System.StringComparison.OrdinalIgnoreCase) >= 0
            || name.IndexOf("SkelMesh", System.StringComparison.OrdinalIgnoreCase) >= 0
            || name.IndexOf("Weapon", System.StringComparison.OrdinalIgnoreCase) >= 0
            || name.IndexOf("Blade", System.StringComparison.OrdinalIgnoreCase) >= 0;
    }

    /// <summary>
    /// 清掉槽上「已无背包装备记录」的残留模型，并隐藏预置外观。
    /// 卸装后手上有剑/盾时必调。
    /// </summary>
    public void ClearOrphanWeaponModels()
    {
        if (weaponSlots == null) return;
        foreach (var slot in weaponSlots)
        {
            if (slot == null) continue;

            if (slot.currentModel != null)
            {
                bool stillEquipped = !string.IsNullOrEmpty(slot.equippedUid)
                    && FindLocalItemSafe(slot.equippedUid) != null
                    && FindLocalItemSafe(slot.equippedUid).isEquipped;
                if (!stillEquipped)
                {
                    Debug.Log($"[WeaponManager] 清理残留装备模型: {slot.currentModel.name} @ {slot.slotName}");
                    Destroy(slot.currentModel);
                    slot.currentModel = null;
                    slot.currentConfig = null;
                    slot.equippedUid = null;
                    RaiseWeaponSlotChanged(Array.IndexOf(weaponSlots, slot));
                }
            }

            HideWeaponVisualsUnder(slot.holdPoint, slot.currentModel != null ? slot.currentModel.transform : null);
        }
        SyncFighterWeapon();
        ApplyMeleeFighterWeaponState();
    }

    /// <summary>装备到自动推断的槽位（旧入口，行为兼容）</summary>
    public bool EquipWeapon(string uid)
    {
        return EquipWeapon(uid, -1);
    }

    /// <summary>
    /// 装备武器/盾。
    /// targetSlotIndex &gt;= 0：目标槽优先，类型不匹配则失败（魂系不自动换手）；
    /// targetSlotIndex &lt; 0：FindSlotForWeapon 兜底（先空槽，再可接受该类型的槽）。
    /// </summary>
    /// <summary>安全取本地物品：GameManager 未就绪时退回 PackageLocalData</summary>
    static PackageLocalItem FindLocalItemSafe(string uid)
    {
        if (string.IsNullOrEmpty(uid)) return null;
        if (GameManager.Instance != null)
        {
            var fromGm = GameManager.Instance.GetPackageLocalItemByUid(uid);
            if (fromGm != null) return fromGm;
        }
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

    public bool EquipWeapon(string uid, int targetSlotIndex)
    {
        PackageLocalItem item = FindLocalItemSafe(uid);
        if (item == null) return false;

        EnsureConfigMap();
        WeaponConfig config;
        if (weaponConfigMap == null || !weaponConfigMap.TryGetValue(item.id, out config)) return false;

        var wType = E_WeaponType.Sword;
        if (DataRepository.ItemTable != null
            && DataRepository.ItemTable.TryGetValue(config.weaponID, out var wItem))
            wType = wItem.WeaponType;

        // isShield 优先于表类型：盾内容可能尚未写入 Item.txt 的 Type=3
        if (config != null && config.isShield)
            wType = E_WeaponType.Shield;

        WeaponSlot targetSlot;
        if (targetSlotIndex >= 0)
        {
            if (targetSlotIndex >= weaponSlots.Length) return false;
            targetSlot = weaponSlots[targetSlotIndex];
            if (targetSlot == null || !targetSlot.Accepts(wType)) return false;
        }
        else
        {
            targetSlot = FindSlotForWeapon(wType);
            if (targetSlot == null) return false;
            if (!targetSlot.Accepts(wType)) return false;
        }

        // 同一件装备不可同时占两手，否则卸一边时另一边 currentModel 变已销毁对象
        EnsureNotDualEquipped(uid, targetSlot);

        if (targetSlot.currentModel != null)
            Destroy(targetSlot.currentModel);

        // 先藏预置/手工外观，再实例化，避免「预置剑 + 装备剑」叠两把
        HideWeaponVisualsUnder(targetSlot.holdPoint, null);

        targetSlot.currentConfig = config;
        targetSlot.equippedUid = uid;

        if (config.weaponPrefab != null)
        {
            targetSlot.currentModel = Instantiate(config.weaponPrefab, targetSlot.holdPoint);

            // 防御性清理：销毁武器模型上可能残留的 WeaponPickup 组件
            WeaponPickup residualPickup = targetSlot.currentModel.GetComponent<WeaponPickup>();
            if (residualPickup != null)
            {
                Destroy(residualPickup);
            }

            // ⚠️ 必须去掉武器上的 Rigidbody：命中盒要作为 CC 子碰撞体才能稳定触发
            Rigidbody residualRb = targetSlot.currentModel.GetComponent<Rigidbody>();
            if (residualRb != null)
            {
                Destroy(residualRb);
            }

            SetLayerRecursive(targetSlot.currentModel, PlayeHitboxLayer);

            // 武器碰撞体开、盾关；盾的开窗留给步骤 10 的 E_AttackHitbox.Shield
            ApplyInitialColliderState(targetSlot.currentModel, config);
        }

        SyncFighterWeapon();
        ApplyMeleeFighterWeaponState();

        // HUD 左下角槽位：装备成功后同步图标（目标槽索引在数组里的位置）
        RaiseWeaponSlotChanged(Array.IndexOf(weaponSlots, targetSlot));

        OnWeaponModelChanged?.Invoke(config);

        // 装备后自动切模式：装弓就掏弓（右键立刻变瞄准），把最后一把弓换掉就收弓回近战。
        // 【为什么要自动切】EquipWeapon 只同步外观，不通知 WeaponSwitcher，
        //   于是出现"手里已经是弓了，右键还在举盾"（实测踩过）。
        //   只在 isRanged / 弓被换掉 这两种明确的边界切，装盾之类不碰玩家当前模式。
        var switcher = GetComponent<WeaponSwitcher>();
        if (switcher != null)
        {
            if (config.isRanged) switcher.SwitchToRanged();
            else if (FindRangedWeaponConfig() == null) switcher.SwitchToMelle();
        }

        return true;
    }

    /// <summary>装备时若该 uid 已在另一手，先卸下另一手</summary>
    void EnsureNotDualEquipped(string uid, WeaponSlot target)
    {
        if (string.IsNullOrEmpty(uid) || weaponSlots == null) return;

        for (int i = 0; i < weaponSlots.Length; i++)
        {
            WeaponSlot slot = weaponSlots[i];
            if (slot == null || slot == target) continue;
            if (slot.equippedUid == uid)
                UnequipSlotInternal(slot);
        }
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
            if (slot.Accepts(type) && slot.currentConfig != null)
                return UnequipSlotInternal(slot);
        }
        return null;
    }

    string UnequipSlotInternal(WeaponSlot slot)
    {
        if (slot == null || slot.currentConfig == null)
        {
            // 即使槽数据为空，也要扫一遍预置外观（卸下后手上仍有模型时）
            if (slot != null && slot.holdPoint != null)
                HideWeaponVisualsUnder(slot.holdPoint, null);
            return null;
        }

        string uid = slot.equippedUid;
        WeaponConfig oldConfig = slot.currentConfig;

        if (slot.currentModel != null)
        {
            Destroy(slot.currentModel);
            slot.currentModel = null;
        }

        slot.currentConfig = null;
        slot.equippedUid = null;

        // 预置/手工挂的 Sword、SkelMesh_RoundShield 等一并藏掉
        HideWeaponVisualsUnder(slot.holdPoint, null);

        // 从剩余槽位重新推导，而不是一律清空 —— 卸盾时右手剑的判定不能被抹掉
        SyncFighterWeapon();
        ApplyMeleeFighterWeaponState();

        // 槽已清空：通知 HUD 把对应槽位变回空槽
        RaiseWeaponSlotChanged(Array.IndexOf(weaponSlots, slot));

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
        EnsureConfigMap();
        if (weaponConfigMap == null) return null;
        WeaponConfig config;
        weaponConfigMap.TryGetValue(weaponId, out config);
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
            if (slot.Accepts(type) && slot.currentConfig == null)
                return slot;
        }
        // 找同类型已占用槽位替换
        foreach (var slot in weaponSlots)
        {
            if (slot.Accepts(type))
                return slot;
        }
        return null;
    }

    /// <summary>找非盾、非远程的近战槽（通常 = RightHand / mainWeaponSlotIndex）</summary>
    WeaponSlot FindMeleeWeaponSlot()
    {
        if (weaponSlots == null) return null;

        if (mainWeaponSlotIndex >= 0 && mainWeaponSlotIndex < weaponSlots.Length)
        {
            WeaponSlot main = weaponSlots[mainWeaponSlotIndex];
            if (main != null && main.currentConfig != null
                && !main.currentConfig.isShield && !main.currentConfig.isRanged)
                return main;
        }

        foreach (var slot in weaponSlots)
        {
            if (slot != null && slot.currentConfig != null
                && !slot.currentConfig.isShield && !slot.currentConfig.isRanged)
                return slot;
        }
        return null;
    }

    WeaponConfig FindMeleeWeaponConfig()
    {
        WeaponSlot slot = FindMeleeWeaponSlot();
        return slot != null ? slot.currentConfig : null;
    }

    /// <summary>
    /// MeleeFighter 的 WeaponCollider 只认近战模型，必须排除盾。
    /// 否则主手槽若被盾占用，EnableHitbox 开的是盾的碰撞体，剑砍不中人。
    /// </summary>
    void SyncFighterWeapon()
    {
        if (meleeFighter == null) return;
        WeaponSlot meleeSlot = FindMeleeWeaponSlot();
        meleeFighter.SetWeapon(meleeSlot != null ? meleeSlot.currentModel : null);
    }

    /// <summary>MeleeFighter 的 weaponID / 等级 / AttackData 跟非盾近战走，装盾时不覆盖</summary>
    void ApplyMeleeFighterWeaponState()
    {
        if (meleeFighter == null) return;

        WeaponSlot meleeSlot = FindMeleeWeaponSlot();
        WeaponConfig meleeCfg = meleeSlot != null ? meleeSlot.currentConfig : null;

        if (meleeCfg != null)
        {
            meleeFighter.SetWeapon(meleeSlot.currentModel);
            meleeFighter.SetWeaponID(meleeCfg.weaponID);
            var localItem = FindLocalItemSafe(meleeSlot.equippedUid);
            meleeFighter.SetUpgradeLevel(localItem?.level ?? 1);
            meleeFighter.SetWeaponConfig(meleeCfg);
            Debug.Log($"[WeaponManager] 动作集={MovesetResolver.CurrentType} 近战={meleeCfg.weaponName} 左手盾={(GetMovesetLeftConfig() != null)}");
            return;
        }

        // 无近战：清判定；若仅剩远程，仍把远程配置交给动画/战斗通道（兼容旧切枪）
        meleeFighter.SetWeapon(null);
        WeaponConfig rangedCfg = FindRangedWeaponConfig();
        if (rangedCfg != null)
        {
            meleeFighter.SetWeaponID(rangedCfg.weaponID);
            meleeFighter.SetUpgradeLevel(1);
            meleeFighter.SetWeaponConfig(rangedCfg);
            Debug.Log($"[WeaponManager] 动作集={MovesetResolver.CurrentType} 远程={rangedCfg.weaponName}");
        }
        else
        {
            meleeFighter.SetWeaponID(-1);
            meleeFighter.SetUpgradeLevel(1);
            meleeFighter.SetWeaponConfig(null);
            Debug.Log($"[WeaponManager] 动作集={MovesetResolver.CurrentType} 空手");
        }
    }

    WeaponConfig FindRangedWeaponConfig()
    {
        if (weaponSlots == null) return null;
        foreach (var slot in weaponSlots)
        {
            if (slot != null && slot.currentConfig != null && slot.currentConfig.isRanged)
                return slot.currentConfig;
        }
        return null;
    }

    /// <summary>
    /// 取左手盾槽上盾牌模型的 BoxCollider，供 MeleeFighter 的盾击窗口开合。
    ///
    /// 盾不走 FindMeleeWeaponSlot（那是给剑用的，明确排除了盾），
    /// 所以要单独开这个出口；装备时 ApplyInitialColliderState 会把盾的碰撞体关掉，
    /// 只有盾击招式窗口才会重新打开它。
    /// </summary>
    public BoxCollider GetShieldHitbox()
    {
        if (weaponSlots == null) return null;

        foreach (var slot in weaponSlots)
        {
            if (slot == null || slot.currentConfig == null || !slot.currentConfig.isShield) continue;
            if (slot.currentModel == null) continue;

            BoxCollider box = slot.currentModel.GetComponentInChildren<BoxCollider>(true);
            if (box != null) return box;
        }
        return null;
    }

    void SetLayerRecursive(GameObject obj, int layer)
    {
        obj.layer = layer;
        foreach (Transform child in obj.transform)
            SetLayerRecursive(child.gameObject, layer);
    }

    /// <summary>
    /// 装备瞬间统一决定模型上所有碰撞体的初始开关状态。
    /// 武器 → 开；盾 → 关（只允许盾击判定窗口内开）。
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
    /// 收起远程武器，恢复"近战 + 盾"的持械状态（按键 1 / 装回近战武器时）。
    ///
    /// 与 `SwitchToSlot` 的区别：这个**不看槽位**，只看"谁是不是远程"。
    /// 原因：右手槽同时允许 Sword 与 Bow，弓可能就顶替了右手剑；
    /// 这时 `SwitchToSlot(meleeSlotIndex)` 切到的仍然可能是弓，盾不会回来（实测踩过）。
    /// </summary>
    public void ShowMeleeLoadout()
    {
        for (int i = 0; i < weaponSlots.Length; i++)
        {
            WeaponSlot slot = weaponSlots[i];
            if (slot == null || slot.currentModel == null) continue;

            WeaponConfig cfg = slot.currentConfig;
            bool slotRanged = cfg != null && cfg.isRanged;
            bool show = !slotRanged;

            slot.currentModel.SetActive(show);
            if (show)
                ApplyInitialColliderState(slot.currentModel, cfg);
        }

        SyncFighterWeapon();
        ApplyMeleeFighterWeaponState();

        RangedFighter rangedFighter = GetComponent<RangedFighter>();
        if (rangedFighter != null) rangedFighter.ClearWeapon();

        OnWeaponModelChanged?.Invoke(FindMeleeWeaponConfig());
    }

    /// <summary>
    /// 切换到指定槽位（供 WeaponSwitcher 调用）。
    ///
    /// 【可见性规则（对齐魂系/ER）】
    ///   - 近战模式：右手武器 + 左手盾同时显示（双持）；
    ///   - 远程模式：**只有弓在手上**，另一手的内容一律收起 —— 弓是双手武器，
    ///     "左手举盾、右手拉弓"这种姿态现实中不存在（原来的实现里盾会一直显示，就是这个 bug）。
    ///   收起只是 `SetActive(false)`，槽位数据与背包不受影响，切回近战会自动重新显示。
    ///   MeleeFighter 始终绑非盾近战，避免盾配置污染连招与命中盒。
    /// </summary>
    public void SwitchToSlot(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= weaponSlots.Length)
        {
            Debug.LogWarning($"WeaponManager: 槽位索引无效 [{slotIndex}]");
            return;
        }

        WeaponSlot target = weaponSlots[slotIndex];
        WeaponConfig targetConfig = target != null ? target.currentConfig : null;
        bool activateRanged = targetConfig != null && targetConfig.isRanged;

        for (int i = 0; i < weaponSlots.Length; i++)
        {
            WeaponSlot slot = weaponSlots[i];
            if (slot == null || slot.currentModel == null) continue;

            WeaponConfig cfg = slot.currentConfig;
            bool slotRanged = cfg != null && cfg.isRanged;

            // 远程模式 = 双手持弓：非远程槽（盾/近战）全部收起；
            // 近战模式 = 远程槽收起，其余双持显示。
            bool show = activateRanged ? (slotRanged && i == slotIndex) : !slotRanged;

            slot.currentModel.SetActive(show);
            if (show)
                ApplyInitialColliderState(slot.currentModel, cfg);
        }

        SyncFighterWeapon();
        ApplyMeleeFighterWeaponState();

        RangedFighter rangedFighter = GetComponent<RangedFighter>();
        if (rangedFighter != null)
        {
            if (activateRanged)
                rangedFighter.SetWeapon(targetConfig);
            else
                rangedFighter.ClearWeapon();
        }

        OnWeaponModelChanged?.Invoke(targetConfig != null ? targetConfig : FindMeleeWeaponConfig());
    }

    /// <summary>
    /// 获取指定槽位的武器配置
    /// </summary>
    public WeaponConfig GetSlotConfig(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= weaponSlots.Length)
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

    /// <summary>是否装备了可用于近战的武器（盾不算）</summary>
    public bool HasMeeleWeaponEquipped()
    {
        return FindMeleeWeaponSlot() != null;
    }

    /// <summary>
    /// 把某个槽的当前装备广播给 HUD（左下角槽位图标）。
    /// 槽为空时 weaponId 传 -1，HUD 会显示空槽而不是保留旧图标。
    /// </summary>
    void RaiseWeaponSlotChanged(int slotIndex)
    {
        if (weaponSlots == null || slotIndex < 0 || slotIndex >= weaponSlots.Length) return;

        WeaponSlot slot = weaponSlots[slotIndex];
        int weaponId = slot != null && slot.currentConfig != null ? slot.currentConfig.weaponID : -1;
        GameEvents.RaisePlayerWeaponSlotChanged(slotIndex, weaponId);
    }
}

using System;
using UnityEngine;


/// <summary>
/// 武器切换器 - 处理数字键1/2切换近战和远程武器
/// 挂载在玩家GameObject上
/// </summary>
public class WeaponSwitcher : MonoBehaviour
{
    [Header("槽位配置")]
    [Tooltip("近战武器槽位索引")]
    [SerializeField] private int meleeSlotIndex = 0;

    [Tooltip("远程武器槽位索引")]
    [SerializeField] private int rangeSlotIndex = 1;

    private WeaponManager weaponManager;
    private RangedFighter rangeFighter;

    // 近战/远程模式只切"能不能出招"，**不再禁用 MeleeFighter**：
    // 玩家的受击回调（MeleeFighter.OnTriggerEnter / HandleUnitDamaged）就挂在它身上，
    // 一禁用就变成"拿弓时敌人打不掉我血、也不播受击"（实测隐患）。
    // 远程模式下不出近战攻击由 PlayerCombat 的 IsUsingRanged 闸门负责。

    //当前状态
    private bool isUsingRanged = false;

    public event Action<bool> OnWeaponTypeChanged;//true = 远程 false = 进程。

    public bool IsUsingRanged => isUsingRanged;

    private void Awake()
    {
        weaponManager = GetComponent<WeaponManager>();
        rangeFighter = GetComponent<RangedFighter>();
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Alpha1))
        {
            SwitchToMelle();
        }
        else if (Input.GetKeyDown(KeyCode.Alpha2))
        {
            SwitchToRanged();
        }
    }

    /// <summary>
    /// 收起弓、回到近战/盾的持械状态（按键 1）。
    ///
    /// ⚠️ 不能写成 `SwitchToSlot(meleeSlotIndex)`：两槽模型里右手槽同时允许 Sword 和 Bow，
    /// 弓很可能就顶替了右手剑 —— 这时切到 slot0 依然是弓，`activateRanged` 仍为 true，
    /// 表现就是"按 1 没反应、盾也不回来"（实测踩过）。
    /// 所以这里要的是"把远程收起来"，而不是"切到某个槽"。
    /// </summary>
    public void SwitchToMelle()
    {
        if(!isUsingRanged) return;
        isUsingRanged = false;

        if(rangeFighter != null)
        {
            rangeFighter.enabled = false;
        }

        // 右手槽若确实装的是近战武器 → 正常切回该槽（会顺带刷碰撞体与模型）；
        // 若右手槽已被弓顶替（两槽模型的常见情况）→ 只把远程收起来，别切槽。
        WeaponConfig meleeCfg = weaponManager != null ? weaponManager.GetSlotConfig(meleeSlotIndex) : null;
        if (meleeCfg != null && !meleeCfg.isRanged)
            weaponManager.SwitchToSlot(meleeSlotIndex);
        else
            weaponManager?.ShowMeleeLoadout();

        OnWeaponTypeChanged?.Invoke(false);

        Debug.Log("切换到近战武器 [按键1]");
    }

    /// <summary>
    /// 切换到远程武器
    /// </summary>
    public void SwitchToRanged()
    {
        if(IsUsingRanged) return;

        if (!HasRangeWeaponEquipped())
        {
            Debug.Log("未装备远程武器，无法切换！");
            return;
        }

        isUsingRanged = true;

        if(rangeFighter != null)
        {
            rangeFighter.enabled = true;
        }


        weaponManager?.SwitchToSlot(FindRangedSlotIndex());

        OnWeaponTypeChanged?.Invoke(true);

        Debug.Log("切换到远程武器 [按键2]");
    }

    /// <summary>
    /// 远程槽不再写死为 index=1（步骤 3 后 1=LeftHand/盾）。
    /// 按 isRanged 找实际装备了远程武器的槽。
    /// </summary>
    int FindRangedSlotIndex()
    {
        if (weaponManager == null) return rangeSlotIndex;
        for (int i = 0; i < weaponManager.SlotCount; i++)
        {
            WeaponConfig c = weaponManager.GetSlotConfig(i);
            if (c != null && c.isRanged) return i;
        }
        return rangeSlotIndex;
    }
    /// <summary>
    /// 检查是否装备了远程武器。
    ///
    /// ⚠️ 不能写死查 `rangeSlotIndex`：步骤 3 之后 slot1 = LeftHand（盾），
    /// 写死查 1 会让「明明装备了弓」被判成没有远程武器，`SwitchToRanged()` 直接拒绝
    /// → 远程模式永远进不去 → 右键只能举盾（实测踩过）。
    /// 口径必须与 `FindRangedSlotIndex()` 一致：扫全部槽位找 `isRanged`。
    /// </summary>
    private bool HasRangeWeaponEquipped()
    {
        if(weaponManager == null) return false;

        for (int i = 0; i < weaponManager.SlotCount; i++)
        {
            WeaponConfig config = weaponManager.GetSlotConfig(i);
            if (config != null && config.isRanged) return true;
        }
        return false;
    }
}

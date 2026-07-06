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
    private MeleeFighter meleeFighter;
    private RangedFighter rangeFighter;

    //当前状态
    private bool isUsingRanged = false;

    public event Action<bool> OnWeaponTypeChanged;//true = 远程 false = 进程。

    public bool IsUsingRanged => isUsingRanged;

    private void Awake()
    {
        weaponManager = GetComponent<WeaponManager>();
        meleeFighter = GetComponent<MeleeFighter>();
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
    /// 切换到近战武器
    /// </summary>
    public void SwitchToMelle()
    {
        if(!isUsingRanged) return;
        isUsingRanged = false;

        if(meleeFighter != null)
        {
            meleeFighter.enabled = true;
        }

        if(rangeFighter != null)
        {
            rangeFighter.enabled = false;
        }

        //通知WeaponManager切换武器模型
        weaponManager?.SwitchToSlot(meleeSlotIndex); 

        OnWeaponTypeChanged?.Invoke(false);

        Debug.Log("切换到远程武器 [按键2]");
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

        if(meleeFighter != null)
        {
            meleeFighter.enabled = false;
        }

        if(rangeFighter != null)
        {
            rangeFighter.enabled = true;
        }


        weaponManager?.SwitchToSlot(rangeSlotIndex);

        OnWeaponTypeChanged?.Invoke(true);

        Debug.Log("切换到远程武器 [按键2]");
    }
    /// <summary>
    /// 检查远程槽位是否装备了远程武器
    /// </summary>
    private bool HasRangeWeaponEquipped()
    {
        if(weaponManager == null) return false;

        WeaponConfig config = weaponManager.GetSlotConfig(rangeSlotIndex);

        return config != null && config.isRanged;
    }
}

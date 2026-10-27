using UnityEngine;

/// <summary>
/// 左下角四槽面板：右手武器 / 左手武器 / 道具 / 法术。
///
/// 武器槽订阅 GameEvents.OnPlayerWeaponSlotChanged；打开时再主动拉一次
/// WeaponManager 当前装备 —— 事件只在变化时发，HUD 打开时武器可能早就装好了。
/// 道具 / 法术槽当前没有数据源，保持空槽，等背包使用与技能系统接线。
/// </summary>
public class PlayerQuickSlots : MonoBehaviour
{
    [Header("四个槽")]
    [SerializeField] private QuickSlotWidget rightHandSlot;
    [SerializeField] private QuickSlotWidget leftHandSlot;
    [SerializeField] private QuickSlotWidget itemSlot;
    [SerializeField] private QuickSlotWidget spellSlot;

    [Header("武器槽索引（对应 WeaponManager.weaponSlots）")]
    [SerializeField] private int rightHandSlotIndex = 0;
    [SerializeField] private int leftHandSlotIndex = 1;

    [Header("绑定")]
    [Tooltip("找不到 WeaponManager 时的重试间隔（秒）")]
    [SerializeField] private float bindRetryInterval = 0.5f;

    private WeaponManager m_weaponManager;
    private bool m_bound;
    private float m_nextBindTime;

    private void OnEnable()
    {
        GameEvents.OnPlayerWeaponSlotChanged += HandleWeaponSlotChanged;

        // 道具 / 法术：暂无数据源，先摆空槽
        if (itemSlot != null) itemSlot.SetEmpty();
        if (spellSlot != null) spellSlot.SetEmpty();

        // 双手同时可见，但主手（右手）是当前操作手，常亮选中
        if (rightHandSlot != null) rightHandSlot.SetSelected(true);
        if (leftHandSlot != null) leftHandSlot.SetSelected(false);

        m_nextBindTime = 0f;
        TryBind();
    }

    private void OnDisable()
    {
        GameEvents.OnPlayerWeaponSlotChanged -= HandleWeaponSlotChanged;
        m_bound = false;
    }

    private void Update()
    {
        if (m_bound) return;
        if (Time.unscaledTime < m_nextBindTime) return;

        m_nextBindTime = Time.unscaledTime + Mathf.Max(0.1f, bindRetryInterval);
        TryBind();
    }

    private void TryBind()
    {
        if (m_bound) return;

        m_weaponManager = FindObjectOfType<WeaponManager>(true);
        if (m_weaponManager == null) return;

        m_bound = true;
        RefreshAllWeaponSlots();
    }

    private void RefreshAllWeaponSlots()
    {
        if (m_weaponManager == null) return;

        ApplyWeapon(rightHandSlot, m_weaponManager.GetSlotConfig(rightHandSlotIndex));
        ApplyWeapon(leftHandSlot, m_weaponManager.GetSlotConfig(leftHandSlotIndex));
    }

    private void HandleWeaponSlotChanged(int slotIndex, int weaponId)
    {
        if (slotIndex == rightHandSlotIndex)
        {
            ApplyWeaponId(rightHandSlot, weaponId);
        }
        else if (slotIndex == leftHandSlotIndex)
        {
            ApplyWeaponId(leftHandSlot, weaponId);
        }
    }

    private void ApplyWeapon(QuickSlotWidget widget, WeaponConfig config)
    {
        ApplyWeaponId(widget, config != null ? config.weaponID : -1);
    }

    private void ApplyWeaponId(QuickSlotWidget widget, int weaponId)
    {
        if (widget == null) return;

        // 武器图标走 Item 表的 image_path（与背包/商店同一套图）
        Sprite icon = weaponId > 0 ? DataRepository.GetItemIcon(weaponId) : null;
        if (icon != null) widget.SetItem(icon);
        else widget.SetEmpty();
    }
}

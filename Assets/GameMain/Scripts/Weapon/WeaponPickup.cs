using UnityEngine;

public class WeaponPickup : MonoBehaviour
{
    [SerializeField] int weaponId;
    [SerializeField] float pickupRadius = 1.5f;

    bool equipped = false;
    Collider activeCollider;
    PlayerController interactingPlayer;
    PickupPopup activePopup;

    void Awake()
    {
        SetupTriggerCollider();
    }

    void OnValidate()
    {
        if (Application.isPlaying) return;
        SphereCollider sc = GetComponent<SphereCollider>();
        if (sc != null) sc.radius = pickupRadius;
    }

    void SetupTriggerCollider()
    {
        SphereCollider existing = GetComponent<SphereCollider>();
        if (existing != null)
        {
            existing.isTrigger = true;
            existing.radius = pickupRadius;
            activeCollider = existing;
            return;
        }

        Collider col = GetComponent<Collider>();
        if (col != null) Destroy(col);

        SphereCollider sc = gameObject.AddComponent<SphereCollider>();
        sc.isTrigger = true;
        sc.radius = pickupRadius;
        activeCollider = sc;
    }

    void OnTriggerEnter(Collider other)
    {
        if (equipped) return;
        PlayerController pc = other.GetComponentInParent<PlayerController>();
        if (pc == null) return;
        interactingPlayer = pc;
        pc.SetNearestPickup(this);
        ShowPickupPopup();
    }

    void OnTriggerExit(Collider other)
    {
        PlayerController pc = other.GetComponentInParent<PlayerController>();
        if (pc == null) return;
        pc.SetNearestPickup(null);
        ClosePickupPopup();
    }

    void ShowPickupPopup()
    {
        UIManager.Instance.ClosePanel(UIconst.PickupPopup);

        PackageTableItem item = GameManager.Instance.GetPackageItemById(weaponId);
        PickupPopupData data = new PickupPopupData
        {
            weaponId = weaponId,
            weaponName = item?.name ?? "Unknown"
        };

        activePopup = UIManager.Instance.OpenPanel(UIconst.PickupPopup) as PickupPopup;
        if (activePopup != null)
            activePopup.ShowPopup(data);
    }

    public void TryEquip()
    {
        if (equipped) return;
        HandleEquip();
    }

    void HandleEquip()
    {
        if (equipped) return;
        bool success = InventoryManager.Instance.EquipFromGround(weaponId);
        if (!success)
        {
            ToastMessage.Show("装备失败！");
            ClosePickupPopup();
            return;
        }
        equipped = true;

        // 【修复】先立刻清理玩家引用，防止碰撞体禁用后OnTriggerExit不触发导致残留
        if (interactingPlayer != null)
        {
            interactingPlayer.SetNearestPickup(null);
            interactingPlayer = null;
        }

        // 【修复】直接关闭面板（不走淡出协程），避免与后续Destroy竞态
        if (activePopup != null)
        {
            activePopup = null;
        }
        UIManager.Instance.ClosePanel(UIconst.PickupPopup);

        PlayPickupEffect();
    }

    void ClosePickupPopup()
    {
        if (activePopup != null)
        {
            activePopup.ClosePopup();
            activePopup = null;
        }
        // 保险：直接通知UIManager关闭面板
        UIManager.Instance.ClosePanel(UIconst.PickupPopup);

        if (interactingPlayer != null)
        {
            interactingPlayer.SetNearestPickup(null);
            interactingPlayer = null;
        }
    }

    void PlayPickupEffect()
    {
        if (activeCollider != null) activeCollider.enabled = false;
        foreach (var r in GetComponentsInChildren<Renderer>())
            r.enabled = false;
        Destroy(gameObject, 0.3f);
    }

    void OnDisable()
    {
        // 【修复】无论是否已装备，都清理玩家引用和弹窗
        if (activePopup != null)
        {
            activePopup.ClosePopup();
            activePopup = null;
        }
        UIManager.Instance.ClosePanel(UIconst.PickupPopup);

        if (interactingPlayer != null)
        {
            interactingPlayer.SetNearestPickup(null);
            interactingPlayer = null;
        }
    }
}

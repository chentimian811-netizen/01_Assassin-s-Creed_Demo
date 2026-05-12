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
            return;
        }
        equipped = true;
        ClosePickupPopup();
        PlayPickupEffect();
    }

    void ClosePickupPopup()
    {
        if (activePopup != null)
        {
            activePopup.ClosePopup();
            activePopup = null;
        }
        if (interactingPlayer != null)
        {
            interactingPlayer.SetNearestPickup(null);
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
        if (!equipped)
        {
            ClosePickupPopup();
        }
    }
}

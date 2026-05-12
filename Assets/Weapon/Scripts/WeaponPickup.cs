using UnityEngine;

public class WeaponPickup : MonoBehaviour
{
    [SerializeField] int weaponId;
    [SerializeField] float pickupRadius = 1.5f;

    bool playerInside = false;
    bool pickupInProgress = false;
    Collider activeCollider;
    PlayerController interactingPlayer;

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
        // 用组件检测代替 Tag 检测（Player 对象可能没有 "Player" Tag）
        if (other.GetComponentInParent<PlayerController>() == null) return;
        playerInside = true;
    }

    void OnTriggerExit(Collider other)
    {
        if (other.GetComponentInParent<PlayerController>() == null) return;
        playerInside = false;
    }

    public void TryPickup(PlayerController player)
    {
        if (pickupInProgress || !playerInside) return;

        pickupInProgress = true;
        interactingPlayer = player;
        player.acceptInput = false;

        PackageTableItem item = GameManager.Instance.GetPackageItemById(weaponId);
        PickupPopupData data = new PickupPopupData
        {
            weaponId = weaponId,
            weaponName = item?.name ?? "Unknown",
            iconPath = item?.imagePath ?? "",
            starCount = item?.star ?? 0,
            onEquip = () => HandleEquip(),
            onAddToBag = () => HandleAddToBag(),
            onClose = () => CleanupPickup()
        };

        PickupPopup popup = UIManager.Instance.OpenPanel(UIconst.PickupPopup) as PickupPopup;
        if (popup != null)
            popup.ShowPopup(data);
        else
            CleanupPickup(); // 弹窗打开失败时也要恢复输入
    }

    void HandleEquip()
    {
        bool success = InventoryManager.Instance.EquipFromGround(weaponId);
        if (!success)
        {
            ToastMessage.Show("装备失败！");
            CleanupPickup();
            return;
        }
        CleanupPickup(); // 成功后必须恢复输入
        PlayPickupEffect();
    }

    void HandleAddToBag()
    {
        string uid = InventoryManager.Instance.AddToBag(weaponId);
        if (uid == null)
        {
            ToastMessage.Show("背包已满！");
            CleanupPickup();
            return;
        }
        CleanupPickup(); // 成功后必须恢复输入
        PlayPickupEffect();
    }

    void PlayPickupEffect()
    {
        if (activeCollider != null) activeCollider.enabled = false;
        foreach (var r in GetComponentsInChildren<Renderer>())
            r.enabled = false;
        Destroy(gameObject, 0.3f);
    }

    void CleanupPickup()
    {
        if (interactingPlayer != null)
        {
            interactingPlayer.acceptInput = true;
            interactingPlayer = null;
        }
        pickupInProgress = false;
    }

    void OnDisable()
    {
        CleanupPickup();
    }
}
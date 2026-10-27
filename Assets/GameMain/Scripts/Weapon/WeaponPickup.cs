using UnityEngine;

public class WeaponPickup : MonoBehaviour
{
    [SerializeField] int weaponId;
    [SerializeField] float pickupRadius = 1.5f;

    bool equipped = false;
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

    /// <summary>
    /// 显示拾取提示。界面是 GF 池化 + 异步打开的，这里只下单（WeaponPickup 拿不到实例）；
    /// 已经在显示时 PickupForm 会自己换内容并重新淡入，不会再开第二个。
    /// </summary>
    void ShowPickupPopup()
    {
        var item = GameManager.Instance.GetPackageItemById(weaponId);
        PickupForm.ShowPrompt(weaponId, item?.Name ?? "Unknown");
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

        
        if (interactingPlayer != null)
        {
            interactingPlayer.SetNearestPickup(null);
            interactingPlayer = null;
        }

        // 关掉拾取提示（GF 侧负责淡出，这里只下单）
        PickupForm.HidePrompt();

        PlayPickupEffect();
    }

    void ClosePickupPopup()
    {
        // 请求淡出并关闭；界面不在时会自己忽略
        PickupForm.HidePrompt();

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
        // 无论是否已装备，都清理玩家引用和提示（武器被拾走/场景卸载都会走到这里）
        PickupForm.HidePrompt();

        if (interactingPlayer != null)
        {
            interactingPlayer.SetNearestPickup(null);
            interactingPlayer = null;
        }
    }
}

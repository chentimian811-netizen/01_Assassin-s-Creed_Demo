using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

public class ShopCell : MonoBehaviour, IPointerClickHandler
{
    private Transform UIIcon;
    private Transform UIQuantity;
    private Transform UIName;
    private Transform UIPrice;
    private DRShop shopData;
    private int shopKeeperId;
    private ShopPanel UIParent;

    private void Awake()
    {
        InitUIName();
    }

    private void InitUIName()
    {
        UIIcon = transform.Find("Object_Icon/Icon");
        UIQuantity = transform.Find("Number");
        UIName = transform.Find("Obj_Name_price/Name");
        UIPrice = transform.Find("Obj_Name_price/Price");
    }

    public void Refresh(DRShop data, int keeperId, ShopPanel parent)
    {
        this.shopData = data;
        this.shopKeeperId = keeperId;
        this.UIParent = parent;

        DRItem item = DataRepository.GetItemByAssetId(data.ItemAssetId);

        if (UIName != null)
            UIName.GetComponent<TextMeshProUGUI>().text = item?.Name ?? "未知";

        if (UIPrice != null)
        {
            int finalPrice = Mathf.RoundToInt(data.Price * data.Discount);
            UIPrice.GetComponent<TextMeshProUGUI>().text = finalPrice.ToString();
        }

        if (UIQuantity != null)
        {
            int stock = ShopManager.Instance.GetStock(keeperId, data.ItemAssetId);
            if (stock == -1)
                UIQuantity.gameObject.SetActive(false);
            else
            {
                UIQuantity.gameObject.SetActive(true);
                UIQuantity.GetComponent<TextMeshProUGUI>().text = "x" + stock;
            }
        }

        RefreshIcon();
    }

    private void RefreshIcon()
    {
        if (UIIcon == null) return;
        var icon = DataRepository.GetItemIcon(shopData.ItemAssetId);
        if (icon != null)
            UIIcon.GetComponent<Image>().sprite = icon;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        UIParent.OnCellClicked(this);
    }

    public void SetSelected(bool selected) { }

    public DRShop GetShopData() => shopData;
}
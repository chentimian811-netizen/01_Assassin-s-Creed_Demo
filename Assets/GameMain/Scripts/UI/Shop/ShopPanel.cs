using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ShopPanel : BasePanel
{
    private Transform UITitle;
    private Transform UIGoldDisplay;
    private Transform UIScrollView;
    private Transform UICloseBtn;
    private Transform UIDetailPanel;
    private Transform UIDetailIcon;
    private Transform UIDetailName;
    private Transform UIDetailDesc;
    private Transform UIConfirmBtn;
    private Transform UIWeaponSkillDesc;
    private Transform[] UIDetailStar = new Transform[5];

    private GameObject shopCellPrefab;
    private int currentShopKeeperId;
    private DRShop selectedShopData;

    protected override void Awake()
    {
        base.Awake();
        InitUIName();
        InitClick();
        InitPrefab();
    }

    public void OpenWithConfig(string name, int shopKeeperId)
    {
        this.currentShopKeeperId = shopKeeperId;
        OpenPanel(name);
        RefreshGold();
        RefreshUI();
    }

    private void InitUIName()
    {
        UITitle = transform.Find("TopCenter/StoreName");
        UIGoldDisplay = transform.Find("TopCenter/GoldNumber");
        UIScrollView = transform.Find("Center/Scroll View");
        UICloseBtn = transform.Find("RightTop/Close/Icon");
        UIDetailPanel = transform.Find("Center/DetailPanel");
        UIDetailIcon = transform.Find("Center/DetailPanel/Center/Icon");
        UIDetailName = transform.Find("Center/DetailPanel/Top/Bg/Title");
        UIDetailDesc = transform.Find("Center/DetailPanel/Center/Description");
        UIWeaponSkillDesc = transform.Find("Center/DetailPanel/Button/SkillDescription");
        UIConfirmBtn = transform.Find("Right_Low/Buy");

        Transform starGroup = transform.Find("Center/DetailPanel/Center/StartLevel");
        if (starGroup != null)
        {
            for (int i = 0; i < 5; i++)
                UIDetailStar[i] = starGroup.Find("Image" + (i + 1));
        }
    }

    private void InitClick()
    {
        if (UICloseBtn != null)
            UICloseBtn.GetComponent<Button>().onClick.AddListener(OnClickClose);
        if (UIConfirmBtn != null)
            UIConfirmBtn.GetComponent<Button>().onClick.AddListener(OnClickConfirm);
    }

    private void InitPrefab()
    {
        shopCellPrefab = Resources.Load("Prefabs/Panels/Shop/ShopCl") as GameObject;
    }

    private void RefreshUI()
    {
        if (UIScrollView == null) return;
        Transform content = UIScrollView.GetComponent<ScrollRect>().content;
        for (int i = content.childCount - 1; i >= 0; i--)
            Destroy(content.GetChild(i).gameObject);
        RefreshBuyList(content);
    }

    private void RefreshBuyList(Transform content)
    {
        List<DRShop> items = DataRepository.GetShopItems(currentShopKeeperId);
        List<ShopCell> cells = new List<ShopCell>();

        foreach (DRShop shopData in items)
        {
            Transform cell = Instantiate(shopCellPrefab.transform, content) as Transform;
            ShopCell shopCell = cell.GetComponent<ShopCell>();
            shopCell.Refresh(shopData, currentShopKeeperId, this);
            cells.Add(shopCell);
        }

        if (cells.Count > 0) OnCellClicked(cells[0]);
    }

    private void RefreshGold()
    {
        if (UIGoldDisplay != null)
            UIGoldDisplay.GetComponent<TextMeshProUGUI>().text
                = CurrencyManager.Instance.Gold.ToString();
    }

    public void OnCellClicked(ShopCell clickedCell)
    {
        Transform content = UIScrollView.GetComponent<ScrollRect>().content;
        for (int i = 0; i < content.childCount; i++)
        {
            ShopCell cell = content.GetChild(i).GetComponent<ShopCell>();
            if (cell != null) cell.SetSelected(false);
        }

        clickedCell.SetSelected(true);
        DRShop shopData = clickedCell.GetShopData();
        UIDetailPanel.gameObject.SetActive(true);

        DRItem item = DataRepository.GetItemByAssetId(shopData.ItemAssetId);

        UIDetailName.GetComponent<Text>().text = item?.Name ?? "未知";
        UIDetailDesc.GetComponent<Text>().text = item?.Description ?? "";
        UIWeaponSkillDesc.GetComponent<Text>().text = item?.SkillDescription ?? "";

        var icon = DataRepository.GetItemIcon(shopData.ItemAssetId);
        if (icon != null)
            UIDetailIcon.GetComponent<Image>().sprite = icon;

        if (item != null)
        {
            for (int i = 0; i < 5; i++)
            {
                if (UIDetailStar[i] != null)
                    UIDetailStar[i].gameObject.SetActive(i < item.Star);
            }
        }

        selectedShopData = shopData;
    }

    private void OnClickConfirm()
    {
        if (selectedShopData == null) return;
        bool success = ShopManager.Instance.BuyItem(
            currentShopKeeperId, selectedShopData.ItemAssetId);
        if (success) { RefreshGold(); RefreshUI(); }
    }

    private void OnClickClose()
    {
        CurrencyManager.Instance.OnGoldChanged -= OnGoldChangeHandler;
        ClosePanel();
    }

    private void OnGoldChangeHandler(int newGold)
    {
        if (UIGoldDisplay != null)
            UIGoldDisplay.GetComponent<TextMeshProUGUI>().text = newGold.ToString();
    }

    public override void OpenPanel(string name)
    {
        base.OpenPanel(name);
        CursorManager.Instance.AddLock("Shop");
        Time.timeScale = 0f;
        CurrencyManager.Instance.OnGoldChanged += OnGoldChangeHandler;
    }

    public override void ClosePanel()
    {
        CurrencyManager.Instance.OnGoldChanged -= OnGoldChangeHandler;
        CursorManager.Instance.RemoveLock("Shop");  
        Time.timeScale = 1f;
        base.ClosePanel();
    }
}
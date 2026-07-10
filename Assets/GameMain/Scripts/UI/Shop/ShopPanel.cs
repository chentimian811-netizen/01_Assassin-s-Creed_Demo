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

    private GameObject shopCellPrefab_Weapon;
    private GameObject shopCellPrefab_Food;

    private GameObject detailPanelPrefab_Food;
    private GameObject detailPanelPrefab_Weapon;
    private GameObject currentDetailPanel;
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
        UIConfirmBtn = transform.Find("Right_Low/Buy");
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
        shopCellPrefab_Weapon = Resources.Load("Prefabs/Panels/Shop/ShopWeapon") as GameObject;
        shopCellPrefab_Food = Resources.Load("Prefabs/Panels/Shop/ShopFood") as GameObject;

        detailPanelPrefab_Weapon = Resources.Load("Prefabs/Panels/Shop/DetailPanel_Weapon") as GameObject;
        detailPanelPrefab_Food = Resources.Load("Prefabs/Panels/Shop/DetailPanel_Food") as GameObject;
    }

    private void RefreshUI()
    {
        if (UIScrollView == null) return;
        Transform content = UIScrollView.GetComponent<ScrollRect>().content;
        for (int i = content.childCount - 1; i >= 0; i--)
            DestroyImmediate(content.GetChild(i).gameObject);
        RefreshBuyList(content);
    }

    private void RefreshBuyList(Transform content)
    {
        List<DRShop> items = DataRepository.GetShopItems(currentShopKeeperId);
        foreach (DRShop shopData in items)
        {   
            DRItem item = DataRepository.GetItemByAssetId(shopData.ItemAssetId);
            GameObject prefab = (item != null && item.Type == GameConst.PackageTypeFood)
                ? shopCellPrefab_Food : shopCellPrefab_Weapon;
            Transform cell = Instantiate(prefab.transform,content);
            cell.GetComponent<ShopCell>().Refresh(shopData,currentShopKeeperId,this);
        }

        Transform first = content.childCount > 0 ? content.GetChild(0) : null;
        if(first != null)
        {
            OnCellClicked(first.GetComponent<ShopCell>());
        }
    }

    private void RefreshGold()
    {
        if (UIGoldDisplay != null)
            UIGoldDisplay.GetComponent<TextMeshProUGUI>().text
                = CurrencyManager.Instance.Gold.ToString();
    }

    private void SwapDetailPanel(bool isFood)
    {
        Transform slot = transform.Find("Center/DetailSlot");
        for(int i = slot.childCount - 1; i >= 0; i--)
        {
            DestroyImmediate(slot.GetChild(i).gameObject);
        }
        GameObject prefba = isFood ? detailPanelPrefab_Food : detailPanelPrefab_Weapon; 
        
        currentDetailPanel = Instantiate(prefba, slot, false);
        currentDetailPanel.name = "DetailPanel";

        UIDetailPanel = currentDetailPanel.transform;
        UIDetailIcon = UIDetailPanel.Find("Center/Icon");
        UIDetailName = UIDetailPanel.Find("Top/Bg/Title");
        UIDetailDesc = UIDetailPanel.Find("Center/Description");
        UIWeaponSkillDesc = UIDetailPanel.Find("Button/SkillDescription");

        Transform starGroup = UIDetailPanel.Find("Center/StartLevel");
        if (starGroup != null)
        {
            for (int i = 0; i < 5; i++)
                UIDetailStar[i] = starGroup.Find("Image" + (i + 1));
        }
    }

    public void OnCellClicked(ShopCell clickedCell)
    {   
        if(clickedCell == null) return;

        Transform content = UIScrollView.GetComponent<ScrollRect>().content;
        for (int i = 0; i < content.childCount; i++)
        {
            ShopCell cell = content.GetChild(i).GetComponent<ShopCell>();
            if (cell != null) cell.SetSelected(false);
        }

        clickedCell.SetSelected(true);
        DRShop shopData = clickedCell.GetShopData();
        if(shopData == null) return;

        DRItem item = DataRepository.GetItemByAssetId(shopData.ItemAssetId);
        bool isFood = item != null && item.Type == GameConst.PackageTypeFood;
        SwapDetailPanel(isFood);

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
        UIManager.Instance.ClosePanel(UIconst.ShopPanel);
    }

    private void OnGoldChangeHandler(int newGold)
    {
        if (UIGoldDisplay != null)
            UIGoldDisplay.GetComponent<TextMeshProUGUI>().text = newGold.ToString();
    }

    public override void OpenPanel(string name)
    {
        base.OpenPanel(name);

        CurrencyManager.Instance.OnGoldChanged += OnGoldChangeHandler;
    }

    public override void ClosePanel()
    {
        CurrencyManager.Instance.OnGoldChanged -= OnGoldChangeHandler; 

        base.ClosePanel();
    }
}
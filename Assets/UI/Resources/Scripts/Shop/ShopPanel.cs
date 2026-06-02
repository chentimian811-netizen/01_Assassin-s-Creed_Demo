using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

///<summary>
///商店面板-支持购买和出售两种模式
/// </summary>
public class ShopPanel : BasePanel
{
    private Transform UITitle;
    private Transform UIGoldDisplay;
    private Transform UIBuyTab;
    private Transform UISellTab;
    private Transform UIScrollView;
    private Transform UICloseBtn;
    private Transform UIDetailPanel;
    private Transform UIDetailIcon;
    private Transform UIDetailName; 
    private Transform UIDetailPrice; 
    private Transform UIConfirmBtn; 


    private GameObject shopCellPrefab;

    private enum _ShopMode
    {
        Buy,
        Sell
    }
    private _ShopMode curMode = _ShopMode.Buy;
    private ShopConfig currentConfig;
    private ShopItemDisplay selectedDisplay;
    private string selectedUid;

    protected override void Awake()
    {
        base.Awake();
        InitUIName();
        InitClick();
        InitPrefab();
    }

    public void OpenWithConfig(string name,ShopConfig config)
    {
        this.currentConfig = config;
        OpenPanel(name);
        RefreshGold();
        RefreshUI();
    }
    
    private void InitUIName()
    {
        
        // --- 顶部 ---
        UITitle = transform.Find("TopCenter/Title");
        UIGoldDisplay = transform.Find("TopCenter/GoldDisplay");

        // --- 中部 Tab ---
        UIBuyTab = transform.Find("Center/BuyTab");
        UISellTab = transform.Find("Center/SellTab");
        UIScrollView = transform.Find("Center/ScrollView");

        // --- 右上关闭 ---
        UICloseBtn = transform.Find("RightTop/Close/Icon");

        // --- 详情面板 ---
        UIDetailPanel = transform.Find("DetailPanel");
        UIDetailIcon = transform.Find("DetailPanel/Icon");
        UIDetailName = transform.Find("DetailPanel/Name");
        UIDetailPrice = transform.Find("DetailPanel/Price");
        UIConfirmBtn = transform.Find("DetailPanel/ConfirmBtn");

    }
    private void InitClick()
    { 
        UIBuyTab.GetComponent<Button>().onClick.AddListener(OnClickBuyTab);
        UISellTab.GetComponent<Button>().onClick.AddListener(OnClickSellTab);
        UICloseBtn.GetComponent<Button>().onClick.AddListener(OnClickClose);
        UIConfirmBtn.GetComponent<Button>().onClick.AddListener(OnClickConfirm);
    }

    private void InitPrefab()
    {
        shopCellPrefab = Resources.Load("Prefabs/Panels/Shop/ShopCell")as GameObject;
    }

    private void OnClickBuyTab()
    {
        if(curMode == _ShopMode.Buy)return;//如果等于购买模式 就不处理
        curMode = _ShopMode.Buy;
        ClearSelection();
        RefreshUI();
    }

    private void OnClickSellTab()
    {
        if(curMode == _ShopMode.Sell)return;
        curMode = _ShopMode.Sell;
        ClearSelection();
        RefreshUI();
    }

    private void RefreshUI()
    {
        Transform content = UIScrollView.GetComponent<ScrollRect>().content;
        for(int i = content.childCount - 1;i >= 0 ; i++)
        {
            Destroy(content.GetChild(i).gameObject);
        }

        UIDetailPanel.gameObject.SetActive(false);

        if(curMode == _ShopMode.Buy)
        {
            RefreshBuyList(content);
        }
        else
        {
            RefreshSellList(content);
        }
    }
    private void RefreshBuyList(Transform content)
    {
        if(currentConfig == null)return;

        List<ShopItemDisplay> items = ShopManager.Instance.GetShopItems(currentConfig);
        foreach (ShopItemDisplay display in items)
        {
            Transform cell = Instantiate(shopCellPrefab.transform,content)as Transform;
            ShopCell shopcell = cell.GetComponent<ShopCell>();
            shopcell.Refresh(display,this);
        }

    }
    private void RefreshSellList(Transform content)
    {
        List<PackageLocalData.PackageLocalItem> localItems = PackageLocalData.Instance.LoadPackage();

        foreach(PackageLocalData.PackageLocalItem localItem in localItems)
        {
            if(localItem.isEquipped)continue;
            PackageTableItem tableItem = GameManager.Instance.GetPackageItemById(localItem.id);
            if(tableItem == null)continue;

            ShopItemDisplay display = new ShopItemDisplay
            {
                itemData = new ShopItemData
                {
                    itemID = localItem.id,
                    price = ShopManager.Instance.GetSellPrice(localItem.id),
                    stock = -1,
                    discount = 1f
                },
                currentStock = -1,
                finalPrice = ShopManager.Instance.GetSellPrice(localItem.id),
                uid = localItem.uid
            };
            Transform cell = Instantiate(shopCellPrefab.transform,content)as Transform;
            ShopCell shopCell = cell.GetComponent<ShopCell>();
            shopCell.Refresh(display,this);
        }
    }

    private void RefreshGold()
    {
        UIGoldDisplay.GetComponent<Text>().text = "金币:" + CurrencyManager.Instance.Gold;
    }

    public void OnCellClicked(ShopCell clickedCell)
    {
        ClearSelection();

        clickedCell.SetSelected(true);

        ShopItemDisplay display = clickedCell.GetDisplayData();

        if(curMode == _ShopMode.Buy)
        {
            selectedDisplay = display;
            selectedUid = null;

            UIDetailPanel.gameObject.SetActive(true);

            UIDetailName.GetComponent<Text>().text = 
                GameManager.Instance.GetPackageItemById(display.itemData.itemID)?
            .name ?? "未知";
            UIDetailPrice.GetComponent<Text>().text = "money"+ display.finalPrice;
            UIConfirmBtn.GetComponentInChildren<Text>().text = "出售";
        }
    }

    private void ClearSelection()
    {
        Transform content = UIScrollView.GetComponent<ScrollRect>().content;
        for(int i = 0;i < content.childCount; i++)
        {
            ShopCell cell = content.GetChild(i).GetComponent<ShopCell>();
            if(cell != null)
            {
                cell.SetSelected(false);
            }
        }
        selectedDisplay = null;
        selectedUid = null;
    }

    //确认按钮
    private void OnClickConfirm()
    {
        if(curMode == _ShopMode.Buy)
        {
            HandleBuy();
        }
        else
        {
            HandleSell();
        }
    }

    private void HandleBuy()
    {
        if(selectedDisplay == null || currentConfig == null)return;

        bool success = ShopManager.Instance.BuyItem(currentConfig,selectedDisplay.itemData.itemID);
        if (success)
        {
            RefreshGold();
            RefreshUI();
        }
    }

    private void HandleSell()
    {
        if(string.IsNullOrEmpty(selectedUid))return;
        bool success = ShopManager.Instance.SellItem(selectedUid);

        if (success)
        {
            RefreshGold();
            RefreshUI();
        }
    }

    private void OnClickClose()
    {
        CurrencyManager.Instance.OnGoldChanged -= OnGoldChangeHandler;
        ClosePanel();
        UIManager.Instance.OpenPanel(UIconst.MainPanel);
    }

    private void OnGoldChangeHandler(int newGold)
    {
        UIGoldDisplay.GetComponent<Text>().text = "金币：" + newGold;

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

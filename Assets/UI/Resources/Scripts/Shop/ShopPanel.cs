using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

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
    private Transform UIDetailDesc;
    private Transform UIDetailPrice;
    private Transform UIDetailStock;
    private Transform UIDetailDiscount;
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
        Transform topBar = transform.Find("TopBar");
        if(topBar != null)
        {
            Debug.Log("[ShopPanel] TopBar 子节点:");
            foreach(Transform child in topBar) Debug.Log("  - [" + child.name + "]");
        }
        else
        {
            Debug.LogError("[ShopPanel] 缺失: TopBar");
        }
        UITitle = transform.Find("TopBar/Shop");
        UIGoldDisplay = transform.Find("TopBar/GoldDisplay");
        UIBuyTab = transform.Find("TopBar/BuyTab");
        UISellTab = transform.Find("TopBar/SellTab");

        // --- 中部 Tab ---
        UIScrollView = transform.Find("ItemGrid/ScrollView");

        // --- 右上关闭 ---
        UICloseBtn = transform.Find("CloseBtn/Close");

        // --- 详情面板 ---
        UIDetailPanel = transform.Find("DetailPanel");
        UIDetailIcon = transform.Find("DetailPanel/ItemIcon");
        UIDetailName = transform.Find("DetailPanel/ItemName");
        UIDetailDesc = transform.Find("DetailPanel/ItemDesc");
        UIDetailPrice = transform.Find("DetailPanel/ItemPrice");
        UIDetailStock = transform.Find("DetailPanel/ItemStock");
        UIDetailDiscount = transform.Find("DetailPanel/ItemDiscount");
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
        for(int i = content.childCount - 1;i >= 0 ; i--)
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
        UIGoldDisplay.GetComponent<TextMeshProUGUI>().text = "Gold:" + CurrencyManager.Instance.Gold;
    }

    public void OnCellClicked(ShopCell clickedCell)
    {
        ClearSelection();

        clickedCell.SetSelected(true);

        ShopItemDisplay display = clickedCell.GetDisplayData();

        UIDetailPanel.gameObject.SetActive(true);

        PackageTableItem tableItem = GameManager.Instance.GetPackageItemById(display.itemData.itemID);
        string itemName = tableItem != null ? tableItem.name : "未知";

        UIDetailName.GetComponent<TextMeshProUGUI>().text = itemName;
        UIDetailPrice.GetComponent<TextMeshProUGUI>().text = "价格: " + display.finalPrice;
        UIConfirmBtn.GetComponentInChildren<TextMeshProUGUI>().text = curMode == _ShopMode.Buy ? "购买" : "出售";

        // 描述
        if(UIDetailDesc != null)
        {
            UIDetailDesc.GetComponent<TextMeshProUGUI>().text = tableItem != null ? tableItem.description : "";
        }

        // 库存
        if(UIDetailStock != null)
        {
            if(curMode == _ShopMode.Buy)
            {
                UIDetailStock.gameObject.SetActive(true);
                UIDetailStock.GetComponent<TextMeshProUGUI>().text =
                    display.currentStock == -1 ? "库存: 无限" : "库存: " + display.currentStock;
            }
            else
            {
                UIDetailStock.gameObject.SetActive(false);
            }
        }

        // 折扣
        if(UIDetailDiscount != null)
        {
            if(display.itemData.discount < 1f)
            {
                UIDetailDiscount.gameObject.SetActive(true);
                int discountDisplay = Mathf.RoundToInt(display.itemData.discount * 10);
                UIDetailDiscount.GetComponent<TextMeshProUGUI>().text = discountDisplay + "折";
            }
            else
            {
                UIDetailDiscount.gameObject.SetActive(false);
            }
        }

        // 保存选中数据
        if(curMode == _ShopMode.Buy)
        {
            selectedDisplay = display;
            selectedUid = null;
        }
        else
        {
            selectedDisplay = null;
            selectedUid = display.uid;
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
        // UIManager.Instance.OpenPanel(UIconst.MainPanel);
    }

    private void OnGoldChangeHandler(int newGold)
    {
        UIGoldDisplay.GetComponent<TextMeshProUGUI>().text = "金币：" + newGold;

    }

    public override void OpenPanel(string name)
    {
        base.OpenPanel(name);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        Time.timeScale = 0f;
        CurrencyManager.Instance.OnGoldChanged += OnGoldChangeHandler;
    }

    public override void ClosePanel()
    {
        CurrencyManager.Instance.OnGoldChanged -= OnGoldChangeHandler;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        Time.timeScale = 1f;
        base.ClosePanel();

    }
}

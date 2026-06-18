using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

///<summary>
///商店面板-支持购买
/// </summary>
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

    private GameObject shopCellPrefab;
    private ShopConfig currentConfig;
    private ShopItemDisplay selectedDisplay;

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
       //顶部
        UITitle = transform.Find ("TopCenter/StoreName");
        UIGoldDisplay = transform.Find("TopCenter/GoldNumber");

        //物品滚动列表
        UIScrollView = transform.Find("Center/Scroll View");

        //关闭按钮
        UICloseBtn = transform.Find("RightTop/Close/Icon");

        //详情面板
        UIDetailPanel = transform.Find("Center/DetailPanel");
        UIDetailIcon = transform.Find("Center/DetailPanel/Center/Icon");
        UIDetailName = transform.Find("Center/DetailPanel/Top/Bg/Title");
        UIDetailDesc = transform.Find("Center/DetailPanel/Button/Description");

        //购买按钮
        UIConfirmBtn = transform.Find("Right_Low/Buy");


        Debug.Log("UIDetailPanel = " + (UIDetailPanel != null ? "OK" : "NULL"));
        Debug.Log("UIDetailIcon = " + (UIDetailIcon != null ? "OK" : "NULL"));
        Debug.Log("UIDetailName = " + (UIDetailName != null ? "OK" : "NULL"));
        Debug.Log("UIDetailDesc = " + (UIDetailDesc != null ? "OK" : "NULL"));
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
        //加载ShopCl作为物品的子预制体
        shopCellPrefab = Resources.Load("Prefabs/Panels/Shop/ShopCl")as GameObject;
    }

    private void RefreshUI()
    {   
        if (UIScrollView == null) return;
        //清空列表
        Transform content = UIScrollView.GetComponent<ScrollRect>().content;
        for(int i = content.childCount - 1 ; i >= 0; i--)
        {
            Destroy(content.GetChild(i).gameObject);
        }

        //隐藏详情面板
        if (UIDetailPanel != null)
        UIDetailPanel.gameObject.SetActive(false);

        //刷新购买列表
        RefreshBuyList(content);
    }

    /// <summary>
    /// 从 ShopManager 获取商品列表，逐个实例化 ShopCl 格子并填充数据
    /// </summary>
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

    private void RefreshGold()
    {
        if(UIGoldDisplay != null)
        {
            UIGoldDisplay.GetComponent<TextMeshProUGUI>().text = 
                CurrencyManager.Instance.Gold.ToString();
        }
    }

    public void OnCellClicked(ShopCell clickedCell)
    {
       //清除之前所有选中的状态
       Transform content = UIScrollView.GetComponent<ScrollRect>().content;

       for(int i = 0;i < content.childCount; i++)
        {
            ShopCell cell = content.GetChild(i).GetComponent<ShopCell>();
            if(cell != null) cell.SetSelected(false);
        }

        //选中当前点击的格子
        clickedCell.SetSelected(true);
        ShopItemDisplay display = clickedCell.GetDisplayData();

        //显示详情面板
        UIDetailPanel.gameObject.SetActive(true);

        //从物品表获取名称和描述   
        PackageTableItem tableItem = GameManager.Instance.GetPackageItemById(display.itemData.itemID);

        //设置详情面板内容
        UIDetailName.GetComponent<Text>().text = 
            tableItem != null ? tableItem.name:"未知";
        UIDetailDesc.GetComponent<Text>().text =
            tableItem != null ? tableItem.description:"";

        //设置武器图标
        if(tableItem != null && !string.IsNullOrEmpty(tableItem.imagePath))
        {
            Sprite icon = Resources.Load<Sprite>(tableItem.imagePath);
            if(icon != null)
            {
                UIDetailIcon.GetComponent<Image>().sprite = icon;
            }
        }

        //保存选中数据
        selectedDisplay = display;
    }

    //确认按钮
    private void OnClickConfirm()
    {
        if(selectedDisplay == null || currentConfig == null) return;

        bool success = ShopManager.Instance.BuyItem(currentConfig,selectedDisplay.itemData.itemID);
        if (success)
        {
            RefreshGold();
            RefreshUI();
        }

    }


    /// <summary>
    /// 关闭按钮点击：退订金币事件 → 关闭面板
    /// </summary>
    private void OnClickClose()
    {
        CurrencyManager.Instance.OnGoldChanged -= OnGoldChangeHandler;
        ClosePanel();
        
    }

    //金币变化时的回调
    private void OnGoldChangeHandler(int newGold)
    {
        if(UIGoldDisplay != null)
        {
            UIGoldDisplay.GetComponent<TextMeshProUGUI>().text = newGold.ToString();
        }

    }

    /// <summary>
    /// 打开面板时：解锁光标、暂停游戏、订阅金币事件
    /// </summary>
    public override void OpenPanel(string name)
    {
        base.OpenPanel(name);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        Time.timeScale = 0f;
        CurrencyManager.Instance.OnGoldChanged += OnGoldChangeHandler;
    }

    
    /// <summary>
    /// 关闭面板时：退订事件、锁定光标、恢复游戏
    /// </summary>
    public override void ClosePanel()
    {
        CurrencyManager.Instance.OnGoldChanged -= OnGoldChangeHandler;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        Time.timeScale = 1f;
        base.ClosePanel();

    }
}

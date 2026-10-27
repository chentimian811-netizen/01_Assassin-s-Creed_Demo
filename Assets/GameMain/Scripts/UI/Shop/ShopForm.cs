//------------------------------------------------------------
// ACDemo — 商店界面（GF UIFormLogic 版）
// 放置路径: Assets/GameMain/Scripts/UI/Shop/ShopForm.cs
// 对应 Prefab: Assets/UI/Prefabs/Shop/ShopForm.prefab
// 分组: Popup（模态：锁光标 + 暂停，统一由 GameUIForm 处理）
// 来源: 由旧 ShopPanel(BasePanel + UIManager) 迁移。买卖业务（ShopManager / DataRepository）未改动，
//       只替换「生命周期 + 参数传递 + 关闭方式」：
//         旧 ShopNPC 直接 new 面板再调 OpenWithConfig(name, keeperId)
//         → 现在把 keeperId 装进 ShopFormArgs 走 UIRouter 的 userData
//         旧 UIManager.ClosePanel → CloseSelf()
//------------------------------------------------------------

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ShopForm : GameUIForm
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

    // Awake 在实例化时跑一次：找子节点 + 绑按钮 + 加载格子/详情预制体（GF 复用时不会再调用）
    private void Awake()
    {
        InitUIName();
        InitClick();
        InitPrefab();
    }

    /// <summary>
    /// 每次打开都跑：从 userData 取 keeperId → 重置选中 → 订阅金币 → 刷列表与详情。
    /// 旧实现由 ShopNPC 先 OpenPanel 再调 OpenWithConfig，池化复用后那条路径不再存在。
    /// </summary>
    protected override void OnOpenForm(object userData)
    {
        ShopFormArgs args = userData as ShopFormArgs;
        if (args == null)
        {
            // 缺参数时不要抛异常：显示一个空商店，用户点关闭即可
            Debug.LogWarning("[ShopForm] 缺少 ShopFormArgs，商店列表将为空（应由 ShopNPC 传入 keeperId）");
            currentShopKeeperId = 0;
        }
        else
        {
            currentShopKeeperId = args.KeeperId;
        }

        // 复用实例时上一次的选中项/详情面板会留着，先清干净
        selectedShopData = null;
        ClearDetailSlot();

        SubscribeGold();

        RefreshGold();
        RefreshUI();
    }

    /// <summary>
    /// 每次关闭都跑：退订金币事件、清选中项，避免下次打开用的是上一位商人的数据。
    /// </summary>
    protected override void OnCloseForm()
    {
        UnsubscribeGold();

        selectedShopData = null;
        currentShopKeeperId = 0;
    }

    private void SubscribeGold()
    {
        // 先退订再订阅，防止上一次关闭路径异常导致重复绑定
        CurrencyManager.Instance.OnGoldChanged -= OnGoldChangeHandler;
        CurrencyManager.Instance.OnGoldChanged += OnGoldChangeHandler;
    }

    private void UnsubscribeGold()
    {
        CurrencyManager.Instance.OnGoldChanged -= OnGoldChangeHandler;
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

        detailPanelPrefab_Weapon = Resources.Load("Prefabs/Panels/ObjectDetiel/DetailPanel_Weapon") as GameObject;
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
            Transform cell = Instantiate(prefab.transform, content);
            cell.GetComponent<ShopCell>().Refresh(shopData, currentShopKeeperId, this);
        }

        Transform first = content.childCount > 0 ? content.GetChild(0) : null;
        if (first != null)
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

    /// <summary>清空详情槽（打开界面时兜底，避免上一位商人的详情残留）</summary>
    private void ClearDetailSlot()
    {
        Transform slot = transform.Find("Center/DetailSlot");
        if (slot == null) return;

        for (int i = slot.childCount - 1; i >= 0; i--)
        {
            DestroyImmediate(slot.GetChild(i).gameObject);
        }
        currentDetailPanel = null;
    }

    private void SwapDetailPanel(bool isFood)
    {
        Transform slot = transform.Find("Center/DetailSlot");
        for (int i = slot.childCount - 1; i >= 0; i--)
        {
            DestroyImmediate(slot.GetChild(i).gameObject);
        }

        GameObject prefab = isFood ? detailPanelPrefab_Food : detailPanelPrefab_Weapon;
        currentDetailPanel = Instantiate(prefab, slot, false);
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
        if (clickedCell == null) return;

        Transform content = UIScrollView.GetComponent<ScrollRect>().content;
        for (int i = 0; i < content.childCount; i++)
        {
            ShopCell cell = content.GetChild(i).GetComponent<ShopCell>();
            if (cell != null) cell.SetSelected(false);
        }

        clickedCell.SetSelected(true);
        DRShop shopData = clickedCell.GetShopData();
        if (shopData == null) return;

        DRItem item = DataRepository.GetItemByAssetId(shopData.ItemAssetId);
        bool isFood = item != null && item.Type == GameConst.PackageTypeFood;
        SwapDetailPanel(isFood);

        UIDetailName.GetComponent<Text>().text = item?.Name ?? "未知";
        if (UIDetailDesc != null)
        {
            UIDetailDesc.GetComponent<Text>().text = item?.Description ?? "";
        }

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

        Transform typeText = UIDetailPanel.Find("Center/WeaponType");
        if (typeText != null && item != null)
            typeText.GetComponent<Text>().text = item.WeaponType.ToString();

        Transform dmgText = UIDetailPanel.Find("Center/BaseDamageText");
        if (dmgText != null && item != null)
            dmgText.GetComponent<Text>().text = item.BaseDamage.ToString();

        Transform critText = UIDetailPanel.Find("Center/CritRateText");
        if (critText != null && item != null)
            critText.GetComponent<Text>().text = $"{item.CritRate * 100:F0}%";

        Transform leveText = UIDetailPanel.Find("Button/LevelText");
        if (leveText != null)
            leveText.GetComponent<Text>().text = $"Lv.1/{WeaponUpgradeSystem.GetMaxLevel()}";

        selectedShopData = shopData;
    }

    private void OnClickConfirm()
    {
        if (selectedShopData == null) return;

        bool success = ShopManager.Instance.BuyItem(currentShopKeeperId, selectedShopData.ItemAssetId);
        if (success)
        {
            RefreshGold();
            RefreshUI();
        }
    }

    private void OnClickClose()
    {
        // 旧实现是 UIManager.ClosePanel（内部 Destroy(gameObject)），GF 池化后必须走路由
        CloseSelf();
    }

    private void OnGoldChangeHandler(int newGold)
    {
        if (UIGoldDisplay != null)
            UIGoldDisplay.GetComponent<TextMeshProUGUI>().text = newGold.ToString();
    }
}

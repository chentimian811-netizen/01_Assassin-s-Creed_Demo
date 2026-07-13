using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using static PackageLocalData;
using TMPro;

public enum PackageMode
{
    normal,
    delete,
    sort
}

public class PackagePanel : BasePanel
{
    private Transform UIMenu;
    private Transform UIMenuWeapon;
    private Transform WeaponIndicator;
    private Transform UIMenuFood;
    private Transform FoodIndicator;
    private Transform UITabName;
    private Transform UICloseBtn;
    private Transform UICenter;
    private Transform UIScrollView;
    private Transform UIDetailPanel;
    private Transform UILeftBtn;
    private Transform UIRightBtn;
    private Transform UIDeletePanel;
    private Transform UIDeleteBackBtn;
    private Transform UIDeleteInfoText;
    private Transform UIDeleteConfirmBtn;
    private Transform UIBottomMenus;
    private Transform UIDeleteBtn;
    private Transform UIDetailBtn;
    private Transform UITotalInfoText;

    public GameObject packageCellPrefab;
    public GameObject packageCellPrefab_Food;

    private GameObject detailPanelPrefab_Food;
    private GameObject detailPanelPrefab_Weapon;
    private Transform detailSlot;

    private Transform UISortBtn;
    private TMP_Text sortBtnText;
    private readonly Dictionary<E_SortMode,string>SortNames = new()
    {
        {E_SortMode.Default,"Quality"},
        {E_SortMode.ByLevel,"Level"},
        {E_SortMode.ByName,"Name"},
        {E_SortMode.ByAcquisition,"Acquisition"},
    };

    //当前页面处于上面模式
    public PackageMode curMode = PackageMode.normal;

    private const int PackageMaxCapacity = 1000;
    public List<string> deleteChooseUid;

    private string _chooseUid;

    public string ChooseUid
    {
        get { return _chooseUid; }
        set
        {
            _chooseUid = value;
            RefreshDetail();
        }
    }
    private int CurrentFilterType = -1;

    private class TabDef
    {
        public string name;
        public int filterType;
        public Transform indicator;
    }

    private List<TabDef> tabs = new List<TabDef>();
    private int currentTabIndex = 0;
    public void AddChooseDeleteUid(string uid)
    {
        this.deleteChooseUid ??= new List<string>();
        if ((!this.deleteChooseUid.Contains(uid)))
        {
            this.deleteChooseUid.Add(uid);
        }
        else
        {
            this.deleteChooseUid.Remove(uid);
        }
        RefreshDeletePanel();
    }

    private void RefreshDeletePanel()
    {
        RectTransform scrollContent = UIScrollView.GetComponent<ScrollRect>().content;
        foreach (Transform child in scrollContent)
        {
            PackageCell packageCell = child.GetComponent<PackageCell>();
            packageCell.RefreshDeleteState();
        }

        int total = GetFilteredItemCount();
        int selected = deleteChooseUid?.Count ?? 0;
        if(UIDeleteInfoText != null)
        {
            UIDeleteInfoText.GetComponent<Text>().text = $"已选{selected}/{total}";
        }
    }

    override protected void Awake()
    {
        base.Awake();
        InitUI();
    }

    private void Start()
    {
        InitTabs();
        WeaponIndicator.gameObject.SetActive(true);
        FoodIndicator.gameObject.SetActive(false);
        RefreshUI();
    }

    private void InitTabs()
    {
        tabs = new List<TabDef>
        {
            new TabDef { name = "武器", filterType = GameConst.PackageTypeWeapon, indicator = WeaponIndicator },
            new TabDef { name = "食物", filterType = GameConst.PackageTypeFood, indicator = FoodIndicator },
        };

        // 默认选中第一个标签（武器）
        currentTabIndex = 0;
        SwitchToTab(0);

    }

    private void SwitchToTab(int index)
    {
        if (index < 0 || index >= tabs.Count) return;

        var tab = tabs[index];
        CurrentFilterType = tab.filterType;

        // 切换所有标签的 Indicator 显示状态
        foreach (var t in tabs)
            t.indicator.gameObject.SetActive(false);
        tab.indicator.gameObject.SetActive(true);

        RefreshScrollView();

    }

    private void InitUI()
    {
        InitUIName();
        InitClick();
        InitPrefab();
    }

    private void RefreshUI()
    {
        RefreshScrollView();
    }

    private void RefreshDetail()
    {
        PackageLocalItem localItem = GameManager.Instance.GetPackageLocalItemByUid(ChooseUid);
        if(localItem == null) return;

        var config = GameManager.Instance.GetPackageItemById(localItem.id);
        bool isFood = config != null && config.Type == GameConst.PackageTypeFood;

        for(int i = detailSlot.childCount -1;i >= 0; i--)
        {
            DestroyImmediate(detailSlot.GetChild(i).gameObject);
        }

        GameObject prefab = isFood ? detailPanelPrefab_Food : detailPanelPrefab_Weapon;
        GameObject go = Instantiate(prefab,detailSlot,false);
        go.name = "DetailPanel";

        go.GetComponent<PackageDetail>().Refresh(localItem,this);
    }

    private void RefreshScrollView()
    {
        //清理滚动容器中原本的代码
        RectTransform scrollContent = UIScrollView.GetComponent<ScrollRect>().content;
        for (int i = 0; i < scrollContent.childCount; i++)
        {
            Destroy(scrollContent.GetChild(i).gameObject);
        }


        List<PackageLocalItem> filteredItems = new List<PackageLocalItem>();
        foreach (PackageLocalItem localData in GameManager.Instance.GetSortPackageLocalData())
        {
            //筛选逻辑
            if(CurrentFilterType >= 0)
            {
                //根据id查询配置表判断逻辑
                var config = GameManager.Instance.GetPackageItemById(localData.id);
                if(config != null && config.Type != CurrentFilterType)
                {
                    continue;//跳过不符合类型的物品
                }

            }
            filteredItems.Add(localData);
        }

        if(CurrentFilterType == GameConst.PackageTypeFood)
        {
            filteredItems = MergeFoodItems(filteredItems);
        }


            PackageCell firstCell = null;

            for(int i = 0; i < filteredItems.Count; i++)
        {
            //根据类型选择预制体
            var config = GameManager.Instance.GetPackageItemById(filteredItems[i].id);
            GameObject prefab = (config != null && config.Type == GameConst.PackageTypeFood)
                ?packageCellPrefab_Food:packageCellPrefab;
            Transform PackageUIItem =  Instantiate(prefab.transform,scrollContent)as Transform;
            PackageCell packageCell = PackageUIItem.GetComponent<PackageCell>();
            packageCell.Refresh(filteredItems[i], this);

            if(firstCell == null)
            {
                firstCell = packageCell;
            }
        }

        if(firstCell != null)
        {
            ChooseUid = firstCell.GetUid();
            firstCell.ShowSelectionFrame(); 

        }

        if(UITotalInfoText != null)
        {
            int totalCount = GetFilteredItemCount();
            UITotalInfoText.GetComponent<Text>().text = $"{totalCount}/{PackageMaxCapacity}";

        }

    }

    private List<PackageLocalItem>MergeFoodItems(List<PackageLocalItem> items)
    {
        Dictionary<int,PackageLocalItem> merged = new Dictionary<int, PackageLocalItem>();
        foreach(PackageLocalItem item in items)
        {
            if (merged.ContainsKey(item.id))
            {
                merged[item.id].num += item.num;
            }
            else
            {
                merged[item.id] = new PackageLocalItem
                {
                    uid = item.uid,
                    id = item.id,
                    num= item.num,
                    level = item.level,
                    isNew = item.isNew

                };
            }
        }
        return new List<PackageLocalItem>(merged.Values);
    }

    private void InitUIName()
    {
        UIMenu = transform.Find("TopCenter/Menus");
        UIMenuWeapon = transform.Find("TopCenter/Menus/Weapons");
        WeaponIndicator = transform.Find("TopCenter/Menus/Weapons/Indicator");
        UIMenuFood = transform.Find("TopCenter/Menus/Food");
        FoodIndicator = transform.Find("TopCenter/Menus/Food/Indicator");
        UITabName = transform.Find("LeftTop/Name");
        UICloseBtn = transform.Find("RightTop/Close/Icon");
        UITotalInfoText = transform.Find("RightTop/PackageNum/AmountText");
        UICenter = transform.Find("Center");
        UIScrollView = transform.Find("Center/Scroll View");
        UILeftBtn = transform.Find("Left/NextBackPack/icon");
        UIRightBtn = transform.Find("Right/NextBackPack/icon");
        detailSlot = transform.Find("Center/DetailSlot");

        UIDeletePanel = transform.Find("Bottom/DeletePanel");
        UIDeleteBackBtn = transform.Find("Bottom/DeletePanel/Back");
        UIDeleteInfoText = transform.Find("Bottom/DeletePanel/DestroyNum/InfoText");
        UIDeleteConfirmBtn = transform.Find("Bottom/DeletePanel/ConfirmBtn");

        UIBottomMenus = transform.Find("Bottom/BottomMenus");
        UIDeleteBtn = transform.Find("Bottom/BottomMenus/DeleteBtn");
        UIDetailBtn = transform.Find("Bottom/BottomMenus/DetilBtn");

        UISortBtn = transform.Find("Bottom/BottomMenus/SortBtn");
        sortBtnText = UISortBtn?.Find("Text")?.GetComponent<TMP_Text>();

        UIDeletePanel.gameObject.SetActive(false);
        UIBottomMenus.gameObject.SetActive(true);
    }

    private void InitClick()
    {
        UIMenuWeapon.GetComponent<Button>().onClick.AddListener(OnClickWeapon);
        UIMenuFood.GetComponent<Button>().onClick.AddListener(OnClickFood);
        UICloseBtn.GetComponent<Button>().onClick.AddListener(OnClickClose);

        UILeftBtn.GetComponent<Button>().onClick.AddListener(OnClickLeft);
        UIRightBtn.GetComponent<Button>().onClick.AddListener(OnClickRight);

        UIDeleteBackBtn.GetComponent<Button>().onClick.AddListener(OnClickDeleteBack);
        UIDeleteConfirmBtn.GetComponent<Button>().onClick.AddListener(OnClickDeleteConfirm);

        UIDeleteBtn.GetComponent<Button>().onClick.AddListener(OnClickDelete);
        UIDetailBtn.GetComponent<Button>().onClick.AddListener(OnClickDetail);

        UISortBtn?.GetComponent<Button>().onClick.AddListener(OnClickSort);
    }

    private void InitPrefab()
    {
        detailPanelPrefab_Weapon = Resources.Load<GameObject>("Prefabs/Panels/Shop/DetailPanel_Weapon");
        detailPanelPrefab_Food = Resources.Load<GameObject>("Prefabs/Panels/Shop/DetailPanel_Food");
    }

    private void OnClickDetail()
    {
        print("点击了详情");
    }

    private void OnClickDelete()
    {
        print("点击了删除");
        curMode = PackageMode.delete;
        UIDeletePanel.gameObject.SetActive(true);
        RefreshDeletePanel();
    }

    private void OnClickDeleteConfirm()
    {
        print("点击了删除确认");
        if (this.deleteChooseUid == null || this.deleteChooseUid.Count == 0)
        {
            return;
        }
        GameManager.Instance.DeletePackageItem(this.deleteChooseUid);
        this.deleteChooseUid = new List<string>();
        //删除后刷新整个页面
        curMode = PackageMode.normal;
        UIDeletePanel.gameObject.SetActive(false);
        RefreshUI();
    }

    private void OnClickDeleteBack()
    {
        print("点击了删除返回");
        curMode = PackageMode.normal;
        UIDeletePanel.gameObject.SetActive(false);
        //重置选中的删除列表
        deleteChooseUid = new List<string>();
        //刷新选中状态
        RefreshDeletePanel();

        if(UIDeleteInfoText != null)
        {
            UIDeleteInfoText.GetComponent<Text>().text = "";
        }
    }

    private void OnClickRight()
    {
        print("点击了右边");
        currentTabIndex = (currentTabIndex + 1) % tabs.Count;
        SwitchToTab(currentTabIndex);

    }

    private void OnClickLeft()
    {
        print("点击了左边");
        currentTabIndex = (currentTabIndex - 1 + tabs.Count) % tabs.Count;
        SwitchToTab(currentTabIndex);

    }

    private int GetFilteredItemCount()
    {
        int count = 0;
        foreach(PackageLocalItem localData in GameManager.Instance.GetSortPackageLocalData())
        {
            if(CurrentFilterType >= 0)
            {
                var config = GameManager.Instance.GetPackageItemById(localData.id);
                if(config != null && config.Type != CurrentFilterType)
                    continue;
            }
            count++;
        }
        return count;
    }

    private void OnClickClose()
    {
        print("点击了关闭");
        CurrentFilterType = -1;//重置筛选
        UIManager.Instance.ClosePanel(UIconst.PackagePanel);

        var mainPanel = UIManager.Instance.GetPanel(UIconst.MainPanel);
        if(mainPanel != null)
        {
            mainPanel.gameObject.SetActive(true);
        }
        
    }
    
    private void OnClickFood()
    {
        currentTabIndex = 1;
        SwitchToTab(1);

    }

    private void OnClickWeapon()
    {
        currentTabIndex = 0;
        SwitchToTab(0);

    }
    public void RefreshList()
    {
        RefreshUI();
    }

    private void OnClickSort()
    {
        var modes = System.Enum.GetValues(typeof(E_SortMode));
        int next = ((int)GameManager.Instance.CurrentSorMode + 1)%modes.Length;
        GameManager.Instance.CurrentSorMode = (E_SortMode)next;
        if(sortBtnText != null)
        {
            sortBtnText.text = SortNames[(E_SortMode)next];
        }
        RefreshScrollView();
    }
}
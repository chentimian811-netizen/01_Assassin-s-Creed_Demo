using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using static PackageLocalData;

public enum  PackageMode
{
    normal,
    delete,
    sort
}


public class PackagePanel : BasePanel
{
    private Transform UIMenu;

    private Transform UIMenuWeapon;

    private Transform UIMenuFood;

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

    public  GameObject packageCellPrefab;

    //当前页面处于上面模式
    public PackageMode curMode = PackageMode.normal;

    public List<string> deleteChooseUid;

    private string _chooseUid;

    public string ChooseUid
    {
        get
        { 
            return _chooseUid; 
        }
        set 
        {
            _chooseUid = value;
            RefreshDetail();
        }
    }

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
    }

    override protected void Awake()
    {
        base.Awake();
        InitUI();
    }

    private void Start()
    {
        RefreshUI();
    }

    private void InitUI()
    {
        InitUIName();
        InitClick();
    }

    private void RefreshUI()
    {
        RefreshScrollView();
    }


    private void RefreshDetail()
    {
        //找到uid对应的动态数据
        PackageLocalItem localItem = GameManager.Instance.GetPackageLocalItemByUid(ChooseUid);
        //刷新详情面板
        UIDetailPanel.GetComponent<PackageDetail>().Refresh(localItem, this);
    }

    private void RefreshScrollView()
    {
         //清理滚动容器中原本的代码
         RectTransform scrollContent = UIScrollView.GetComponent<ScrollRect>().content;
        for (int i = 0; i < scrollContent.childCount; i++)
        {
            Destroy(scrollContent.GetChild(i).gameObject);
        }

        foreach (PackageLocalItem localData in GameManager.Instance.GetSortPackageLocalData())
        {
            Transform PackageUIItem = Instantiate(packageCellPrefab.transform, scrollContent) as Transform;
            PackageCell packageCell = PackageUIItem.GetComponent<PackageCell>();
            packageCell.Refresh(localData,this);
        }
    }

    private void InitUIName()
    {
        UIMenu = transform.Find("TopCenter/Menus");
        UIMenuWeapon = transform.Find("TopCenter/Menus/Weapons");
        UIMenuFood = transform.Find("TopCenter/Menus/Food");
        UITabName = transform.Find("LeftTop/Name");
        UICloseBtn = transform.Find("RightTop/Close/Icon");
        UICenter = transform.Find("Center");
        UIScrollView = transform.Find("Center/Scroll View");
        UIDetailPanel = transform.Find("Center/DetailPanel");
        UILeftBtn = transform.Find("Left/NextBackPack/icon");
        UIRightBtn = transform.Find("Right/NextBackPack/icon");

        UIDeletePanel = transform.Find("Bottom/DeletePanel");
        UIDeleteBackBtn = transform.Find("Bottom/DeletePanel/Back");
        UIDeleteInfoText = transform.Find("Bottom/DeletePanel/InfoText");
        UIDeleteConfirmBtn = transform.Find("Bottom/DeletePanel/ConfirmBtn");

        UIBottomMenus = transform.Find("Bottom/BottomMenus");
        UIDeleteBtn = transform.Find("Bottom/BottomMenus/DeleteBtn");
        UIDetailBtn = transform.Find("Bottom/BottomMenus/DetilBtn");

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
    }

    private void OnClickDeleteConfirm()
    {
        print("点击了删除确认");
        if (this.deleteChooseUid == null)
        {
            return;
        }
        if(this.deleteChooseUid.Count == 0)
        {
            return;
        }
        GameManager.Instance.DeletePackageItem(this.deleteChooseUid);
        //删除后刷新整个页面
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
    }

    private void OnClickRight()
    {
        print("点击了右边");
    }

    private void OnClickLeft()
    {
        print("点击了左边");
    }

    private void OnClickClose()
    {
        print("点击了关闭");
        ClosePanel();
        UIManager.Instance.OpenPanel(UIconst.MainPanel);
    }

    private void OnClickFood()
    {
        print("点击了食物");
    }

    private void OnClickWeapon()
    {
        print("点击了武器");
    }

    public void RefreshList()
    {
        RefreshUI();
    }
}



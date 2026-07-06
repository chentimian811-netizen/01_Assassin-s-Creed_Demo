using System.Collections;
using System.Collections.Generic;
using System.Xml.Serialization;
using UnityEngine;
using static PackageLocalData;


/// <summary>
/// 游戏管理器 - 全局单例，管理游戏状态和核心数据
/// </summary>
public class GameManager : MonoBehaviour
{
    [Header("主菜单设置")]
    [SerializeField] private GameObject healthBarRoot;
    [SerializeField] private GameObject minimapRoot;
    [SerializeField] private GameObject menuCharacterPrefab;  // 主菜单展示模型预制体
    [SerializeField] private Vector3 menuCharacterPosition;
    [SerializeField] private Vector3 menuCharacterRotation = new Vector3(0, 30, 0); 
    private GameObject menuCharacterInstance;                  // 场景中的实例
    private static GameManager _instance;
    private PackageTables packageTable;

    [Header("游戏状态")]
    [SerializeField] private bool isMainMenuActive = true;  // 是否在主菜单状态

    private void Awake()
    {
        // 如果已经有实例存在，销毁这个重复的
        if(_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public static GameManager Instance
    {
        get
        {
            return _instance;
        }
    }

    void Start()
    {
        InitMainMenu();
    }

    private void OnDestroy()
    {
        // 如果销毁的是当前实例，清空静态引用
        if(_instance == this)
        {
            _instance = null;
        }
    }

    /// <summary>
    /// 初始化主菜单状态
    /// 禁用玩家输入，打开主菜单面板
    /// </summary>
    private void InitMainMenu()
    {
        isMainMenuActive = true;

        //禁用玩家输入
        PlayerController player = FindObjectOfType<PlayerController>();
        if(player != null)
        {
            player.acceptInput = false;
            // 隐藏游戏主角（可选）
            player.gameObject.SetActive(false);
        }

        // 解锁鼠标，让主菜单按钮可以接收点击
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        // 禁用游戏 FreeLook 相机，让菜单相机控制画面
        CameraManager camManager = FindObjectOfType<CameraManager>();
        if(camManager != null && camManager.freeLook != null)
        {
            camManager.freeLook.gameObject.SetActive(false);
        }


        if(menuCharacterPrefab != null)
        {
            menuCharacterInstance = Instantiate(menuCharacterPrefab);
        }

         // 让菜单相机注视生成的角色
        MainMenuCamera mainMenuCamera = FindObjectOfType<MainMenuCamera>();
        if(mainMenuCamera != null && menuCharacterInstance != null)
        {
            mainMenuCamera.SetLookAtTarget(menuCharacterInstance.transform);
        }

        //隐藏游戏HUD
        if(healthBarRoot != null) healthBarRoot.SetActive(false);
        if(minimapRoot != null) minimapRoot.SetActive(false);

        //打开主菜单面板
        UIManager.Instance.OpenPanel(UIconst.MainMenuPanel);
    }

    /// <summary>
    /// 游戏开始时调用 - 从主菜单过渡到游戏状态
    /// </summary>
    public void StartGame()
    {
        isMainMenuActive = false;

        // 销毁主菜单展示模型
        if (menuCharacterInstance != null)
        {
            Destroy(menuCharacterInstance);
            menuCharacterInstance = null;
        }

        // 启用游戏主角（includeInactive=true 确保能找到被禁用的玩家）
        PlayerController player = FindObjectOfType<PlayerController>(true);
        if (player != null)
        {
            player.gameObject.SetActive(true);
            player.acceptInput = true;
        }

        // 重新锁定鼠标，进入游戏操作模式
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        // 激活 FreeLook 相机，让视角切换到玩家身上
        CameraManager camManager = FindObjectOfType<CameraManager>();
        if(camManager != null && camManager.freeLook != null)
        {
            camManager.freeLook.gameObject.SetActive(true);
            // 重置视角到玩家背后
            camManager.ResetFreeLookCamera();
        }

         //显示游戏HUD
        if(healthBarRoot != null) healthBarRoot.SetActive(true);
        if(minimapRoot != null) minimapRoot.SetActive(true);
    }


    public void DeletePackageItem(List<string> uids)
    {
        foreach(string uid in uids)
        {
            DeletePackageItem(uid, false);
        }
        PackageLocalData.Instance.SavePackage();
    }

    private void DeletePackageItem(string uid, bool needSave = true)
    {
        PackageLocalItem packageLocalItem = GetPackageLocalItemByUid(uid);
        if (packageLocalItem == null)
            return;

        PackageLocalData.Instance.items.Remove(packageLocalItem);
        if(needSave)
        {
            PackageLocalData.Instance.SavePackage();
        }
    }

    public PackageTables GetPackageTable()
    {
        if (packageTable == null)
        {
            packageTable = Resources.Load<PackageTables>("TableDate/PackageTable");
        }
        return packageTable;
    }

    //1：武器类型 2：食物类型
    //根据类型获取配置的表格数据
    public List<PackageTableItem> GetPackageDataByType(int type)
    {
        List<PackageTableItem> packageItems = new List<PackageTableItem>();
        foreach(PackageTableItem packageItem in GetPackageTable().DataList)
        {
            if(packageItem.type == type)
            {
                packageItems.Add(packageItem);
            }
        }
        return packageItems;
    }

    //随机抽卡获得一件武器
    public PackageLocalItem GetLotteryRandom1()
    {
        List<PackageTableItem> packageItems = GetPackageDataByType(GameConst.PackageTypeWeapon);
        int index = Random.Range(0, packageItems.Count);
        PackageTableItem packageItem = packageItems[index];
        PackageLocalItem packageLocalItem = new()
        {
            uid = System.Guid.NewGuid().ToString(),
            id = packageItem.id,
            num = 1,
            level = 1,
            isNew = CheckWeaponIsNew(packageItem.id)
        };
        PackageLocalData.Instance.items.Add(packageLocalItem);
        PackageLocalData.Instance.SavePackage();
        return packageLocalItem;
    }

    //随机抽卡 获得十件武器
    public List<PackageLocalItem> GetLotteryRandom10(bool sort = false)
    {
        //随机抽卡
        List<PackageLocalItem> packageLocalItems = new();
        for(int i = 0; i < 10;i ++ )
        {
            PackageLocalItem packageLocalItem = GetLotteryRandom1();
            packageLocalItems.Add(packageLocalItem);
        }

        //武器排序
        if (!sort)
        {
            packageLocalItems.Sort(new PackageItemComparer());
        }
        return packageLocalItems;
    }

    public  bool CheckWeaponIsNew(int id)
    {
        foreach(PackageLocalItem packageLocalItem in GetPackageLocalData())
        {
            if(packageLocalItem.id == id)
            {
                return false;
            }
        }
        return true;
    }

    public List<PackageLocalItem> GetPackageLocalData()
    {
        return PackageLocalData.Instance.LoadPackage();
    }

    public PackageTableItem GetPackageItemById(int id)
    {
        List<PackageTableItem> packageItems = GetPackageTable().DataList;
        foreach (PackageTableItem item in packageTable.DataList)
        {
            if (item.id == id)
            {
                return item;
            }
        }
        return null;
    }

    public PackageLocalItem GetPackageLocalItemByUid(string uid)
    {
        List<PackageLocalItem> packageDataList = GetPackageLocalData();
        foreach (PackageLocalItem item in packageDataList)
        {
            if (item.uid == uid)
            {
                return item;
            }
        }
        return null;
    }

    public List<PackageLocalItem> GetSortPackageLocalData()
    {
        List<PackageLocalItem> localItems = PackageLocalData.Instance.LoadPackage();
        localItems.Sort(new PackageItemComparer());
        return localItems;
    }
}

public class PackageItemComparer : IComparer<PackageLocalItem>
{
    public int Compare(PackageLocalItem a, PackageLocalItem b)
    {
        PackageTableItem x = GameManager.Instance.GetPackageItemById(a.id);
        PackageTableItem y = GameManager.Instance.GetPackageItemById(b.id);

        int starComparison = y.star.CompareTo(x.star);
        if(starComparison == 0)
        {
            int idComparison = y.id.CompareTo(x.id);
            if(idComparison == 0)
            {
                return b.level.CompareTo(a.level);
            }
            return idComparison;
        }
        return starComparison;
    }
}

public class GameConst
{
    // 武器类型
    public const int PackageTypeWeapon = 1;
    // 食物类型
    public const int PackageTypeFood = 2;
}
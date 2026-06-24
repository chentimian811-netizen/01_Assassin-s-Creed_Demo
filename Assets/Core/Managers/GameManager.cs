using System.Collections;
using System.Collections.Generic;
using System.Xml.Serialization;
using UnityEngine;
using static PackageLocalData;
using UnityEngine.SceneManagement;


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

    private Vector3 playerInitialPosition;      // 玩家初始位置
    private Quaternion playerInitialRotation;   // 玩家初始旋转
    private bool hasRecordedInitialPosition = false;  // 是否已记录
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

        // 确保时间缩放恢复正常（防止从暂停状态返回主菜单后 timeScale 仍为 0）
        Time.timeScale = 1f;

        // 停止主菜单BGM
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.StopMusic();
        }

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
            player.ResetPlayerState();
        }

        // 重置所有敌人
        ResetAllEnemies();

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

    //返回主菜单
    public void ReturnToMainMenu()
    {
        // 确保时间缩放恢复正常（防止从暂停状态返回时 timeScale 仍为 0）
        Time.timeScale = 1f;

        UIManager.Instance.ClosePanel(UIconst.DeathPanel);
        UIManager.Instance.ClosePanel(UIconst.PausePanel);
        InitMainMenu();

        // 回到主菜单时播放主菜单BGM
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayMusic();
        }
    }

    /// <summary>
    /// 重生玩家 - 传送到初始位置并重置状态
    /// </summary>
    public void RespawnPlayer()
    {
        // 关闭死亡面板
        UIManager.Instance.ClosePanel(UIconst.DeathPanel);

        Time.timeScale = 1f;


        // 重置玩家
        PlayerController player = FindObjectOfType<PlayerController>(true);
        if (player != null)
        {
            player.gameObject.SetActive(true);

            player.ResetPlayerState();
        }

        // 重置所有活着的敌人
        ResetAllEnemies();

        // 锁定鼠标
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        // 相机
        CameraManager camManager = FindObjectOfType<CameraManager>();
        if (camManager != null && camManager.freeLook != null)
        {
            camManager.freeLook.gameObject.SetActive(true);
            camManager.ResetFreeLookCamera();
        }

        // HUD
        if (healthBarRoot != null) healthBarRoot.SetActive(true);
        if (minimapRoot != null) minimapRoot.SetActive(true);
    }


    /// <summary>
    /// 重置所有敌人 - 恢复到初始状态
    /// </summary>
    private void ResetAllEnemies()
    {
        EnemyController[] allEnemies = FindObjectsOfType<EnemyController>(true);
        foreach (var enemy in allEnemies)
        {
            if (enemy == null) continue;

            // 先禁用再启用，强制重新初始化
            enemy.gameObject.SetActive(false);

            // 重置 Boss 特殊状态
            BossController boss = enemy as BossController;
            if (boss != null)
            {
                boss.ResetBossState();
            }

            // 重置血量（Boss 根据配置重置，普通敌人重置为 25）
            if (enemy.Fighter != null)
            {
                if (boss != null && boss.bossConfig != null)
                {
                    // Boss 使用配置的最大血量
                    enemy.Fighter.SetHealth(boss.bossConfig.maxHealth);
                }
                else
                {
                    enemy.Fighter.SetHealth(25f);
                }
            }

            // 清除战斗目标
            enemy.Target = null;
            enemy.TargetsInRange.Clear();

            EnemyManager.i.RemoveEnemyInRange(enemy);

            // 重新启用组件
            if (enemy.NavAgent != null)
            {
                enemy.NavAgent.enabled = true;
                // 只有在 NavMesh 上才调用 ResetPath
                if (enemy.NavAgent.isOnNavMesh)
                {
                    enemy.NavAgent.ResetPath();
                }
            }

            if (enemy.character != null)
            {
                enemy.character.enabled = true;
            }

            if (enemy.VisionSensor != null)
            {
                enemy.VisionSensor.gameObject.SetActive(true);
            }

            // 先激活敌人，再切换状态
            enemy.gameObject.SetActive(true);

            // 根据是否有巡逻路径决定初始状态
            if (enemy.GetComponent<PatrolPoute>() != null && enemy.GetComponent<PatrolPoute>().HasPoints)
            {
                enemy.ChangeState(E_EnemyState.Patrol);
            }
            else
            {
                enemy.ChangeState(E_EnemyState.Idle);
            }
        }
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
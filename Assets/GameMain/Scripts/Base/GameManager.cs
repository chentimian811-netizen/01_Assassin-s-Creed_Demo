using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static PackageLocalData;

public class GameManager : MonoBehaviour
{
    [Header("主菜单设置")]
    [SerializeField] private GameObject healthBarRoot;
    [SerializeField] private GameObject minimapRoot;
    [SerializeField] private GameObject menuCharacterPrefab;
    [SerializeField] private Vector3 menuCharacterPosition;
    [SerializeField] private Vector3 menuCharacterRotation = new Vector3(0, 30, 0);
    private GameObject menuCharacterInstance;
    private static GameManager _instance;

    public E_SortMode CurrentSorMode{get;set;}=E_SortMode.Default;

    [Header("游戏状态")]
    [SerializeField] private bool isMainMenuActive = true;

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
        DontDestroyOnLoad(gameObject);
        DataRepository.Initialize();
    }

    public static GameManager Instance => _instance;

    void Start()
    {
        InitMainMenu();
    }

    private void OnDestroy()
    {
        if (_instance == this) _instance = null;
    }

    private void InitMainMenu()
    {
        isMainMenuActive = true;
        PlayerController player = FindObjectOfType<PlayerController>();
        if (player != null)
        {
            player.acceptInput = false;
            player.gameObject.SetActive(false);
        }
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        CameraManager camManager = FindObjectOfType<CameraManager>();
        if (camManager != null && camManager.freeLook != null)
            camManager.freeLook.gameObject.SetActive(false);
        if (menuCharacterPrefab != null)
            menuCharacterInstance = Instantiate(menuCharacterPrefab);
        MainMenuCamera mainMenuCamera = FindObjectOfType<MainMenuCamera>();
        if (mainMenuCamera != null && menuCharacterInstance != null)
            mainMenuCamera.SetLookAtTarget(menuCharacterInstance.transform);
        if (healthBarRoot != null) healthBarRoot.SetActive(false);
        if (minimapRoot != null) minimapRoot.SetActive(false);
        UIManager.Instance.OpenPanel(UIconst.MainMenuPanel);
    }

    public void StartGame()
    {
        isMainMenuActive = false;
        if (menuCharacterInstance != null)
        {
            Destroy(menuCharacterInstance);
            menuCharacterInstance = null;
        }
        PlayerController player = FindObjectOfType<PlayerController>(true);
        if (player != null)
        {
            player.gameObject.SetActive(true);
            player.acceptInput = true;
        }
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        CameraManager camManager = FindObjectOfType<CameraManager>();
        if (camManager != null && camManager.freeLook != null)
        {
            camManager.freeLook.gameObject.SetActive(true);
            camManager.ResetFreeLookCamera();
        }
        if (healthBarRoot != null) healthBarRoot.SetActive(true);
        if (minimapRoot != null) minimapRoot.SetActive(true);
    }

    public void DeletePackageItem(List<string> uids)
    {
        foreach (string uid in uids)
            DeletePackageItem(uid, false);
        PackageLocalData.Instance.SavePackage();
    }

    private void DeletePackageItem(string uid, bool needSave = true)
    {
        PackageLocalItem packageLocalItem = GetPackageLocalItemByUid(uid);
        if (packageLocalItem == null) return;
        PackageLocalData.Instance.items.Remove(packageLocalItem);
        if (needSave) PackageLocalData.Instance.SavePackage();
    }

    public List<DRItem> GetPackageDataByType(int type)
    {
        List<DRItem> result = new List<DRItem>();
        foreach (var item in DataRepository.ItemTable.Values)
        {
            if (item.Type == type) result.Add(item);
        }
        return result;
    }

    public PackageLocalItem GetLotteryRandom1()
    {
        List<DRItem> packageItems = GetPackageDataByType(GameConst.PackageTypeWeapon);
        int index = Random.Range(0, packageItems.Count);
        DRItem packageItem = packageItems[index];
        PackageLocalItem packageLocalItem = new()
        {
            uid = System.Guid.NewGuid().ToString(),
            id = packageItem.Id,
            num = 1,
            level = 1,
            isNew = CheckWeaponIsNew(packageItem.Id)
        };
        PackageLocalData.Instance.items.Add(packageLocalItem);
        PackageLocalData.Instance.SavePackage();
        return packageLocalItem;
    }

    public List<PackageLocalItem> GetLotteryRandom10(bool sort = false)
    {
        List<PackageLocalItem> packageLocalItems = new();
        for (int i = 0; i < 10; i++)
            packageLocalItems.Add(GetLotteryRandom1());
        if (!sort) packageLocalItems.Sort(new PackageItemComparer{sorMode=E_SortMode.Default});
        return packageLocalItems;
    }

    public bool CheckWeaponIsNew(int id)
    {
        foreach (PackageLocalItem item in GetPackageLocalData())
        {
            if (item.id == id) return false;
        }
        return true;
    }

    public List<PackageLocalItem> GetPackageLocalData()
    => PackageLocalData.Instance.LoadPackage();

    public DRItem GetPackageItemById(int id)
    {
        DataRepository.ItemTable.TryGetValue(id, out var item);
        return item;
    }

    public PackageLocalItem GetPackageLocalItemByUid(string uid)
    {
        foreach (PackageLocalItem item in GetPackageLocalData())
        {
            if (item.uid == uid) return item;
        }
        return null;
    }

    public List<PackageLocalItem> GetSortPackageLocalData()
    {
        List<PackageLocalItem> localItems = PackageLocalData.Instance.LoadPackage();
        localItems.Sort(new PackageItemComparer(){sorMode = CurrentSorMode});
        return localItems;
    }
}

public class GameConst
{
    public const int PackageTypeWeapon = 1;
    public const int PackageTypeFood = 2;
}
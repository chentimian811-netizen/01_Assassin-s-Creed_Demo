using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class UIManager
{
    private static UIManager _instance;

    private Transform _uiRoot;
    private static readonly HashSet<string> ModalPanels = new HashSet<string>
    {
        UIconst.MainPanel,
        UIconst.PackagePanel,
        UIconst.LotteryPanel,
        UIconst.ShopPanel,
    };
    private Dictionary<string, string> pathDict;

    private Dictionary<string, GameObject> prefabDict;

    public Dictionary<string, BasePanel> panelDict;
    public static UIManager Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = new UIManager();
            }
            return _instance;
        }
    }

    public Transform UIRoot
    {
        get
        {
            if (_uiRoot == null)
            {
                if(GameObject.Find("Canvas"))
                {
                    _uiRoot = GameObject.Find("Canvas").transform;
                }
                else
                {
                    _uiRoot = new GameObject("Canvas").transform;
                }
                
            }
            return _uiRoot;
        }
    }

    private UIManager()
    {
        InitDicts();
    }

    private void InitDicts()
    {
        prefabDict = new Dictionary<string, GameObject>();
        panelDict = new Dictionary<string, BasePanel>();

        pathDict = new Dictionary<string, string>()
        {
            {UIconst.PackagePanel,"Package/PackagePanel" },
            {UIconst.LotteryPanel,"Lottery/LotteryPanel" },
            {UIconst.MainPanel,"MainPanel" },
            {UIconst.PickupPopup, "PickupPopup" },
            {UIconst.ShopPanel, "Shop/ShopPe" },
            {UIconst.MainMenuPanel,"MainMenu/MainMenuPanel"}
        };
    }

    public BasePanel GetPanel(string name)
    {
        BasePanel panel = null;

        if(panelDict.TryGetValue(name,out panel))
        {
            return panel;
        }
        return null;
    }

    public BasePanel OpenPanel(string name)
    {
        BasePanel panel = null;

        // 调试：检查面板是否已存在
        if(panelDict.TryGetValue(name,out panel))
        {
            Debug.LogWarning("面板已存在，无法重复打开: " + name);
            return null;
        }

        // 检查路径是否存在
        string path = "";
        if (!pathDict.TryGetValue(name, out path))
        {
            Debug.LogError("面板名称错误，或未配置路径: " + name);
            return null;
        }

        Debug.Log("正在打开面板: " + name + "，路径: " + path);

        // 使用缓存预制件
        GameObject panelPrefab = null;
        if (!prefabDict.TryGetValue(name, out panelPrefab))
        {
            string realPath = "Prefabs/Panels/"+ path;
            Debug.Log("加载预制体: " + realPath);

            panelPrefab = Resources.Load<GameObject>(realPath) as GameObject;

            if(panelPrefab == null)
            {
                Debug.LogError("预制件加载失败: " + realPath);
                return null;
            }
            prefabDict.Add(name, panelPrefab);
        }

        // 打开界面
        GameObject panelObject = GameObject.Instantiate(panelPrefab, UIRoot, false);
        panel = panelObject.GetComponent<BasePanel>();
        if (panel == null)
        {
            Debug.LogError("预制件缺少BasePanel组件: " + name);
            return null;
        }

        Debug.Log("面板打开成功: " + name);
        panelDict.Add(name, panel);
        panel.OpenPanel(name);

        if (ModalPanels.Contains(name))
        {
            Time.timeScale = 0f;
        }

        CursorManager.Instance.AddLock(name);
        return panel;

    }

    public bool ClosePanel(string name)
    {
        BasePanel panel = null;
        if(!panelDict.TryGetValue(name,out panel))
        {
            Debug.Log("面板未打开:"+name);
            return false;
        }
        CursorManager.Instance.RemoveLock(name);

        if (ModalPanels.Contains(name))
        {
            TryResumeGame();
        }

        panel.ClosePanel();
        panelDict.Remove(name);

        CursorManager.Instance.RemoveLock(name);
        if (ModalPanels.Contains(name))
        {
            TryResumeGame();
        }
        return true;
    }

    private void TryResumeGame()
    {
        foreach(var kv in panelDict)
        {
            if(ModalPanels.Contains(kv.Key)) return;
        }
        Time.timeScale = 1f;
    }
}

public class UIconst
{
    public const string PackagePanel = "PackagePanel";

    public const string LotteryPanel = "LotteryPanel";

    public const string MainPanel = "MainPanel";

    public const string PickupPopup = "PickupPopup";

    public const string ShopPanel = "ShopPanel";

    public const string MainMenuPanel = "MainMenuPanel";
}

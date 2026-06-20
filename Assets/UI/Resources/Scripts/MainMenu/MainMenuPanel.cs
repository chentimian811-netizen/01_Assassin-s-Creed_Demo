using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 主菜单面板 - 游戏启动时显示的首屏界面
/// 包含开始游戏、游戏设置、退出游戏三个功能按钮
/// </summary>
public class MainMenuPanel : BasePanel
{
    [Header("UI引用")]
    [SerializeField] private Button startGameBtn;    //开始游戏按钮
    [SerializeField] private Button settingsBtn;     //游戏设置按钮
    [SerializeField] private Button quitGameBtn;     //退出游戏按钮
    [SerializeField] private Text   titleText;      //标题文本
    private CanvasGroup canvasGroup; //淡入淡出效果

    [Header("动画设置")]
    [SerializeField] private float fadeInDuration = 1.5f;
    [SerializeField] private float buttonDelay = 0.3f;

    private bool lastFrameInstanceValid = true; // 上一帧 GameManager.Instance 是否有效

    protected override void Awake()
    {
        base.Awake();
        InitUI();
    }

    private void Update()
    {
        // 监控 GameManager.Instance 何时变成 null
        bool currentValid = (GameManager.Instance != null);
        if(lastFrameInstanceValid && !currentValid)
        {
            Debug.LogError("【监控】GameManager.Instance 在这一帧变成了 null！");
        }
        lastFrameInstanceValid = currentValid;
    }

    /// <summary>
    /// 初始化UI组件和事件绑定
    /// </summary>
    private void InitUI()
    {   
        // 检查是否在正确的 Canvas 下，如果不是，自动挂载到 MainMenuCanvas
        Transform currentCanvas = transform.parent;
        if(currentCanvas == null || currentCanvas.name != "MainMenuCanvas")
        {
        GameObject mainMenuCanvas = GameObject.Find("MainMenuCanvas");
        if(mainMenuCanvas != null)
        {
            transform.SetParent(mainMenuCanvas.transform, false);
            Debug.Log("MainMenuPanel 已挂载到 MainMenuCanvas");
        }
        }
        
        canvasGroup = GetComponent<CanvasGroup>();
        if(canvasGroup == null)
        {
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }

        Debug.Log("InitUI: startGameBtn=" + startGameBtn + ", settingsBtn=" + settingsBtn + ", quitGameBtn=" + quitGameBtn);
        Debug.Log("CanvasGroup.interactable=" + canvasGroup.interactable + ", CanvasGroup.blocksRaycasts=" + canvasGroup.blocksRaycasts);

        //绑定按钮事件
        if(startGameBtn != null)
        {
            startGameBtn.onClick.AddListener(OnStartGame);
            Debug.Log("开始游戏按钮绑定成功");
        }
        else
        {
            Debug.LogError("startGameBtn 为 null！请在预制体 Inspector 中拖拽赋值");
        }

        if(settingsBtn != null)
        {
            settingsBtn.onClick.AddListener(OnSettings);
        }
        else
        {
            Debug.LogError("settingsBtn 为 null！请在预制体 Inspector 中拖拽赋值");
        }

        if(quitGameBtn != null)
        {
            quitGameBtn.onClick.AddListener(OnQuitGame);
        }
        else
        {
            Debug.LogError("quitGameBtn 为 null！请在预制体 Inspector 中拖拽赋值");
        }

        //初始隐藏所有按钮，等待淡入动画
        SetButtonsInteractable(false);
    }

    /// <summary>
    /// 打开面板时播放淡入动画
    /// </summary>
    public override void OpenPanel(string name)
    {
        base.OpenPanel(name);
        StartCoroutine(FadeInAnimation());
    }

    /// <summary>
    /// 淡入动画协程 - 逐步显示UI元素
    /// </summary>
    private IEnumerator FadeInAnimation()
    {
        //初始透明
        canvasGroup.alpha = 0f;

        //等待相机
        yield return new WaitForSecondsRealtime(0.5f);

        //淡入标题
        float elapsed = 0f;
        while(elapsed < fadeInDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            canvasGroup.alpha = Mathf.Lerp(0f,1f,elapsed / fadeInDuration);
            yield return null;
        }
        canvasGroup.alpha = 1f;

        //依次显示按钮
        yield return new WaitForSecondsRealtime(buttonDelay);
        SetButtonsInteractable(true);
    }

    //设置按钮是否可交互
    private void SetButtonsInteractable(bool interactable)
    {
        if(startGameBtn != null) startGameBtn.interactable = interactable;
        if(settingsBtn != null) settingsBtn.interactable = interactable;
        if(quitGameBtn != null) quitGameBtn.interactable = interactable;
    }

    /// <summary>
    /// 开始游戏 - 加载主游戏场景或进入游戏状态
    /// </summary>
    private void OnStartGame()
    {
        Debug.Log("OnStartGame 被点击了！");
        //同一场景关闭主菜单，调用游戏逻辑
        StartCoroutine(StartGameTransition());
    }

    /// <summary>
    /// 开始游戏的过渡动画
    /// </summary>
    private IEnumerator StartGameTransition()
    {
        //禁用按钮防止重复点击
        SetButtonsInteractable(false);

        //淡出效果
        float elapsed = 0f;
        float fadeOutDuration = 1f;

        while(elapsed < fadeOutDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            canvasGroup.alpha = Mathf.Lerp(1f,0f,elapsed / fadeOutDuration);
            yield return null;
        }

        //通知GameManager游戏开始
        MainMenuCamera mainMenuCamera = FindObjectOfType<MainMenuCamera>();
        if(mainMenuCamera != null)
        {
            mainMenuCamera.TransitionToGameplay();
        }

         // 通知 GameManager 游戏开始
        // 如果 Instance 为 null，尝试在场景中重新查找（容错）
        if(GameManager.Instance == null)
        {
            Debug.LogWarning("GameManager.Instance 为 null，尝试重新查找...");
            GameManager found = FindObjectOfType<GameManager>();
            if(found != null)
            {
                Debug.LogWarning("找到了 GameManager: " + found.gameObject.name + "，但 Instance 丢失");
            }
            else
            {
                Debug.LogError("场景中找不到任何 GameManager！请检查是否被销毁");
            }
        }

        if(GameManager.Instance != null)
        {
            Debug.Log("正在调用 GameManager.StartGame()");
            GameManager.Instance.StartGame();
        }
        else
        {
            Debug.LogError("GameManager.Instance 为 null！StartGame 未被调用！");
        }

        //显示游戏HUD
        // UIManager.Instance.OpenPanel(UIconst.GameHUD);

        //关闭主菜单页面
        ClosePanel();
    }

    /// <summary>
    /// 打开设置面板（预留）
    /// </summary>
    private void OnSettings()
    {
        
    }
    
    /// <summary>
    /// 退出游戏
    /// </summary>
    private void OnQuitGame()
    {
        Application.Quit();
    }

}

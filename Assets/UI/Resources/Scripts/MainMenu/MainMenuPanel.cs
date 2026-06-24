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

    protected override void Awake()
    {
        base.Awake();
        InitUI();
    }

    private void OnEnable()
    {
        // 面板打开时播放背景音乐
        if (AudioManager.Instance != null)
        {
            Debug.Log("[MainMenuPanel] 播放主菜单 BGM");
            AudioManager.Instance.PlayMusic();
        }
        else
        {
            Debug.LogWarning("[MainMenuPanel] AudioManager.Instance 为 null！");
        }
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
            }
        }

        canvasGroup = GetComponent<CanvasGroup>();
        if(canvasGroup == null)
        {
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }

        //绑定按钮事件
        if(startGameBtn != null)
        {
            startGameBtn.onClick.AddListener(OnStartGame);
        }

        if(settingsBtn != null)
        {
            settingsBtn.onClick.AddListener(OnSettings);
        }

        if(quitGameBtn != null)
        {
            quitGameBtn.onClick.AddListener(OnQuitGame);
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

        // 通知菜单相机切换
        MainMenuCamera mainMenuCamera = FindObjectOfType<MainMenuCamera>();
        if(mainMenuCamera != null)
        {
            mainMenuCamera.TransitionToGameplay();
        }

        // 通知 GameManager 游戏开始
        if(GameManager.Instance != null)
        {
            GameManager.Instance.StartGame();
        }

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

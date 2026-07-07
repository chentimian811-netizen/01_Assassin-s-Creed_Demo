//------------------------------------------------------------
// ACDemo — 主菜单界面（GF UIFormLogic 版）
// 放置路径: Assets/GameMain/Scripts/UI/MainMenuForm.cs
// 对应 Prefab: Assets/UI/Prefabs/MainMenu/MainMenuForm.prefab
// 分组: Page（全屏覆盖，depth=1）
// 来源: 从 MainMenuPanel.cs 迁移，继承 UIFormLogic
//------------------------------------------------------------

using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityGameFramework.Runtime;

/// <summary>
/// 主菜单界面 — 游戏启动时的首屏全屏界面。
/// 包含：开始游戏、设置、退出游戏三个按钮，带淡入/淡出动画。
/// "开始游戏"点击后播放淡出动画，发送 LoginSuccessEventArgs 通知流程切换。
/// </summary>
public class MainMenuForm : UIFormLogic
{
    [Header("UI 引用")]
    [SerializeField] private Button startGameBtn;
    [SerializeField] private Button settingsBtn;
    [SerializeField] private Button quitGameBtn;
    [SerializeField] private Text titleText;

    [Header("动画设置")]
    [SerializeField] private CanvasGroup canvasGroup;  // Prefab 上必须挂载 CanvasGroup 组件
    [SerializeField] private float fadeInDuration = 1.5f;
    [SerializeField] private float buttonDelay = 0.3f;

    // ==================== UIFormLogic 生命周期 ====================

    protected override void OnOpen(object userData)
    {
        base.OnOpen(userData);

        // 绑定按钮事件
        if (startGameBtn != null) startGameBtn.onClick.AddListener(OnStartGame);
        if (settingsBtn != null) settingsBtn.onClick.AddListener(OnSettings);
        if (quitGameBtn != null) quitGameBtn.onClick.AddListener(OnQuitGame);

        // 初始隐藏按钮，等待淡入动画完成后才可交互
        SetButtonsInteractable(false);

        // 启动淡入动画
        StartCoroutine(FadeInAnimation());
    }

    protected override void OnClose(bool isShutdown, object userData)
    {
        // 清理按钮监听，防止内存泄漏
        if (startGameBtn != null) startGameBtn.onClick.RemoveListener(OnStartGame);
        if (settingsBtn != null) settingsBtn.onClick.RemoveListener(OnSettings);
        if (quitGameBtn != null) quitGameBtn.onClick.RemoveListener(OnQuitGame);

        base.OnClose(isShutdown, userData);
    }

    // ==================== 动画 ====================

    /// <summary>
    /// 淡入动画 — 标题和按钮依次显现。
    /// </summary>
    private IEnumerator FadeInAnimation()
    {
        canvasGroup.alpha = 0f;

        // 等一帧确保 Canvas 就绪（UIGroupHelper 需要一帧初始化）
        yield return null;

        // 淡入
        float elapsed = 0f;
        while (elapsed < fadeInDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            canvasGroup.alpha = Mathf.Lerp(0f, 1f, elapsed / fadeInDuration);
            yield return null;
        }
        canvasGroup.alpha = 1f;

        // 延迟后显示按钮
        yield return new WaitForSecondsRealtime(buttonDelay);
        SetButtonsInteractable(true);
    }

    /// <summary>
    /// 淡出动画 — 点击开始游戏后播放，完成后触发流程切换。
    /// </summary>
    private IEnumerator FadeOutAndStartGame()
    {
        SetButtonsInteractable(false);

        float elapsed = 0f;
        float fadeOutDuration = 1f;
        while (elapsed < fadeOutDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            canvasGroup.alpha = Mathf.Lerp(1f, 0f, elapsed / fadeOutDuration);
            yield return null;
        }
        canvasGroup.alpha = 0f;

        // 发送登录成功事件 → ProcedureMainMenu 收到后切换到 ProcedureLoading
        GameEntry.Event.Fire(this, LoginSuccessEventArgs.Create());
    }

    // ==================== 按钮回调 ====================

    /// <summary>
    /// 开始游戏 — 播放淡出动画，然后发送 LoginSuccessEventArgs。
    /// </summary>
    private void OnStartGame()
    {
        StartCoroutine(FadeOutAndStartGame());
    }

    /// <summary>
    /// 打开设置（阶段3 实现 SettingsForm）。
    /// </summary>
    private void OnSettings()
    {
        // TODO 阶段3: GameEntry.UI.OpenUIForm(UIPaths.SettingsForm, "Popup");
        Debug.Log("[MainMenuForm] 设置功能暂未实现");
    }

    /// <summary>
    /// 退出游戏。
    /// </summary>
    private void OnQuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // ==================== 工具方法 ====================

    private void SetButtonsInteractable(bool interactable)
    {
        if (startGameBtn != null) startGameBtn.interactable = interactable;
        if (settingsBtn != null) settingsBtn.interactable = interactable;
        if (quitGameBtn != null) quitGameBtn.interactable = interactable;
    }
}

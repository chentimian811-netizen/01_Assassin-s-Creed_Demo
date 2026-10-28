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

        // GF 的 DefaultUIFormHelper 用 SetParent 挂实例，根节点在「无父级」状态下 rect 是 0×0，
        // 实测会停在 0×0，于是居中的标题/按钮全部挤到同一点。这里显式铺满父 Canvas。
        ForceFillParentCanvas();

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
        // GF 关界面只是把实例挂起/复用，协程不会自己停；不停的话淡入会在关闭后继续改 alpha，
        // 下次打开就从一半的透明度开始。
        StopAllCoroutines();

        // 清理按钮监听，防止内存泄漏
        if (startGameBtn != null) startGameBtn.onClick.RemoveListener(OnStartGame);
        if (settingsBtn != null) settingsBtn.onClick.RemoveListener(OnSettings);
        if (quitGameBtn != null) quitGameBtn.onClick.RemoveListener(OnQuitGame);

        base.OnClose(isShutdown, userData);
    }

    /// <summary>
    /// 把根节点拉伸到父 Canvas（等价于锚点四向对齐 + 偏移全 0）。
    /// </summary>
    private void ForceFillParentCanvas()
    {
        RectTransform rect = transform as RectTransform;
        if (rect == null) return;

        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.localScale = Vector3.one;
    }

    // ==================== 动画 ====================

    /// <summary>
    /// 淡入动画 — 标题和按钮依次显现。
    /// </summary>
    private IEnumerator FadeInAnimation()
    {
        // 预制体上没挂 CanvasGroup 时直接放开按钮，不要让整屏卡在不可交互状态。
        if (canvasGroup == null)
        {
            SetButtonsInteractable(true);
            yield break;
        }

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
    /// 打开设置 — SettingsForm 还没建，先用 Toast 明确告知，避免点了像坏掉。
    /// </summary>
    private void OnSettings()
    {
        ToastService.Show("设置功能还没做");
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

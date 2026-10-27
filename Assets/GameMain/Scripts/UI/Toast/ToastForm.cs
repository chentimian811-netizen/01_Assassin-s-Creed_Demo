//------------------------------------------------------------
// ACDemo — 顶部提示条（GF UIFormLogic 版）
// 放置路径: Assets/GameMain/Scripts/UI/Toast/ToastForm.cs
// 对应 Prefab: Assets/UI/Prefabs/HUD/ToastForm.prefab
// 分组: Top（不暂停、不锁光标 —— 只显示，不拦输入）
// 来源: 由旧 ToastMessage(MonoBehaviour 单例 + 静态 Show) 迁移。
//       旧的实例是 Instantiate 到场景根、没有 Canvas 祖先，实际不可见；
//       迁移后挂在 GF 的 Top 组 Canvas 下，才真的能显示。
//
// 【为什么 ToastMessage 静态门面还留在本文件里】
//   预制体根节点的 MonoBehaviour 是按 .cs 的 GUID 引用的（7dde33d8…），
//   这个 GUID 跟着文件搬家一起保留下来，所以预制体不会丢脚本；
//   Unity 又要求 MonoBehaviour 的类名与文件名一致（ToastForm）。
//   而工程里有 20+ 处写的是 ToastMessage.Show(...)，为了「调用点一行不改」，
//   这里保留同名静态门面，内部转给 ToastService。
//------------------------------------------------------------

using UnityEngine;
using UnityEngine.UI;
using UnityGameFramework.Runtime;

/// <summary>
/// 顶部提示条界面。同一时间只有一条，后来的消息覆盖文本并重置计时。
/// </summary>
public class ToastForm : GameUIForm
{
    [SerializeField] private Text messageText;
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private float displayDuration = 2f;
    [SerializeField] private float fadeDuration = 0.3f;

    /// <summary>提示条不暂停游戏</summary>
    protected override bool IsModal => false;

    /// <summary>提示条不接管光标（否则每次弹提示都会把光标放出来）</summary>
    protected override bool LockCursor => false;

    /// <summary>
    /// 预制体根节点是 100×100 居中小块，子节点 Text 用绝对尺寸定位；
    /// 强制铺满父 Canvas 会把文字撑到全屏并改变观感，所以这里保持预制体自身布局。
    /// </summary>
    protected override bool FillParentCanvas => false;

    private enum Phase
    {
        None,
        FadeIn,
        Hold,
        FadeOut
    }

    private Phase m_Phase = Phase.None;
    private float m_PhaseElapsed;
    private int m_ConsumedVersion = -1;

    /// <summary>
    /// 打开时（首次显示）应用当前消息并开始淡入。
    /// </summary>
    protected override void OnOpenForm(object userData)
    {
        string message = userData as string;
        if (string.IsNullOrEmpty(message)) message = ToastService.Message;

        ApplyMessage(message);
        m_ConsumedVersion = ToastService.Version;
        EnterPhase(Phase.FadeIn);
    }

    /// <summary>
    /// 关闭时把计时/文本状态复位，避免池化复用后带着上一条消息的中间状态。
    /// </summary>
    protected override void OnCloseForm()
    {
        m_Phase = Phase.None;
        m_PhaseElapsed = 0f;
        m_ConsumedVersion = -1;
        SetAlpha(0f);
    }

    /// <summary>
    /// 用真实时间推进淡入 → 停留 → 淡出。
    /// 不用协程也不用 GF 的 OnUpdate：MonoBehaviour.Update 在暂停（timeScale=0）下照样跑，
    /// 而且在这里 CloseSelf 不会打断 GF 自己的界面遍历。
    /// </summary>
    private void Update()
    {
        float deltaTime = Time.unscaledDeltaTime;

        // 覆盖策略：ToastService 的版本号变了就换文本并重新计时（不叠第二个界面）
        if (ToastService.Version != m_ConsumedVersion)
        {
            m_ConsumedVersion = ToastService.Version;
            ApplyMessage(ToastService.Message);
            EnterPhase(Phase.FadeIn);
        }

        bool finished = false;

        switch (m_Phase)
        {
            case Phase.FadeIn:
                m_PhaseElapsed += deltaTime;
                SetAlpha(fadeDuration > 0f ? Mathf.Clamp01(m_PhaseElapsed / fadeDuration) : 1f);
                if (m_PhaseElapsed >= fadeDuration) EnterPhase(Phase.Hold);
                break;

            case Phase.Hold:
                m_PhaseElapsed += deltaTime;
                if (m_PhaseElapsed >= displayDuration) EnterPhase(Phase.FadeOut);
                break;

            case Phase.FadeOut:
                m_PhaseElapsed += deltaTime;
                SetAlpha(fadeDuration > 0f ? 1f - Mathf.Clamp01(m_PhaseElapsed / fadeDuration) : 0f);
                if (m_PhaseElapsed >= fadeDuration) finished = true;
                break;
        }

        if (finished)
        {
            m_Phase = Phase.None;
            SetAlpha(0f);
            // 淡出结束就关掉界面；下一条消息由 ToastService 重新打开（GF 会复用实例）
            CloseSelf();
        }
    }

    private void EnterPhase(Phase phase)
    {
        m_Phase = phase;
        m_PhaseElapsed = 0f;

        if (phase == Phase.FadeIn) SetAlpha(0f);
    }

    private void ApplyMessage(string message)
    {
        if (messageText != null) messageText.text = message ?? string.Empty;
    }

    private void SetAlpha(float alpha)
    {
        if (canvasGroup == null) return;

        canvasGroup.alpha = Mathf.Clamp01(alpha);
        // 提示条永远不拦点击（它在 Top 组，压在所有界面之上）
        canvasGroup.blocksRaycasts = false;
        canvasGroup.interactable = false;
    }
}

/// <summary>
/// 旧静态入口，保留给工程里 20+ 处调用点（一行不改）。
/// 新代码请直接用 <see cref="ToastService"/>。
/// </summary>
public static class ToastMessage
{
    public static void Show(string message)
    {
        ToastService.Show(message);
    }
}

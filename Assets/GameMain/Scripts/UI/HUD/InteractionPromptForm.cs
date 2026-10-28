using UnityEngine;
using UnityEngine.UI;
using UnityGameFramework.Runtime;

/// <summary>
/// 世界交互提示条（「按 E 点燃篝火」这类）。
///
/// 【为什么另做一个界面，而不是复用 PickupForm】
///   第一版图省事复用了武器拾取提示，结果实测「看不见」：PickupForm 的根节点没有背景图
///   （只有 CanvasRenderer + CanvasGroup），标题文字框净宽只有 120px、18 号白字，还贴在屏幕
///   右侧 —— 塞进「[E] 点燃篝火（bonfire_01）」这种长句会折成多行并溢出外框，
///   白字叠在亮场景上等于没有。交互提示是要玩家一眼看到并按 E 的东西，必须有自己的底色条。
///   PickupForm 继续只服务「地面武器拾取」（它要显示物品图标，两者形态不同）。
///
/// 【静态下单 + 界面轮询版本号】
///   GF 的界面是池化 + 异步打开的：调用方拿不到实例，也不能假设「这一刻已经开着了」。
///   所以 Show/Hide 只改静态状态并递增版本号，界面在自己的 Update 里比对版本号决定
///   「换内容 + 重新淡入」还是「淡出关闭」。与 PickupForm / ToastService 同一套做法。
///
/// 【不暂停、不锁光标】
///   提示显示的同一时刻玩家要能打、能滚、能转视角，否则一走近篝火就动不了。
/// </summary>
public class InteractionPromptForm : GameUIForm
{
    [Header("UI 绑定")]
    [SerializeField] private Text m_Text;
    [SerializeField] private CanvasGroup m_CanvasGroup;

    [Header("动画")]
    [Tooltip("淡入淡出时长（真实时间，不受卡肉/暂停影响）")]
    [SerializeField] private float m_FadeDuration = 0.15f;

    [Header("位置与尺寸（Inspector 直接调，改完 Play 就生效）")]
    [Tooltip("提示条中心距屏幕底部的距离（1920×1080 参考分辨率下的像素）")]
    [SerializeField] private float m_BottomOffset = 140f;

    [Tooltip("提示条宽度（参考分辨率像素）")]
    [SerializeField] private float m_BarWidth = 760f;

    [Tooltip("提示条高度（参考分辨率像素）")]
    [SerializeField] private float m_BarHeight = 72f;

    [Tooltip("打开时打一行布局日志（canvas / 父级 / 自身尺寸与坐标），定位跑偏时用")]
    [SerializeField] private bool m_LogLayout = true;

    // ==================== 静态下单 ====================

    private static string s_PendingText = string.Empty;
    private static int s_PendingVersion;
    private static bool s_CloseRequested;

    /// <summary>
    /// 显示提示。同一句话重复下单会被忽略（界面已经开着且内容没变）。
    /// </summary>
    public static void Show(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            Hide();
            return;
        }

        // 已经开着、内容没变、也没有待处理的关闭请求 → 什么都不用做
        if (UIRouter.IsOpen(UIPaths.InteractionPromptForm)
            && s_PendingText == text
            && !s_CloseRequested)
        {
            return;
        }

        s_PendingText = text;
        s_PendingVersion++;
        s_CloseRequested = false;

        // 这行日志是排查「提示不出现」的分界点：有它 = 交互层已发热到界面，
        // 没有它 = 玩家还没进交互范围（或交互物的 CanInteract 是 false）
        Debug.Log($"[Prompt] 显示交互提示：{text}");

        if (UIRouter.IsOpen(UIPaths.InteractionPromptForm)) return;
        UIRouter.Open(UIPaths.InteractionPromptForm, "Top");
    }

    /// <summary>请求淡出并关闭。界面可能还没创建出来，所以这里只置标记。</summary>
    public static void Hide()
    {
        if (string.IsNullOrEmpty(s_PendingText) && !UIRouter.IsOpen(UIPaths.InteractionPromptForm)) return;

        s_PendingText = string.Empty;
        s_CloseRequested = true;
    }

    /// <summary>关闭 Play 前清静态状态：关闭 Domain Reload 时静态字段会带进下一次 Play</summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        s_PendingText = string.Empty;
        s_PendingVersion = 0;
        s_CloseRequested = false;
    }

    // ==================== 生命周期 ====================

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

    /// <summary>提示条不暂停游戏</summary>
    protected override bool IsModal => false;

    /// <summary>提示条不接管光标（否则站到篝火旁就转不了视角、打不了架）</summary>
    protected override bool LockCursor => false;

    /// <summary>固定尺寸的小条：铺满父 Canvas 会把字撑到屏幕外</summary>
    protected override bool FillParentCanvas => false;

    protected override void OnOpenForm(object userData)
    {
        m_ConsumedVersion = s_PendingVersion;
        ApplyText(s_PendingText);
        ApplyLayout();

        m_Phase = Phase.FadeIn;
        m_PhaseElapsed = 0f;
        SetAlpha(0f);
    }

    /// <summary>
    /// 把提示条钉在「屏幕底部居中」。
    ///
    /// 【为什么不能只靠 prefab 里的锚点】
    ///   实测：界面实例的父级 rect 是 **0×0**，而锚点是按父级 rect 算的 →
    ///   0.5 的横向锚点也退化成 x = 0，760 宽的条子有一半在屏幕外
    ///   （第一次验收的截图：只剩「燃篝火」三个字压在武器槽上）。
    ///   所以这里改成「往上找一张尺寸正常的 Canvas，用它的矩形算世界坐标，直接设 position」，
    ///   父级多小都不影响；这也解释了第一版复用 `PickupForm`（锚点贴右边 −678）为什么整条跑到屏幕外。
    ///
    /// 【要挪位置/改大小】只改 Inspector 上的 m_BottomOffset / m_BarWidth / m_BarHeight，
    /// 不用动 prefab 的 RectTransform，也不用改代码。
    /// </summary>
    private void ApplyLayout()
    {
        RectTransform rect = transform as RectTransform;
        if (rect == null) return;

        // 1) 自身：底部居中，pivot 放底边 → m_BottomOffset 的语义就是「距屏幕底部多少」
        rect.anchorMin = new Vector2(0.5f, 0f);
        rect.anchorMax = new Vector2(0.5f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.localScale = Vector3.one;
        rect.sizeDelta = new Vector2(m_BarWidth, m_BarHeight);

        // 2) 位置：最上面那张「尺寸正常」的 Canvas 的底边 + m_BottomOffset
        RectTransform canvasRect = FindUsableCanvasRect();
        if (canvasRect != null)
        {
            Rect canvasLocalRect = canvasRect.rect;
            Vector3 canvasLocalPoint = new Vector3(canvasLocalRect.center.x, canvasLocalRect.yMin + m_BottomOffset, 0f);
            rect.position = canvasRect.TransformPoint(canvasLocalPoint);
        }
        else
        {
            // 连一张正常尺寸的 Canvas 都没有：退回锚点，至少别飞出屏幕布局逻辑
            rect.anchoredPosition = new Vector2(0f, m_BottomOffset);
        }

        if (!m_LogLayout) return;

        RectTransform parentRect = rect.parent as RectTransform;
        string canvasSize = canvasRect != null ? canvasRect.rect.size.ToString() : "没找到";
        string parentSize = parentRect != null ? parentRect.rect.size.ToString() : "无 RectTransform";
        Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(null, rect.position);

        Debug.Log($"[Prompt] 布局：canvas={canvasSize}  parent={parentSize}" +
                  $"  自身 size={rect.rect.size} screen={screenPoint}");
    }

    /// <summary>
    /// 向上找一张 rect 尺寸正常的 Canvas（GF 的组建出来可能是 0×0）。
    /// 找不到就返回 null，由调用方走兜底分支。
    /// </summary>
    private RectTransform FindUsableCanvasRect()
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        for (int depth = 0; canvas != null && depth < 6; depth++)
        {
            RectTransform rect = canvas.transform as RectTransform;
            if (rect != null && rect.rect.width > 1f && rect.rect.height > 1f) return rect;

            Transform parent = canvas.transform.parent;
            Canvas parentCanvas = parent != null ? parent.GetComponentInParent<Canvas>() : null;
            if (parentCanvas == null || parentCanvas == canvas) break;

            canvas = parentCanvas;
        }

        return null;
    }

    protected override void OnCloseForm()
    {
        m_Phase = Phase.None;
        m_PhaseElapsed = 0f;
        m_ConsumedVersion = -1;
        s_CloseRequested = false;
        SetAlpha(0f);
    }

    /// <summary>
    /// 用真实时间推进淡入/淡出。停在原地（Hold）时一直显示，直到交互层下 Hide。
    /// </summary>
    private void Update()
    {
        float deltaTime = Time.unscaledDeltaTime;

        // 换了内容（例如「点燃篝火」→「在篝火休息」）：换字并重新淡入
        if (m_ConsumedVersion != s_PendingVersion)
        {
            m_ConsumedVersion = s_PendingVersion;
            ApplyText(s_PendingText);
            m_Phase = Phase.FadeIn;
            m_PhaseElapsed = 0f;
            SetAlpha(0f);
            return;
        }

        if (s_CloseRequested && m_Phase != Phase.FadeOut && m_Phase != Phase.None)
        {
            m_Phase = Phase.FadeOut;
            m_PhaseElapsed = 0f;
        }

        switch (m_Phase)
        {
            case Phase.FadeIn:
                m_PhaseElapsed += deltaTime;
                SetAlpha(m_FadeDuration > 0f ? Mathf.Clamp01(m_PhaseElapsed / m_FadeDuration) : 1f);
                if (m_PhaseElapsed >= m_FadeDuration)
                {
                    m_Phase = Phase.Hold;
                    m_PhaseElapsed = 0f;
                }
                break;

            case Phase.FadeOut:
                m_PhaseElapsed += deltaTime;
                SetAlpha(m_FadeDuration > 0f ? 1f - Mathf.Clamp01(m_PhaseElapsed / m_FadeDuration) : 0f);
                if (m_PhaseElapsed >= m_FadeDuration)
                {
                    m_Phase = Phase.None;
                    SetAlpha(0f);
                    CloseSelf();
                }
                break;
        }
    }

    private void ApplyText(string text)
    {
        if (m_Text != null) m_Text.text = text ?? string.Empty;
    }

    private void SetAlpha(float alpha)
    {
        if (m_CanvasGroup == null) return;

        m_CanvasGroup.alpha = Mathf.Clamp01(alpha);
        // 提示条永远不拦点击
        m_CanvasGroup.blocksRaycasts = false;
        m_CanvasGroup.interactable = false;
    }
}

//------------------------------------------------------------
// ACDemo — 地面武器拾取提示（GF UIFormLogic 版）
// 放置路径: Assets/GameMain/Scripts/UI/Pickup/PickupForm.cs
// 对应 Prefab: Assets/UI/Prefabs/Pickup/PickupForm.prefab
// 分组: Popup（**非模态**：不暂停、不锁光标 —— 它只是提示，站在武器旁边仍然要能打）
// 来源: 由旧 PickupPopup(BasePanel + UIManager) 迁移。
//
// 【顺带修掉的旧行为】旧 UIManager.OpenPanel 对每个面板都做 CursorManager.AddLock，
//   于是「走到武器旁边」会解锁光标、CameraManager 停转、攻击/瞄准被 IsGameplayFocused 拦掉 ——
//   站在武器边上打不了架。现在走 LockCursor=false，不再碰光标。
//
// 【为什么用静态请求，而不是让 WeaponPickup 直接开关界面】
//   GF 的界面是池化 + 异步打开的：WeaponPickup 拿不到实例，也不能假设「这一刻开着」。
//   而且武器触发器可能重叠（A 的 Exit 与 B 的 Enter 顺序不确定），
//   直接 Open/Close 会出现「淡出途中来了新目标 → 关闭被吞掉」。
//   所以改成「下单 + 轮询」：ShowPrompt/HidePrompt 只改静态状态，
//   界面在自己的 Update 里比对版本号决定换内容还是淡出，时序天然解耦。
//------------------------------------------------------------

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class PickupForm : GameUIForm
{
    [Header("UI 绑定")]
    [SerializeField] private Image iconImage;
    [SerializeField] private Text titleText;
    [SerializeField] private CanvasGroup canvasGroup;

    [Header("动画")]
    [SerializeField] private float fadeDuration = 0.2f;

    [Header("缩放")]
    [Tooltip("GF 的 DefaultUIFormHelper 建实例时会把 localScale 重置成 1，这里补回预制体原本的 2 倍")]
    [SerializeField] private float formScale = 2f;

    /// <summary>武器图标缓存：DataRepository.GetItemIcon 每次都要走 Resources，缓存一份</summary>
    private static readonly Dictionary<int, Sprite> m_WeaponIconCache = new Dictionary<int, Sprite>();

    /// <summary>最近一次 ShowPrompt 的数据</summary>
    private static PickupFormArgs m_PendingArgs;

    /// <summary>提示数据版本号，界面靠它判断要不要换内容</summary>
    private static int m_PendingVersion;

    /// <summary>是否有待处理的「关掉」请求</summary>
    private static bool m_CloseRequested;

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

    /// <summary>提示条不接管光标（否则站在武器旁边就动不了镜头、打不了架）</summary>
    protected override bool LockCursor => false;

    /// <summary>预制体根是 200×80 靠右居中的小块，套用父 Canvas 会把它撑满屏</summary>
    protected override bool FillParentCanvas => false;

    // ==================== 对外静态接口（WeaponPickup 调） ====================

    /// <summary>
    /// 显示「可拾取」提示。已经开着时只换内容并重新淡入，不会再开第二个界面。
    /// </summary>
    public static void ShowPrompt(int weaponId, string weaponName)
    {
        m_PendingArgs = new PickupFormArgs { WeaponId = weaponId, WeaponName = weaponName };
        m_PendingVersion++;
        m_CloseRequested = false;

        // 已经开着：界面在 Update 里看到版本号变化会自己换内容
        if (UIRouter.IsOpen(UIPaths.PickupForm)) return;

        UIRouter.Open(UIPaths.PickupForm, "Popup", m_PendingArgs);
    }

    /// <summary>
    /// 请求淡出并关闭。界面可能还没创建出来，所以这里只置标记，真正的关闭在界面 Update 里做。
    /// </summary>
    public static void HidePrompt()
    {
        m_CloseRequested = true;
    }

    /// <summary>预加载全部武器图标（首次打开时调用一次）</summary>
    public static void PreloadWeaponIcons()
    {
        if (m_WeaponIconCache.Count > 0) return;
        if (DataRepository.ItemTable == null) return;

        foreach (var kv in DataRepository.ItemTable)
        {
            if (kv.Value == null || kv.Value.Type != GameConst.PackageTypeWeapon) continue;

            var icon = DataRepository.GetItemIcon(kv.Key);
            if (icon != null) m_WeaponIconCache[kv.Key] = icon;
        }
    }

    /// <summary>清空图标缓存（Luban 重新导出后由工具或调试入口调用）</summary>
    public static void ClearCache()
    {
        m_WeaponIconCache.Clear();
    }

    // ==================== 生命周期 ====================

    /// <summary>
    /// 每次打开都跑：补回缩放、按当前请求刷内容、开始淡入。
    /// </summary>
    protected override void OnOpenForm(object userData)
    {
        // GF 的 DefaultUIFormHelper.CreateUIForm 会执行 transform.localScale = one，
        // 这个提示条原本是靠预制体上的 2 倍缩放显示大小的，这里补回来
        transform.localScale = Vector3.one * formScale;

        PreloadWeaponIcons();

        PickupFormArgs args = userData as PickupFormArgs;
        if (args == null) args = m_PendingArgs;

        m_ConsumedVersion = m_PendingVersion;
        ApplyArgs(args);

        m_Phase = Phase.FadeIn;
        m_PhaseElapsed = 0f;
        SetAlpha(0f);
    }

    /// <summary>
    /// 每次关闭都跑：清状态，避免池化复用后带着上一次的淡出进度。
    /// </summary>
    protected override void OnCloseForm()
    {
        m_Phase = Phase.None;
        m_PhaseElapsed = 0f;
        m_ConsumedVersion = -1;
        m_CloseRequested = false;
        SetAlpha(0f);
    }

    /// <summary>
    /// 用真实时间推进淡入 / 淡出。待在原地（Hold）时不自动关闭 ——
    /// 提示要一直显示到玩家离开拾取范围（WeaponPickup 会调 HidePrompt）。
    /// </summary>
    private void Update()
    {
        float deltaTime = Time.unscaledDeltaTime;

        // 换了目标（比如从一把武器走到另一把）：换内容并重新淡入，优先级最高
        if (m_PendingVersion != m_ConsumedVersion)
        {
            m_ConsumedVersion = m_PendingVersion;
            ApplyArgs(m_PendingArgs);
            m_Phase = Phase.FadeIn;
            m_PhaseElapsed = 0f;
            SetAlpha(0f);
            return;
        }

        // 收到关闭请求 → 转淡出
        if (m_CloseRequested && m_Phase != Phase.FadeOut && m_Phase != Phase.None)
        {
            m_Phase = Phase.FadeOut;
            m_PhaseElapsed = 0f;
        }

        switch (m_Phase)
        {
            case Phase.FadeIn:
                m_PhaseElapsed += deltaTime;
                SetAlpha(fadeDuration > 0f ? Mathf.Clamp01(m_PhaseElapsed / fadeDuration) : 1f);
                if (m_PhaseElapsed >= fadeDuration)
                {
                    m_Phase = Phase.Hold;
                    m_PhaseElapsed = 0f;
                }
                break;

            case Phase.FadeOut:
                m_PhaseElapsed += deltaTime;
                SetAlpha(fadeDuration > 0f ? 1f - Mathf.Clamp01(m_PhaseElapsed / fadeDuration) : 0f);
                if (m_PhaseElapsed >= fadeDuration)
                {
                    m_Phase = Phase.None;
                    SetAlpha(0f);
                    CloseSelf();
                }
                break;
        }
    }

    private void ApplyArgs(PickupFormArgs args)
    {
        if (args == null)
        {
            if (titleText != null) titleText.text = string.Empty;
            if (iconImage != null) iconImage.gameObject.SetActive(false);
            return;
        }

        if (titleText != null) titleText.text = args.WeaponName;

        if (iconImage != null)
        {
            if (m_WeaponIconCache.TryGetValue(args.WeaponId, out Sprite icon))
            {
                iconImage.sprite = icon;
                iconImage.gameObject.SetActive(true);
            }
            else
            {
                iconImage.gameObject.SetActive(false);
            }
        }
    }

    private void SetAlpha(float alpha)
    {
        if (canvasGroup == null) return;

        canvasGroup.alpha = Mathf.Clamp01(alpha);
        // 提示条永远不拦点击
        canvasGroup.blocksRaycasts = false;
        canvasGroup.interactable = false;
    }
}

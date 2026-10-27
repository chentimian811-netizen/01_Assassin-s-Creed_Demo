using GameFramework.UI;
using UnityEngine;
using UnityEngine.UI;
using UnityGameFramework.Runtime;

/// <summary>
/// UI 组自举：保证 GF 的每个 UIGroup 都真的有一张 Canvas。
///
/// 【为什么需要】
///   1. 本工程的 GameFramework.prefab 用的是 DefaultUIGroupHelper，
///      它的 SetDepth 是空实现，不会创建 Canvas / GraphicRaycaster / CanvasScaler；
///      ACUIGroupHelper 虽然写了，但预制体上并没有挂它。
///   2. TestScene 的入口被覆盖成 ProcedureGame，跳过了 ProcedureLaunch 里的
///      AddUIGroup，导致 "HUD" 等组根本不存在。
///   结果就是 OpenUIForm 没有可挂载的 Canvas，界面全部不可见。
///
/// 这里在打开界面前补齐：缺组就 AddUIGroup；组存在但没有 Canvas 就补一张，
/// 并按 depth 设置 sortingOrder（HUD 0 / Page 1 / Popup 2 / Top 3 / Loading 4）。
///
/// 为什么不直接改 GameFramework.prefab：该目录标注为勿改，
/// 运行时自举可以同时兼容「入口是 ProcedureLaunch」和「入口是 ProcedureGame」两种场景。
/// </summary>
public static class UIGroupBootstrap
{
    /// <summary>与 ACUIGroupHelper.DepthFactor 保持一致</summary>
    public const int DepthFactor = 10000;

    private static readonly string[] GroupNames = { "HUD", "Page", "Popup", "Top", "Loading" };

    /// <summary>确保全部 5 个 UI 组存在且各自有 Canvas（可重复调用）</summary>
    public static void EnsureAll()
    {
        if (GameEntry.UI == null)
        {
            Debug.LogError("[UIGroupBootstrap] GameEntry.UI 为空，UI 组无法初始化");
            return;
        }

        for (int i = 0; i < GroupNames.Length; i++)
        {
            EnsureGroup(GroupNames[i], i);
        }
    }

    /// <summary>确保单个 UI 组存在，并补 Canvas / CanvasScaler / GraphicRaycaster</summary>
    public static void EnsureGroup(string groupName, int depth)
    {
        if (GameEntry.UI == null) return;

        if (!GameEntry.UI.HasUIGroup(groupName))
        {
            if (!GameEntry.UI.AddUIGroup(groupName, depth))
            {
                Debug.LogError($"[UIGroupBootstrap] 添加 UI 组失败：{groupName}");
                return;
            }
        }

        IUIGroup group = GameEntry.UI.GetUIGroup(groupName);
        MonoBehaviour helper = group != null ? group.Helper as MonoBehaviour : null;
        if (helper == null)
        {
            Debug.LogError($"[UIGroupBootstrap] UI 组 {groupName} 没有可用的 Helper");
            return;
        }

        GameObject groupObject = helper.gameObject;

        Canvas canvas = groupObject.GetComponent<Canvas>();
        if (canvas == null) canvas = groupObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        // +100：保证压过场景里那套旧 Canvas（sortingOrder 0），又不打乱各组相对顺序
        canvas.sortingOrder = DepthFactor * depth + 100;
        canvas.enabled = true;

        if (groupObject.GetComponent<GraphicRaycaster>() == null)
        {
            groupObject.AddComponent<GraphicRaycaster>();
        }

        CanvasScaler scaler = groupObject.GetComponent<CanvasScaler>();
        if (scaler == null) scaler = groupObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        RectTransform rect = groupObject.GetComponent<RectTransform>();
        if (rect != null)
        {
            // 关键：不能把 sizeDelta 设成 0。
            // 这张 Canvas 是运行时 AddComponent 出来的，父级 "UI Form Instances" 只是普通 Transform，
            // 没有 Canvas 的自动屏幕尺寸驱动时，rect 会一直是 0×0：
            // 顶左锚点的血条全部被算到屏幕下方（日志里的 screen y 为负就是这个原因），
            // 底左锚点的槽位恰好落在 (56,56)，所以只有四槽能看见。
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;

            float scale = canvas.scaleFactor > 0.01f ? canvas.scaleFactor : 1f;
            rect.sizeDelta = new Vector2(Screen.width / scale, Screen.height / scale);
            rect.localScale = Vector3.one;
        }

        Canvas.ForceUpdateCanvases();
    }
}

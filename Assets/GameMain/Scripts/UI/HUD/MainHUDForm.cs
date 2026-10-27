//------------------------------------------------------------
// ACDemo — 常驻游戏 HUD 界面（GF UIFormLogic 版）
// 放置路径: Assets/GameMain/Scripts/UI/HUD/MainHUDForm.cs
// 对应 Prefab: Assets/UI/Prefabs/HUD/MainHUDForm.prefab
// 分组: HUD（深度 0，不遮挡其他 UI）
// 来源: 从 MainPanel.cs 迁移，仅保留常驻 HUD 职责
//       暂停/菜单功能 → PauseForm（阶段3）
//       Tab 按钮 → TopRightTabForm（阶段3）
//       菜单按钮 → MainMenuBarForm（阶段3）
//------------------------------------------------------------

using UnityEngine;
using UnityEngine.UI;
using UnityGameFramework.Runtime;

/// <summary>
/// 常驻游戏 HUD 界面。
/// 血条、小地图、准星等作为子节点挂在 Prefab 下。
/// 子面板（背包/抽卡）通过 OpenPanelEventArgs 事件驱动打开，HUD 保持显示。
/// </summary>
public class MainHUDForm : UIFormLogic
{
    [SerializeField] private Button packageBtn;    // 背包按钮
    [SerializeField] private Button lotteryBtn;    // 抽卡按钮


    protected override void OnOpen(object userData)
    {
        base.OnOpen(userData);

        ForceFillParentCanvas();

        if (packageBtn != null) packageBtn.onClick.AddListener(OnOpenPackage);
        if (lotteryBtn != null) lotteryBtn.onClick.AddListener(OnOpenLottery);
    }

    /// <summary>
    /// 把 Form 根节点强制拉伸到父 Canvas。
    ///
    /// 为什么预制体已经是 stretch + sizeDelta 0 还要再设一次：
    /// GF 的 DefaultUIFormHelper 用 SetParent 把实例挂到 UI 组 Canvas 下，
    /// 但实例在“无父级”状态下的 rect 是 0×0，运行时实测根节点仍停在 0×0，
    /// 于是所有顶部锚点的子物体都被算到画布下方（血条不可见）。
    /// 这里显式给一次 offsetMin/offsetMax = 0，保证根节点真的铺满 Canvas。
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

    protected override void OnClose(bool isShutdown, object userData)
    {
        if (packageBtn != null) packageBtn.onClick.RemoveListener(OnOpenPackage);
        if (lotteryBtn != null) lotteryBtn.onClick.RemoveListener(OnOpenLottery);

        base.OnClose(isShutdown, userData);
    }

    /// <summary>
    /// 打开背包面板 — 通过 GF 事件解耦，不直接依赖 PackageForm。
    /// </summary>
    private void OnOpenPackage()
    {
        GameEntry.Event.Fire(this, OpenPanelEventArgs.Create(UIPaths.PackageForm, "Popup"));
    }

    /// <summary>
    /// 打开抽卡面板。
    /// </summary>
    private void OnOpenLottery()
    {
        GameEntry.Event.Fire(this, OpenPanelEventArgs.Create(UIPaths.LotteryForm, "Popup"));
    }
}

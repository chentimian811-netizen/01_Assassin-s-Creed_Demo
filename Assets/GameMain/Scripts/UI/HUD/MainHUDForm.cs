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
    [Header("HUD 功能按钮（阶段3 迁移到 TopRightTabForm）")]
    [SerializeField] private Button packageBtn;    // 背包按钮
    [SerializeField] private Button lotteryBtn;    // 抽卡按钮

    // ==================== UIFormLogic 生命周期 ====================

    protected override void OnOpen(object userData)
    {
        base.OnOpen(userData);

        if (packageBtn != null) packageBtn.onClick.AddListener(OnOpenPackage);
        if (lotteryBtn != null) lotteryBtn.onClick.AddListener(OnOpenLottery);
    }

    protected override void OnClose(bool isShutdown, object userData)
    {
        if (packageBtn != null) packageBtn.onClick.RemoveListener(OnOpenPackage);
        if (lotteryBtn != null) lotteryBtn.onClick.RemoveListener(OnOpenLottery);

        base.OnClose(isShutdown, userData);
    }

    // ==================== 按钮回调 ====================

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

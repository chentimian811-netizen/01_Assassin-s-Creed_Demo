using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityGameFramework.Runtime;
using ProcedureOwner = GameFramework.Fsm.IFsm<GameFramework.Procedure.IProcedureManager>;

/// <summary>
/// 游戏流程。
/// 进入 SampleScene 后打开常驻 HUD（血条、小地图、Tab 栏、菜单栏）。
/// 监听菜单命令事件处理暂停/返回主菜单。
/// </summary>
public class ProcedureGame : ProcedureBase
{
    public override bool UseNativeDialog => false;

    private int? m_HUDFormId = null;
    private int? m_TabFormId = null;
    private int? m_MenuBarFormId = null;

    protected override void OnEnter(ProcedureOwner procedureOwner)
    {
        base.OnEnter(procedureOwner);

        //打开常驻HUD组件（HUD组 depth0 不相互遮挡)
        m_HUDFormId = GameEntry.UI.OpenUIForm(UIPaths.MainHUDForm,"HUD");
        m_TabFormId = GameEntry.UI.OpenUIForm(UIPaths.TopRightTabForm,"HUD");
        m_MenuBarFormId = GameEntry.UI.OpenUIForm(UIPaths.MainMenuBarForm,"HUD");

        //恢复游戏时间（防止从暂停返回时 timeScale 仍为 0）
        Time.timeScale = 1f;
        //订阅菜单命令事件
        GameEntry.Event.Subscribe(MenuCommandEventArgs.EventId, OnMenuCommand);
    }

    protected override void OnLeave(ProcedureOwner procedureOwner, bool isShutdown)
    {
        base.OnLeave(procedureOwner, isShutdown);

        // 关闭所有 HUD
        if (m_HUDFormId.HasValue) GameEntry.UI.CloseUIForm(m_HUDFormId.Value);
        if (m_TabFormId.HasValue) GameEntry.UI.CloseUIForm(m_TabFormId.Value);
        if (m_MenuBarFormId.HasValue) GameEntry.UI.CloseUIForm(m_MenuBarFormId.Value);

        base.OnLeave(procedureOwner, isShutdown);
    }

    /// <summary>
    /// 处理菜单命令事件
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void OnMenuCommand(object sender, GameFramework.Event.GameEventArgs e)
    {
        MenuCommandEventArgs args = e as MenuCommandEventArgs;
        if (args == null) return;

        switch (args.Command)
        {
            case "ReturnToMainMenu":
                // 返回主菜单：加载 MainScene
                ChangeState<ProcedureLoading>(null);
                break;
            case "Pause":
                // 暂停：打开 PauseForm（Popup 组），冻结游戏
                Time.timeScale = 0f;
                GameEntry.UI.OpenUIForm(UIPaths.PauseForm, "Popup");
                break;
            case "Resume":
                // 继续：关闭 PauseForm，恢复游戏
                Time.timeScale = 1f;
                break;
        }
    }
}

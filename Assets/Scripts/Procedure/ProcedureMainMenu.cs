using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityGameFramework.Runtime;
using ProcedureOwner = GameFramework.Fsm.IFsm<GameFramework.Procedure.IProcedureManager>;

/// <summary>
/// 主菜单流程
/// 打开全屏主菜单界面，订阅登录成功事件，收到事件后切换到 Loading 流程。
/// </summary>
public class ProcedureMainMenu : ProcedureBase
{
    public override bool UseNativeDialog =>false;

    private int? m_MainMenuFormId = null;

    protected override void OEnter(ProcedureOwner procedureOwner)
    {
        base.OnEnter(procedureOwner);

        //打开主菜单界面（Page组 全屏覆盖）
        m_MainMenuFormId = GameEntry.UI.OpenUIForm(UIPaths.MainMenuForm,"Page");

        //订阅登录成功事件
        GameEntry.Event.Subscribe(LoginSuccessEventArgs.EventId,OnLoginSuccess);
    }

    protected override void OLeave(ProcedureOwner procedureOwner, bool isShutdown)
    {
        //取消订阅 防止内存泄露
        GameEntry.Event.Unsubscribe(LoginSuccessEventArgs.EventId, OnLoginSuccess);

        if (m_MainMenuFormId.HasValue)
        {
            GameEntry.UI.CloseUIForm(m_MainMenuFormId.Value);
        }

        base.OnLeave(procedureOwner, isShutdown);
    }
    /// <summary>
    /// 登录成功回调 — 切换到场景加载流程。
    /// </summary>
    /// </summary>
    private void OnLoginSuccess(object sender, GameFramework.Event.GameEventArgs e)
    {
        ChangeState<ProcedureLoading>(null);
    }
}
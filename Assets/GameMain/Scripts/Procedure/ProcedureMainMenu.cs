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
    private int? m_MainMenuFormId = null;
    private ProcedureOwner m_ProcedureOwner = null;
    private bool m_IsTransitioning = false;
    protected override void OnEnter(ProcedureOwner procedureOwner)
    {
        base.OnEnter(procedureOwner);
        m_ProcedureOwner = procedureOwner;
        m_IsTransitioning = false;

        //主菜单需要鼠标操作
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        //打开主菜单界面（Page组 全屏覆盖）
        m_MainMenuFormId = GameEntry.UI.OpenUIForm(UIPaths.MainMenuForm,"Page");

        //订阅登录成功事件
        GameEntry.Event.Subscribe(LoginSuccessEventArgs.EventId,OnLoginSuccess);
    }

    protected override void OnLeave(ProcedureOwner procedureOwner, bool isShutdown)
    {
        //取消订阅 防止内存泄露
        GameEntry.Event.Unsubscribe(LoginSuccessEventArgs.EventId, OnLoginSuccess);
    
        if (m_MainMenuFormId.HasValue)
        {
            GameEntry.UI.CloseUIForm(m_MainMenuFormId.Value);
        }

        // 相机过渡：禁用主菜单相机，为游戏相机让路
        MainMenuCamera menuCamera = Object.FindObjectOfType<MainMenuCamera>();
        if (menuCamera != null)
        {
            menuCamera.TransitionToGameplay();
        }

        m_ProcedureOwner = null;
        base.OnLeave(procedureOwner, isShutdown);
    }
    /// <summary>
    /// 登录成功回调 — 切换到场景加载流程。
    /// </summary>
    /// </summary>
    private void OnLoginSuccess(object sender, GameFramework.Event.GameEventArgs e)
    {
        // 防重入：防止快速双击触发两次 ChangeState
        if (m_IsTransitioning) return;
        m_IsTransitioning = true;

        // 设置加载目标：主菜单 → 游戏关卡
        ProcedureLoading.TargetScene = ScenePaths.MainScene;
        ProcedureLoading.NextProcedureType = typeof(ProcedureGame).Name;

        ChangeState<ProcedureLoading>(m_ProcedureOwner);
    }
}
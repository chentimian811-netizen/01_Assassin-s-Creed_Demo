//------------------------------------------------------------
// ACDemo — 主菜单流程
// 放置路径: Assets/GameMain/Scripts/Procedure/ProcedureMainMenu.cs
//------------------------------------------------------------

using GameFramework.Event;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityGameFramework.Runtime;
using ProcedureOwner = GameFramework.Fsm.IFsm<GameFramework.Procedure.IProcedureManager>;

/// <summary>
/// 主菜单流程。
///
/// 【职责】
///   1. 进入「菜单模式」：玩家与相机都不再响应输入，避免主菜单后面角色还能跑；
///   2. 打开全屏 MainMenuForm（Page 组），订阅 LoginSuccessEventArgs；
///   3. 收到登录成功 → ProcedureLoading 重载场景 → ProcedureGame。
///
/// 【为什么不在 OnLeave 恢复玩家/相机】
///   菜单 → 游戏走的是「重载场景」：旧实例整体销毁，新实例由 ProcedureGame.SetupGameSystems
///   统一激活（玩家 / 相机 / 光标）。在这里恢复反而会和那次激活打架。
///
/// 【为什么相机还开着】
///   菜单背景想看到关卡（角色站在原地的定帧），所以只停掉视角输入，不关相机。
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

        // 1. 菜单模式：玩家与相机停手
        EnterMenuMode();

        // 2. 主菜单需要鼠标操作
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        // 3. 打开主菜单（Page 组，全屏覆盖）
        m_MainMenuFormId = GameEntry.UI.OpenUIForm(UIPaths.MainMenuForm, "Page");

        // 4. 订阅登录成功
        GameEntry.Event.Subscribe(LoginSuccessEventArgs.EventId, OnLoginSuccess);
    }

    protected override void OnLeave(ProcedureOwner procedureOwner, bool isShutdown)
    {
        GameEntry.Event.Unsubscribe(LoginSuccessEventArgs.EventId, OnLoginSuccess);

        if (m_MainMenuFormId.HasValue)
        {
            GameEntry.UI.CloseUIForm(m_MainMenuFormId.Value);
            m_MainMenuFormId = null;
        }

        // 有主菜单相机实例时做一次过渡；当前场景没有实例，这里是空操作。
        MainMenuCamera menuCamera = Object.FindObjectOfType<MainMenuCamera>();
        if (menuCamera != null)
        {
            menuCamera.TransitionToGameplay();
        }

        m_ProcedureOwner = null;
        base.OnLeave(procedureOwner, isShutdown);
    }

    /// <summary>
    /// 菜单模式：关玩家输入 + 关相机视角输入。
    ///
    /// 只关 PlayerInput 不够：PlayerController 的移动/攻击回调还会走 acceptInput，
    /// 两个一起关才算「角色完全不响应」。相机保留渲染，只停掉输入消费。
    /// </summary>
    private static void EnterMenuMode()
    {
        PlayerController player = Object.FindObjectOfType<PlayerController>(true);
        if (player != null)
        {
            player.acceptInput = false;

            PlayerInput playerInput = player.GetComponent<PlayerInput>();
            if (playerInput != null) playerInput.enabled = false;
        }

        CameraManager.Instance?.SetLookEnabled(false);
    }

    /// <summary>
    /// 登录成功：进入 Loading（重载开发场景），随后 ProcedureGame 接管。
    /// 重复点击由 m_IsTransitioning 挡住。
    /// </summary>
    private void OnLoginSuccess(object sender, GameEventArgs e)
    {
        if (m_IsTransitioning) return;
        m_IsTransitioning = true;

        ProcedureLoading.TargetScene = GameStartupConfig.Instance.DevScenePath;
        ProcedureLoading.NextProcedureType = typeof(ProcedureGame).Name;
        ChangeState<ProcedureLoading>(m_ProcedureOwner);
    }
}

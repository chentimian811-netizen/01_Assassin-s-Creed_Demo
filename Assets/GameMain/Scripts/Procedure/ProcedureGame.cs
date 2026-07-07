using GameFramework.Event;
using UnityEngine;
using UnityGameFramework.Runtime;
using ProcedureOwner = GameFramework.Fsm.IFsm<GameFramework.Procedure.IProcedureManager>;

/// <summary>
/// 游戏流程。
/// 游戏关卡加载完成后进入。打开常驻 HUD，初始化游戏系统。
/// 监听菜单命令（暂停/返回主菜单）和面板打开事件。
/// </summary>
public class ProcedureGame : ProcedureBase
{
    private int? m_HUDFormId = null;
    // 阶段3 启用：
    // private int? m_TabFormId = null;
    // private int? m_MenuBarFormId = null;
    private ProcedureOwner m_ProcedureOwner = null;

    protected override void OnEnter(ProcedureOwner procedureOwner)
    {
        base.OnEnter(procedureOwner);
        m_ProcedureOwner = procedureOwner;

        // 1. 初始化游戏系统（替代老 GameManager.StartGame()）
        SetupGameSystems();

        // 2. 打开常驻 HUD（HUD 组，depth=0）
        m_HUDFormId = GameEntry.UI.OpenUIForm(UIPaths.MainHUDForm, "HUD");
        // 阶段3: m_TabFormId = GameEntry.UI.OpenUIForm(UIPaths.TopRightTabForm, "HUD");
        // 阶段3: m_MenuBarFormId = GameEntry.UI.OpenUIForm(UIPaths.MainMenuBarForm, "HUD");

        // 3. 恢复游戏时间
        Time.timeScale = 1f;

        // 4. 订阅事件
        GameEntry.Event.Subscribe(MenuCommandEventArgs.EventId, OnMenuCommand);
        GameEntry.Event.Subscribe(OpenPanelEventArgs.EventId, OnOpenPanel);
    }

    protected override void OnLeave(ProcedureOwner procedureOwner, bool isShutdown)
    {
        GameEntry.Event.Unsubscribe(MenuCommandEventArgs.EventId, OnMenuCommand);
        GameEntry.Event.Unsubscribe(OpenPanelEventArgs.EventId, OnOpenPanel);

        // 关闭所有 HUD
        CloseFormIfOpen(ref m_HUDFormId);
        // 阶段3: CloseFormIfOpen(ref m_TabFormId);
        // 阶段3: CloseFormIfOpen(ref m_MenuBarFormId);

        m_ProcedureOwner = null;
        base.OnLeave(procedureOwner, isShutdown);
    }

    // ==================== 游戏系统初始化 ====================

    /// <summary>
    /// 激活玩家、游戏相机、锁定光标。
    /// 替代老 GameManager.StartGame() 的逻辑，由 Procedure 统一管理。
    /// </summary>
    private static void SetupGameSystems()
    {
        // 启用玩家（includeInactive=true 确保能找到被禁用的对象）
        PlayerController player = Object.FindObjectOfType<PlayerController>(true);
        if (player != null)
        {
            player.gameObject.SetActive(true);
            player.acceptInput = true;
        }

        // 激活 FreeLook 相机
        CameraManager camManager = Object.FindObjectOfType<CameraManager>();
        if (camManager != null && camManager.freeLook != null)
        {
            camManager.freeLook.gameObject.SetActive(true);
            camManager.ResetFreeLookCamera();
        }

        // 锁定鼠标，进入游戏操作模式
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    // ==================== 事件回调 ====================

    /// <summary>
    /// 处理菜单命令（来自 MainMenuBarForm / PauseForm 等）。
    /// </summary>
    private void OnMenuCommand(object sender, GameEventArgs e)
    {
        if (e is not MenuCommandEventArgs args) return;

        switch (args.Command)
        {
            case "ReturnToMainMenu":
                // 设置反向加载目标和下一流程
                ProcedureLoading.TargetScene = ScenePaths.MainMenu;
                ProcedureLoading.NextProcedureType = typeof(ProcedureMainMenu).Name;
                ChangeState<ProcedureLoading>(m_ProcedureOwner);
                break;

            case "Pause":
                Time.timeScale = 0f;
                // 阶段3: GameEntry.UI.OpenUIForm(UIPaths.PauseForm, "Popup");
                break;

            case "Resume":
                Time.timeScale = 1f;
                // 阶段3: 关闭 PauseForm
                break;
        }
    }

    /// <summary>
    /// 处理打开面板事件 — 解耦 HUD 按钮与具体面板类型。
    /// </summary>
    private static void OnOpenPanel(object sender, GameEventArgs e)
    {
        if (e is not OpenPanelEventArgs args) return;
        GameEntry.UI.OpenUIForm(args.FormPath, args.GroupName);
    }

    // ==================== 工具方法 ====================

    private void CloseFormIfOpen(ref int? formId)
    {
        if (formId.HasValue)
        {
            GameEntry.UI.CloseUIForm(formId.Value);
            formId = null;
        }
    }
}

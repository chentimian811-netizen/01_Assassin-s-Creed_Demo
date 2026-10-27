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
    private int? m_EnemyBarsFormId = null;
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

        // 1.5 UI 组自举：TestScene 的入口覆盖成 ProcedureGame，跳过了 ProcedureLaunch 的
        //     AddUIGroup；同时 GameFramework.prefab 用的是不会建 Canvas 的默认 Helper。
        //     不补齐的话 OpenUIForm 没有 Canvas 可挂，HUD 永远不可见。
        UIGroupBootstrap.EnsureAll();

        // 2. 打开常驻 HUD（HUD 组，depth=0）
        m_HUDFormId = GameEntry.UI.OpenUIForm(UIPaths.MainHUDForm, "HUD");
        // 敌人头顶血条独立成一个 HUD 层：与玩家状态条解耦，方便单独开关
        m_EnemyBarsFormId = GameEntry.UI.OpenUIForm(UIPaths.EnemyHealthBarsForm, "HUD");
        // 阶段3: m_TabFormId = GameEntry.UI.OpenUIForm(UIPaths.TopRightTabForm, "HUD");
        // 阶段3: m_MenuBarFormId = GameEntry.UI.OpenUIForm(UIPaths.MainMenuBarForm, "HUD");

        Debug.Log($"[ProcedureGame] HUD 已打开：MainHUD={m_HUDFormId}，EnemyBars={m_EnemyBarsFormId}");

        // 3. UI 层的暂停/光标计数复位。
        //    这两份计数是静态的：上一次 Play（关闭 Domain Reload 时）或上一次流程残留下来，
        //    会让这一局开局就停住或光标乱。OnLeave 已经 CloseAll 过界面，这里只是兜底。
        UIPauseService.Reset();
        UICursorService.Reset();

        // 3.5 确保卡肉/暂停唯一所有者存在，并恢复游戏时间
        HitStopManager.EnsureExists();
        HitStopManager.Instance?.Resume();

        // 4. 订阅事件
        GameEntry.Event.Subscribe(MenuCommandEventArgs.EventId, OnMenuCommand);
        GameEntry.Event.Subscribe(OpenPanelEventArgs.EventId, OnOpenPanel);
    }

    protected override void OnLeave(ProcedureOwner procedureOwner, bool isShutdown)
    {
        GameEntry.Event.Unsubscribe(MenuCommandEventArgs.EventId, OnMenuCommand);
        GameEntry.Event.Unsubscribe(OpenPanelEventArgs.EventId, OnOpenPanel);

        // 关闭所有 HUD
        CloseFormIfOpen(ref m_EnemyBarsFormId);
        CloseFormIfOpen(ref m_HUDFormId);
        // 阶段3: CloseFormIfOpen(ref m_TabFormId);
        // 阶段3: CloseFormIfOpen(ref m_MenuBarFormId);

        // 关闭所有弹窗。GF 的 UI 是 DontDestroyOnLoad 的，切流程时不会自动清场；
        // 若留着模态界面（暂停 + 锁光标），下一局一开始就停住。
        UIRouter.CloseAll();

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
                // 单场景开发期：重载 TestScene 相当于重开关卡。
                // 之后自建 MainMenu 场景后，改回 TargetScene=ScenePaths.MainMenu / Next=ProcedureMainMenu
                ProcedureLoading.TargetScene = ScenePaths.TestScene;
                ProcedureLoading.NextProcedureType = typeof(ProcedureGame).Name;
                ChangeState<ProcedureLoading>(m_ProcedureOwner);
                break;

            case "Pause":
                HitStopManager.EnsureExists();
                HitStopManager.Instance?.Pause();
                // 阶段3: GameEntry.UI.OpenUIForm(UIPaths.PauseForm, "Popup");
                break;

            case "Resume":
                HitStopManager.Instance?.Resume();
                // 阶段3: 关闭 PauseForm
                break;
        }
    }

    /// <summary>
    /// 处理打开面板事件 — 解耦 HUD 按钮与具体面板类型。
    /// 必须走 UIRouter：GF 的 OpenUIForm 不查重，HUD 按钮 + 反引号各按一次
    /// 会实例化出两个背包界面。
    /// </summary>
    private static void OnOpenPanel(object sender, GameEventArgs e)
    {
        if (e is not OpenPanelEventArgs args) return;
        UIRouter.Open(args.FormPath, args.GroupName);
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

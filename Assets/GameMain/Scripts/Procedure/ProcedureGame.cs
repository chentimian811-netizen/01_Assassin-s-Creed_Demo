using GameFramework.Event;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityGameFramework.Runtime;
using ProcedureOwner = GameFramework.Fsm.IFsm<GameFramework.Procedure.IProcedureManager>;

/// <summary>
/// 游戏流程。
/// 游戏关卡加载完成后进入。打开常驻 HUD，初始化游戏系统。
/// 监听菜单命令（暂停/返回主菜单）和面板打开事件。
/// </summary>
public class ProcedureGame : ProcedureBase
{
    // 菜单命令字面量。发出方：PauseForm / DeathForm（GF Event）、PlayerController.GetPauseInput。
    // 集中成常量是因为「拼错命令名」的表现是「按了没反应」，在 Console 里完全看不出原因。
    public const string CommandPause = "Pause";
    public const string CommandResume = "Resume";
    public const string CommandReturnToMainMenu = "ReturnToMainMenu";

    private int? m_HUDFormId = null;
    private int? m_EnemyBarsFormId = null;
    // 待接（TopRightTabForm / MainMenuBarForm 预制体尚未创建）：
    // private int? m_TabFormId = null;
    // private int? m_MenuBarFormId = null;
    private ProcedureOwner m_ProcedureOwner = null;

    protected override void OnEnter(ProcedureOwner procedureOwner)
    {
        base.OnEnter(procedureOwner);
        m_ProcedureOwner = procedureOwner;

        // 1. 初始化游戏系统（替代老 GameManager.StartGame()）
        SetupGameSystems();

        // 1.5 UI 组自举：正常入口（ProcedureLaunch）已经补过，这里保留一次兜底 ——
        //     直接以 ProcedureGame 为入口（调试、或以后关卡流程直接重入）时也不会缺 Canvas。
        //     GameFramework.prefab 用的是不会建 Canvas 的默认 Helper，缺了界面就永远不可见。
        UIGroupBootstrap.EnsureAll();

        // 2. 打开常驻 HUD（HUD 组，depth=0）
        m_HUDFormId = GameEntry.UI.OpenUIForm(UIPaths.MainHUDForm, "HUD");
        // 敌人头顶血条独立成一个 HUD 层：与玩家状态条解耦，方便单独开关
        m_EnemyBarsFormId = GameEntry.UI.OpenUIForm(UIPaths.EnemyHealthBarsForm, "HUD");
        // 待接: m_TabFormId = GameEntry.UI.OpenUIForm(UIPaths.TopRightTabForm, "HUD");
        // 待接: m_MenuBarFormId = GameEntry.UI.OpenUIForm(UIPaths.MainMenuBarForm, "HUD");

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
        m_ProcedureOwner = null;

        // 停机时 GF 按「事件 → 流程 → UI」逐层销毁（BaseComponent.OnDestroy →
        // GameFrameworkEntry.Shutdown → ProcedureManager.Shutdown → 这里），此时 EventManager /
        // UIManager 可能已经先关掉了：再退订会抛 "Event '...' not exists specified handler"，
        // 再关界面会抛 "Can not find UI form"。停机本来就不需要清理，直接交给基类。
        if (isShutdown)
        {
            base.OnLeave(procedureOwner, isShutdown);
            return;
        }

        GameEntry.Event.Unsubscribe(MenuCommandEventArgs.EventId, OnMenuCommand);
        GameEntry.Event.Unsubscribe(OpenPanelEventArgs.EventId, OnOpenPanel);

        // 关闭所有 HUD
        CloseFormIfOpen(ref m_EnemyBarsFormId);
        CloseFormIfOpen(ref m_HUDFormId);
        // 待接: CloseFormIfOpen(ref m_TabFormId);
        // 待接: CloseFormIfOpen(ref m_MenuBarFormId);

        // 关闭所有弹窗。GF 的 UI 是 DontDestroyOnLoad 的，切流程时不会自动清场；
        // 若留着模态界面（暂停 + 锁光标），下一局一开始就停住。
        UIRouter.CloseAll();

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

            // ProcedureMainMenu 在菜单期间会关掉 PlayerInput；单场景阶段不重载场景，
            // 这里必须显式恢复（幂等）。
            PlayerInput playerInput = player.GetComponent<PlayerInput>();
            if (playerInput != null) playerInput.enabled = true;

            // 死亡 → 回主菜单 → 再开始：复活 + 回出生点（见 PlayerController.ResetForNewRun）
            player.ResetForNewRun();
        }

        // 激活游戏相机：优先新几何跟随 Rig；未配置新 Rig 时回退旧 FreeLook（见 CameraManager 兼容路由）
        CameraManager camManager = Object.FindObjectOfType<CameraManager>();
        if (camManager != null)
        {
            camManager.SetGameplayCameraActive(true);
            // 菜单模式会关掉视角输入，进游戏时重新打开，否则鼠标转不了视角
            camManager.SetLookEnabled(true);
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
            case CommandReturnToMainMenu:
                // 不在这里 CloseAll：OnLeave 里先按 formId 关 HUD、再 CloseAll 清弹窗。
                // 反过来先 CloseAll 的话，HUD / 敌人血条虽然由 Procedure 直接打开，但 GF 的
                // 「打开成功」事件会把它们登记进 UIRouter，于是会被一起关掉；随后 OnLeave
                // 再按旧 formId 关一次就抛 "Can not find UI form"。
                //
                // 单场景阶段不重载场景：GF 的 LoadScene 是 additive 加载，对当前场景再加载
                // 一次会多出一整套框架/玩家/敌人实例。直接回菜单流程，玩家状态由
                // ProcedureGame.SetupGameSystems → ResetForNewRun 在下次开始时复位。
                // GF-4 拆出真实主菜单/关卡场景后，这里改回 ProcedureLoading。
                ChangeState<ProcedureMainMenu>(m_ProcedureOwner);
                break;

            case CommandPause:
                // 暂停不在这里写 timeScale：PauseForm 继承 GameUIForm，打开即
                // UIPauseService.Push → HitStopManager.Pause（计数归零才恢复）。
                UIRouter.Open(UIPaths.PauseForm, "Popup");
                break;

            case CommandResume:
                // 关闭时 GameUIForm 反向 Pop：只有最后一个模态界面关掉才真正 Resume
                UIRouter.Close(UIPaths.PauseForm);
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

    /// <summary>
    /// 关闭 Procedure 自己开的界面（HUD / 敌人血条）。
    ///
    /// 这类界面虽然不走 UIRouter 打开，但 GF 的「打开成功」事件会把它们登记进路由，
    /// 所以 UIRouter.CloseAll() 可能已经关过它们；此时直接 CloseUIForm 会抛
    /// "Can not find UI form"。关之前先确认界面还在。
    /// </summary>
    private void CloseFormIfOpen(ref int? formId)
    {
        if (!formId.HasValue) return;

        int id = formId.Value;
        formId = null;

        if (GameEntry.UI != null && GameEntry.UI.HasUIForm(id))
        {
            GameEntry.UI.CloseUIForm(id);
        }
    }
}

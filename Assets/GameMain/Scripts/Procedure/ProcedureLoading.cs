using GameFramework;
using GameFramework.Event;
using UnityGameFramework.Runtime;
using ProcedureOwner = GameFramework.Fsm.IFsm<GameFramework.Procedure.IProcedureManager>;

/// <summary>
/// 场景加载流程。
/// 打开 LoadingForm，通过 GF 事件监听异步加载进度和完成。
/// </summary>
public class ProcedureLoading : ProcedureBase
{
    public static string TargetScene { get; set; } = string.Empty;

    public static string NextProcedureType { get; set; } = typeof(ProcedureGame).Name;
    private int? m_LoadingFormId = null;

    private ProcedureOwner m_ProcedureOwner = null;
    protected override void OnEnter(ProcedureOwner procedureOwner)
    {
        base.OnEnter(procedureOwner);

        m_ProcedureOwner = procedureOwner;

        // 校验目标场景（防御性编程）
        if (string.IsNullOrEmpty(TargetScene))
        {
            Log.Error("ProcedureLoading: TargetScene is not set.");
            TargetScene = ScenePaths.MainScene;
        }

        // 打开加载界面（Loading 组，盖住一切）
        m_LoadingFormId = GameEntry.UI.OpenUIForm(UIPaths.LoadingForm, "Loading");

        // 订阅场景加载事件
        GameEntry.Event.Subscribe(LoadSceneUpdateEventArgs.EventId, OnLoadSceneUpdate);
        GameEntry.Event.Subscribe(LoadSceneSuccessEventArgs.EventId, OnLoadSceneSuccess);

        // 异步加载场景
        GameEntry.Scene.LoadScene(TargetScene, this);
    }

    protected override void OnLeave(ProcedureOwner procedureOwner, bool isShutdown)
    {
        // 取消订阅
        GameEntry.Event.Unsubscribe(LoadSceneUpdateEventArgs.EventId, OnLoadSceneUpdate);
        GameEntry.Event.Unsubscribe(LoadSceneSuccessEventArgs.EventId, OnLoadSceneSuccess);

        if (m_LoadingFormId.HasValue)
        {
            GameEntry.UI.CloseUIForm(m_LoadingFormId.Value);
            m_LoadingFormId = null;
        }

        // 清理静态状态，防止下次误用
        TargetScene = string.Empty;
        NextProcedureType = typeof(ProcedureGame).Name;

        m_ProcedureOwner = null;

        base.OnLeave(procedureOwner, isShutdown);
    }

    /// <summary>
    /// 场景加载进度更新 — 广播进度事件给 LoadingForm。
    /// </summary>
    private void OnLoadSceneUpdate(object sender, GameEventArgs e)
    {
        if (e is not LoadSceneUpdateEventArgs args) return;

        GameEntry.Event.Fire(this, LoadProgressEventArgs.Create(args.Progress));
    }

    /// <summary>
    /// 场景加载完成 — 切换到目标流程。
    /// </summary>
    private void OnLoadSceneSuccess(object sender, GameEventArgs e)
    {
        if (NextProcedureType == typeof(ProcedureMainMenu).Name)
            ChangeState<ProcedureMainMenu>(m_ProcedureOwner);
        else
            ChangeState<ProcedureGame>(m_ProcedureOwner);
    }
}

using GameFramework.Event;
using UnityGameFramework.Runtime;
using ProcedureOwner = GameFramework.Fsm.IFsm<GameFramework.Procedure.IProcedureManager>;

/// <summary>
/// 场景加载流程。
/// 打开 LoadingForm，通过 GF 事件监听异步加载进度和完成。
/// </summary>
public class ProcedureLoading : ProcedureBase
{
    public override bool UseNativeDialog => false;

    private string m_TargetScene = string.Empty;
    private int? m_LoadingFormId = null;

    protected override void OnEnter(ProcedureOwner procedureOwner)
    {
        base.OnEnter(procedureOwner);

        m_TargetScene = "Assets/Scenes/MainScene.unity";

        // 打开加载界面（Loading 组，盖住一切）
        m_LoadingFormId = GameEntry.UI.OpenUIForm(UIPaths.LoadingForm, "Loading");

        // 订阅场景加载事件
        GameEntry.Event.Subscribe(LoadSceneUpdateEventArgs.EventId, OnLoadSceneUpdate);
        GameEntry.Event.Subscribe(LoadSceneSuccessEventArgs.EventId, OnLoadSceneSuccess);

        // 异步加载场景
        GameEntry.Scene.LoadScene(m_TargetScene, this);
    }

    protected override void OnLeave(ProcedureOwner procedureOwner, bool isShutdown)
    {
        // 取消订阅
        GameEntry.Event.Unsubscribe(LoadSceneUpdateEventArgs.EventId, OnLoadSceneUpdate);
        GameEntry.Event.Unsubscribe(LoadSceneSuccessEventArgs.EventId, OnLoadSceneSuccess);

        if (m_LoadingFormId.HasValue)
        {
            GameEntry.UI.CloseUIForm(m_LoadingFormId.Value);
        }

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
        ChangeState<ProcedureGame>(null);
    }
}

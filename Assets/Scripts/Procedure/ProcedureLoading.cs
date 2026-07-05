using UnityEngine;
using UnityGameFramework.Runtime;
using ProcedureOwner = GameFramework.Fsm.IFsm<GameFramework.Procedure.IProcedureManager>;

/// <summary>
/// 场景加载流程。
/// 打开 LoadingForm，异步加载目标场景，每帧更新进度。
/// </summary>
public class ProcedureLoading : ProcedureBase
{
    public override bool UseNativeDialog => false;

    /// <summary>
    /// 要加载的目标场景路径。
    /// </summary>
    private string m_TargetScene = string.Empty;

     /// <summary>
    /// 加载完成后的目标流程类型名。
    /// </summary>
    private string m_NextProcedure = string.Empty;

    private int?m_LoadingFormId= null;
    private float m_Progress = 0f;

    protected override void OnEnter(ProcedureOwner procedureOwner)
    {
        base.OnEnter(procedureOwner);

        //从上一流程传入的场景参数读取目标
        //默认 加载SampleScene -> ProcedureGame
        m_TargetScene = "Assets/Scenes/MainScene.unity";
        m_NextProcedure = "ProcedureGame";

        //打开加载界面（Loading 组，盖住一切）
        m_LoadingFormId = GameEntry.UI.OpenUIForm(UIPaths.LoadingForm, "Loading");

        //异步加载场景
        GameEntry.Scene.LoadScene(m_TargetScene,this);
    }

    protected override void OnUpdate(ProcedureOwner procedureOwner, float elapseSeconds, float realElapseSeconds)
    {
        base.OnUpdate(procedureOwner, elapseSeconds, realElapseSeconds);

        //获取加载进度
        m_Progress = GameEntry.Scene.GetSceneLoadProgress(m_TargetScene);

        //广播加载进度事件（LoadingForm订阅此事件更新进度条)
        GameEntry.Event.Fire(this, LoadProgressEventArgs.Create(m_Progress));

        if (m_Progress >= 1f)
        {
            //加载完成 切换到目标流程
            ChangeState(procedureOwner,m_NextProcedure);
        }

    }

    protected override void OnLeave(ProcedureOwner procedureOwner, bool isShutdown)
    {
        if (m_LoadingFormId.HasValue)
        {
            GameEntry.UI.CloseUIForm(m_LoadingFormId.Value);
        }

        base.OnLeave(procedureOwner, isShutdown);
    }
    
    /// <summary>
    /// 根据流程类型名切换状态。
    /// </summary>
    private void ChangeState(ProcedureOwner procedureOwner, string procedureName)
    {
        switch (procedureName)
        {
            case "ProcedureGame":
                ChangeState<ProcedureGame>(procedureOwner);
                break;
            case "ProcedureMainMenu":
                ChangeState<ProcedureMainMenu>(procedureOwner);
                break;
            default:
                ChangeState<ProcedureMainMenu>(procedureOwner);
                break;
        }
    }
}

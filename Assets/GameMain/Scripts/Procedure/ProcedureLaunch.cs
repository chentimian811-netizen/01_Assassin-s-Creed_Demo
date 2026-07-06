using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityGameFramework.Runtime;
using ProcedureOwner = GameFramework.Fsm.IFsm<GameFramework.Procedure.IProcedureManager>;

/// <summary>
/// 启动流程
/// 仅执行一次 注册UI分组 然后切换到主菜单流程
/// </summary>
public class ProcedureLaunch : ProcedureBase
{
    public override bool UseNativeDialog => true;

    protected override void OnEnter(ProcedureOwner procedureOwner)
    {
        base.OnEnter(procedureOwner);
        // 注册 UIGroup（分组深度决定渲染顺序，框架跨场景持久化）
        // Depth 值 × ACUIGroupHelper.DepthFactor(10000) = Canvas Sorting Order
        GameEntry.UI.AddUIGroup("HUD",    0);
        GameEntry.UI.AddUIGroup("Page",   1);
        GameEntry.UI.AddUIGroup("Popup",  2);
        GameEntry.UI.AddUIGroup("Top",    3);
        GameEntry.UI.AddUIGroup("Loading",4);

        // 注册完毕，切换到主菜单流程
        ChangeState<ProcedureMainMenu>(procedureOwner);
    }
}
using UnityGameFramework.Runtime;
using ProcedureOwner = GameFramework.Fsm.IFsm<GameFramework.Procedure.IProcedureManager>;

/// <summary>
/// 启动流程 —— 游戏入口，只进入一次。
/// 职责：补齐 UI 组 → 读启动开关 → 分发到主菜单或直接进游戏。
///
/// 【两条路径为什么都要保留】
///   开发期改一行数值就要 Play 验证，每次先过一遍主菜单是纯浪费；
///   而正式流程（GF-4）需要「主菜单 → Loading → 关卡」的完整链路。
///   所以入口固定成这里，走哪条路由 GameStartupConfig.skipMainMenu 决定，
///   不再像 GF-3 之前那样靠「在场景里把入口覆盖成 ProcedureGame」来跳过。
/// </summary>
public class ProcedureLaunch : ProcedureBase
{
    protected override void OnEnter(ProcedureOwner procedureOwner)
    {
        base.OnEnter(procedureOwner);

        // 1. 补齐 UI 组与各自的 Canvas。
        //    GameFramework.prefab 用的是 DefaultUIGroupHelper（SetDepth 是空实现，不建 Canvas），
        //    所以「组 + Canvas」统一由 UIGroupBootstrap 负责（长期补丁，见其类注释）。
        //    以前这段是 ProcedureLaunch 自己 AddUIGroup，没有 Canvas，界面挂不上去。
        UIGroupBootstrap.EnsureAll();

        // 2. 读启动开关（Resources/Config/GameStartupConfig.asset，缺失时回落开发期默认值）
        GameStartupConfig.EnsureLoaded();

        // 3. 分发流程
        if (GameStartupConfig.Instance.SkipMainMenu)
        {
            // 开发期：单场景直达，行为与 GF-3 之前完全一致
            ChangeState<ProcedureGame>(procedureOwner);
            return;
        }

        // 正式流程：主菜单 → Loading → 关卡（场景拆好后由 GF-4 收口）
        ChangeState<ProcedureMainMenu>(procedureOwner);
    }
}

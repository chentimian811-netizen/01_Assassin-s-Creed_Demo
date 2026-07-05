//------------------------------------------------------------
// ACDemo — 流程基类
//------------------------------------------------------------

using GameFramework.Procedure;

/// <summary>
/// 所有 Procedure 的抽象基类。
/// </summary>
public abstract class ProcedureBase : GameFramework.Procedure.ProcedureBase
{
    /// <summary>
    /// 获取流程是否使用原生对话框。
    /// 在资源更新完成前的特殊流程中，可返回 true 使用原生对话框提示消息。
    /// </summary>
    public abstract bool UseNativeDialog { get; }
}

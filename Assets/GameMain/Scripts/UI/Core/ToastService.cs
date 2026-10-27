using UnityEngine;
using UnityGameFramework.Runtime;

/// <summary>
/// Toast 静态入口：全工程唯一的「弹一条提示」方法。
///
/// 【覆盖策略】同一时间只保留一条提示。
///   已经开着 ToastForm 时不重复打开，只把 Message 换掉、Version 自增；
///   ToastForm 在自己的 Update 里比对 Version，发现变了就换文本并重新计时。
///   这样连续触发「金币不足」之类的提示不会叠出多个界面，也不会出现两条重叠的淡入淡出。
///
/// 【为什么用版本号而不是直接调表单方法】
///   表单可能还没创建出来（GF 的 OpenUIForm 是异步的，返回时实例还没建好），
///   所以调用方不能假设「这一刻已经有 ToastForm 可以调」。版本号 + 轮询把时序解耦：
///   表单什么时候起来，第一次 Update 就会读到最新的一条。
/// </summary>
public static class ToastService
{
    /// <summary>当前要显示的文本</summary>
    public static string Message { get; private set; } = string.Empty;

    /// <summary>消息版本号。每 Show 一次自增，ToastForm 靠它判断要不要换文本重计时</summary>
    public static int Version { get; private set; }

    /// <summary>
    /// 显示一条提示。空字符串直接忽略。
    /// </summary>
    public static void Show(string message)
    {
        if (string.IsNullOrEmpty(message)) return;

        // GF 未就绪（编辑器里直接调、或流程还在启动）时不要抛异常，丢一条警告即可
        if (GameEntry.UI == null)
        {
            Debug.LogWarning($"[ToastService] GameEntry.UI 为空，提示被丢弃：{message}");
            return;
        }

        Message = message;
        Version++;

        // 已经开着：交给 ToastForm 自己比对 Version，这里不再开第二个
        if (UIRouter.IsOpen(UIPaths.ToastForm)) return;

        UIRouter.Open(UIPaths.ToastForm, "Top", message);
    }

    /// <summary>清空当前文本（不影响已经显示的界面）</summary>
    public static void Clear()
    {
        Message = string.Empty;
    }

    /// <summary>
    /// 关闭 Domain Reload 时静态字段会跨 Play 会话存活，进入 Play 前必须清空。
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Message = string.Empty;
        Version = 0;
    }
}

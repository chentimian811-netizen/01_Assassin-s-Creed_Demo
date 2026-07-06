using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using GameFramework.Event;
using GameFramework;

/// <summary>
/// Toast 通知事件参数。
/// 任意系统发送此事件即可在屏幕顶部显示短暂通知。
/// </summary>
public class ToastEventArgs : GameEventArgs
{
    public static readonly int EventId = typeof(ToastEventArgs).GetHashCode();

    public override int Id => EventId;

    /// <summary>
    /// 通知消息文本。
    /// </summary>
    public string Message { get; private set; }

    public override void Clear()
    {
        Message = string.Empty;
    }

    public static ToastEventArgs Create(string message)
    {
        ToastEventArgs args = ReferencePool.Acquire<ToastEventArgs>();
        args.Message = message;
        return args;
    }
}

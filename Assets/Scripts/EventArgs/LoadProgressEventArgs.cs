using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using GameFramework.Event;
using GameFramework;


/// <summary>
/// 场景加载进度事件参数 每帧由ProcedureLoading广播;
/// </summary>
public class LoadProgressEventArgs : GameEventArgs
{
    public static readonly int EventId = typeof(LoadProgressEventArgs).GetHashCode();

    public override int Id => EventId;

    /// <summary>
    /// 加载进度（0-1）
    /// </summary>
    public float Progress{get;private set;}

    public override void Clear()
    {
        Progress = 0f;
    }

    public static LoadProgressEventArgs Create(float progress)
    {
        LoadProgressEventArgs args = ReferencePool.Acquire<LoadProgressEventArgs>();
        args.Progress = progress;
        return args;
    }
}

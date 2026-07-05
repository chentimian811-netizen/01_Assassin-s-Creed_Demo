using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using GameFramework.Event;
using GameFramework;

/// <summary>
/// 打开面板事件参数
/// 用于解耦面板打开逻辑 发送事件而非直接调用 UIComponent
/// </summary>
public class OpenPanelEventArgs : GameEventArgs
{
    public static readonly int EventId = typeof(OpenPanelEventArgs).GetHashCode();

    public override int Id => EventId;

    /// <summary>
    /// 面板预制体路径（如UIPaths.ShopForm)
    /// </summary>
    public string FormPath{get; private set;}

    /// <summary>
    /// UIGroup 名称（HUD/Page/Popup/Top/Loading）。
    /// </summary>
    public string GroupName{get;private set;}

    public override void Clear()
    {
        FormPath = string.Empty;
        GroupName = string.Empty;
    }

    public static OpenPanelEventArgs Create(string formPath,string groupName)
    {
        OpenPanelEventArgs args = ReferencePool.Acquire<OpenPanelEventArgs>();
        args.FormPath = formPath;
        args.GroupName = groupName;
        return args;
    }
}

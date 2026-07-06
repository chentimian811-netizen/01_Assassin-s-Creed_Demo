using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using GameFramework.Event;
using GameFramework;

/// <summary>
/// 菜单命令事件参数。
/// MainMenuBarForm/PauseForm 通过此事件向 ProcedureGame 发送菜单操作。
/// </summary>
public class MenuCommandEventArgs : GameEventArgs
{
    public static readonly int EventId = typeof(MenuCommandEventArgs).GetHashCode();

    public override int Id => EventId;

    /// <summary>
    /// 命令字符串："ReturnToMainMenu"、"Pause"、"Resume"、"Settings"。
    /// </summary>
    public string Command { get; private set; }

    public override void Clear()
    {
        Command = string.Empty;
    }

    public static MenuCommandEventArgs Create(string command)
    {
        MenuCommandEventArgs args = ReferencePool.Acquire<MenuCommandEventArgs>();
        args.Command = command;
        return args;
    }
}

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using GameFramework.Event;
using GameFramework;

/// <summary>
/// 登录事件参数 用户在主菜单点击 开始游戏时触发
/// </summary>
public class LoginEventArgs : GameEventArgs
{  
    /// <summary>
    /// 事件ID 通过类型HashCode保证唯一
    /// </summary>
   public static readonly int EvenId = typeof(LoginEventArgs).GetHashCode();

    public override int Id => EvenId;

    /// <summary>
    /// 账号
    /// </summary>
    public string Account {get;private set;}

    /// <summary>
    /// 密码
    /// </summary>
    public string Password{get;private set;}

    public override void Clear()
    {
        Account = string.Empty;
        Password = string.Empty;
    }

    public static LoginEventArgs Create(string account,string password)
    {
        LoginEventArgs args = ReferencePool.Acquire<LoginEventArgs>();
        args.Account = account;
        args.Password = password;
        return args;
    }
}

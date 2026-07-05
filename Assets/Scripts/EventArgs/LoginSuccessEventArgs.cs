using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using GameFramework.Event;
using GameFramework;

/// <summary>
/// 登录成功事件参数 验证通过后触发 通知流程切换到Loading
/// </summary>
public class LoginSuccessEventArgs :  GameEventArgs
{
    public static readonly int EventId =typeof(LoginSuccessEventArgs).GetHashCode();

    public override int Id => EventId;

    public override void Clear()
    {
        
    }

    public static LoginSuccessEventArgs Create()
    {
         return ReferencePool.Acquire<LoginSuccessEventArgs>();
    }
}

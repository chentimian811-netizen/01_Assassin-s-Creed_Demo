using UnityEngine;

/// <summary>
/// 日志门面。热路径（命中/飞行每帧）必须走这里，
/// 用条件编译在非 Editor 构建里彻底移除，避免字符串插值产生 GC。
/// </summary>
public static class GameLog
{
    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    [System.Diagnostics.Conditional("ACDEMO_VERBOSE")]
    public static void Combat(string msg) => Debug.Log(msg);
}

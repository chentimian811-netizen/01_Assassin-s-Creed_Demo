using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// UI 光标锁的引用计数服务（GF UIForm 体系专用）。
///
/// 【为什么需要这一层】
///   旧 <see cref="UIManager"/> 是「每个面板自己 AddLock/RemoveLock」，而且
///   ClosePanel 里 RemoveLock 被写了两遍（同一 key 进 HashSet 是幂等的，凑巧没暴雷）。
///   换到 GF 之后界面会被池化复用，一旦某个界面在 OnClose 里漏掉一次 Unlock，
///   或者在另一个界面的关闭路径上误删了别人的锁，就会出现「关掉面板光标还锁着」
///   或者「还开着面板光标就没了」。所以统一走这里：
///   按 key 计数，只有最后一个持锁界面关闭时，才真正把光标还给游戏。
///
/// 【约定】
///   1. 只有 <see cref="GameUIForm"/> 调用本服务，业务脚本不要再直接调 CursorManager.AddLock；
///   2. Lock / Unlock 必须成对，key 用界面类型名（GameUIForm.CursorKey 的默认实现）；
///   3. 同一 key 重复 Lock 不重复计数（幂等），重复 Unlock 是空操作。
/// </summary>
public static class UICursorService
{
    /// <summary>当前持有光标锁的 key 集合。HashSet 天然去重，重复 Lock 同一 key 不会叠加</summary>
    private static readonly HashSet<string> m_LockKeys = new HashSet<string>();

    /// <summary>当前锁数量；为 0 表示光标处于游戏操作模式（锁定 + 隐藏）</summary>
    public static int LockCount => m_LockKeys.Count;

    /// <summary>是否持有指定 key 的光标锁</summary>
    public static bool IsLocked(string key)
    {
        return !string.IsNullOrEmpty(key) && m_LockKeys.Contains(key);
    }

    /// <summary>
    /// 申请光标锁。第一个持锁者生效，后续只是增加计数。
    /// </summary>
    public static void Lock(string key)
    {
        if (string.IsNullOrEmpty(key))
        {
            Debug.LogWarning("[UICursorService] Lock 的 key 为空，已忽略");
            return;
        }

        // 已经持锁：不再重复下发，避免 CursorManager 里同一 key 的语义被反复覆盖
        if (!m_LockKeys.Add(key)) return;

        CursorManager.Instance.AddLock(key);
    }

    /// <summary>
    /// 释放光标锁。只有真正持锁的 key 才会被移除，避免误删别人的锁。
    /// </summary>
    public static void Unlock(string key)
    {
        if (string.IsNullOrEmpty(key)) return;

        // 没持锁就返回：重复 Unlock 或跨界面误删都会走到这里
        if (!m_LockKeys.Remove(key)) return;

        CursorManager.Instance.RemoveLock(key);
    }

    /// <summary>
    /// 清空全部光标锁（流程切换 / 退出到主菜单 / 关闭所有界面时兜底）。
    /// 会同时把锁下发给 CursorManager，保证光标真的回到游戏模式。
    /// </summary>
    public static void Reset()
    {
        if (m_LockKeys.Count == 0) return;

        // 先拷贝再遍历：RemoveLock 不能在遍历 HashSet 的过程中改集合
        string[] keys = new string[m_LockKeys.Count];
        m_LockKeys.CopyTo(keys);
        m_LockKeys.Clear();

        foreach (string key in keys)
        {
            CursorManager.Instance.RemoveLock(key);
        }
    }

    /// <summary>
    /// 关闭 Domain Reload 时静态字段会跨 Play 会话存活，进入 Play 前必须清空。
    /// 这里只清本地集合，不碰 CursorManager（SubsystemRegistration 阶段它还没准备好）。
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        m_LockKeys.Clear();
    }
}

using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 模态界面暂停的引用计数服务（GF UIForm 体系专用）。
///
/// 【为什么不能直接写 Time.timeScale】
///   <see cref="HitStopManager"/> 是 Time.timeScale 的唯一所有者（暂停优先、卡肉不覆盖暂停）。
///   UI 层如果自己写 timeScale，就会和卡肉协程互相覆盖：暂停中触发一次卡肉，
///   卡肉结束时把 timeScale 恢复成 1，暂停就被意外解除。
///   所以界面层只表达「我要暂停多久」，真正的时间缩放大权仍留在 HitStopManager。
///
/// 【为什么用计数而不是布尔】
///   同时开着背包 + 商店时，关掉其中一个不应该恢复游戏。只有最后一个模态界面
///   关闭（计数归零）才 Resume。
///
/// 【约定】
///   1. 只有 <see cref="GameUIForm"/> 调用本服务，业务脚本不要直接 Pause/Resume；
///   2. Push / Pop 必须成对，key 与光标锁共用界面类型名；
///   3. 同一 key 重复 Push 直接忽略并告警（成对关系被破坏时能第一时间发现）。
/// </summary>
public static class UIPauseService
{
    /// <summary>当前持有暂停的 key 列表。用 List 而不是 HashSet，便于按打开顺序排查</summary>
    private static readonly List<string> m_PauseKeys = new List<string>();

    /// <summary>当前模态界面数量</summary>
    public static int PauseCount => m_PauseKeys.Count;

    /// <summary>是否被界面暂停中</summary>
    public static bool IsPausedByUI => m_PauseKeys.Count > 0;

    /// <summary>
    /// 申请暂停。第一个模态界面生效，后续只是增加计数。
    /// </summary>
    public static void Push(string key)
    {
        if (string.IsNullOrEmpty(key))
        {
            Debug.LogWarning("[UIPauseService] Push 的 key 为空，已忽略");
            return;
        }

        if (m_PauseKeys.Contains(key))
        {
            Debug.LogWarning($"[UIPauseService] key 重复 Push，已忽略：{key}（Push/Pop 不成对？）");
            return;
        }

        m_PauseKeys.Add(key);

        // 不是第一个：已经处于暂停，不需要再次下发
        if (m_PauseKeys.Count > 1) return;

        HitStopManager manager = HitStopManager.EnsureExists();
        manager?.Pause();
    }

    /// <summary>
    /// 释放暂停。只有计数归零（最后一个模态界面关闭）才真正恢复游戏。
    /// </summary>
    public static void Pop(string key)
    {
        if (string.IsNullOrEmpty(key)) return;

        if (!m_PauseKeys.Remove(key))
        {
            Debug.LogWarning($"[UIPauseService] key 未持锁却 Pop，已忽略：{key}");
            return;
        }

        if (m_PauseKeys.Count > 0) return;

        HitStopManager.Instance?.Resume();
    }

    /// <summary>
    /// 清空全部暂停（流程切换 / 关闭所有界面时兜底）。会立即恢复游戏。
    /// </summary>
    public static void Reset()
    {
        if (m_PauseKeys.Count == 0) return;

        m_PauseKeys.Clear();
        HitStopManager.Instance?.Resume();
    }

    /// <summary>
    /// 打印用快照：当前模态数量与 key 列表。验收「全关后 timeScale 回到 1」时看这个。
    /// </summary>
    public static string GetDebugInfo()
    {
        if (m_PauseKeys.Count == 0) return "UI 暂停: 0（timeScale 应为 1）";

        return $"UI 暂停: {m_PauseKeys.Count} [{string.Join(", ", m_PauseKeys)}]";
    }

    /// <summary>
    /// 关闭 Domain Reload 时静态字段会跨 Play 会话存活，进入 Play 前必须清空。
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        m_PauseKeys.Clear();
    }
}

using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 界面路由：全工程唯一的 UIForm 打开 / 关闭入口。
///
/// 【为什么不让各界面自己调 GameEntry.UI】
///   GF 的 OpenUIForm 不查重：同一路径调用两次就会实例化两个界面（GF 会把第二个挂在
///   同一组里，谁也关不掉谁）。而本工程的入口有三条 —— HUD 按钮（经 OpenPanelEventArgs
///   由 ProcedureGame 打开）、反引号（PlayerController）、GM 菜单（GmCmd）。
///   统一走路由之后，三条入口共享同一份「已打开」记录，重复打开会返回已有 formId。
///
/// 【记录为什么要能自愈】
///   同工程里还有 ProcedureGame 直接持有 formId 关闭 HUD 的写法；一旦有人绕过本路由
///   调 CloseUIForm，路由内部的记录就会指向一个已经不存在的界面。因此每次查询都用
///   GameEntry.UI.HasUIForm(serialId) 校验一次，记录失效就顺手清掉，而不是返回脏数据。
///
/// 【约定】
///   1. 界面路径只能来自 <see cref="UIPaths"/>，不要写字符串字面量；
///   2. 模态界面继承 <see cref="GameUIForm"/>，暂停与光标由它统一处理，路由不碰；
///   3. HUD 这类常驻界面由 Procedure 直接开关（不走路由），避免被当成模态。
/// </summary>
public static class UIRouter
{
    /// <summary>默认 UI 组。除 HUD/Page/Top/Loading 外的界面都进 Popup</summary>
    public const string DefaultGroup = "Popup";

    private class FormRecord
    {
        public int FormId;
        public string Path;
        public string Group;
    }

    /// <summary>路径 → 记录，用于查重与按路径关闭</summary>
    private static readonly Dictionary<string, FormRecord> m_ByPath = new Dictionary<string, FormRecord>();

    /// <summary>formId → 记录，用于按 id 关闭时反查路径并清理 m_ByPath</summary>
    private static readonly Dictionary<int, FormRecord> m_ById = new Dictionary<int, FormRecord>();

    /// <summary>当前已打开的界面数量</summary>
    public static int OpenCount => m_ByPath.Count;

    /// <summary>
    /// 打开界面。已经打开时不会重复实例化，直接返回已有的 formId。
    /// </summary>
    /// <param name="formPath">界面预制体路径，必须来自 <see cref="UIPaths"/></param>
    /// <param name="groupName">UI 组名，默认 Popup</param>
    /// <param name="userData">传给界面 OnOpen 的数据，路由不解析内容</param>
    /// <returns>formId；打开失败返回 0</returns>
    public static int Open(string formPath, string groupName = DefaultGroup, object userData = null)
    {
        if (string.IsNullOrEmpty(formPath))
        {
            Debug.LogError("[UIRouter] formPath 为空，已取消打开");
            return 0;
        }

        if (GameEntry.UI == null)
        {
            Debug.LogError($"[UIRouter] GameEntry.UI 为空（GameFramework.prefab 未挂载或未启动），无法打开：{formPath}");
            return 0;
        }

        // 查重：同一路径只允许一个实例
        if (TryGetRecord(formPath, out FormRecord existing))
        {
            Debug.Log($"[UIRouter] 界面已打开，忽略重复请求：{formPath}（formId={existing.FormId}）");
            return existing.FormId;
        }

        if (string.IsNullOrEmpty(groupName)) groupName = DefaultGroup;

        // UI 组没注册时补一次：TestScene 的入口直接进 ProcedureGame，会跳过 ProcedureLaunch 的 AddUIGroup
        if (!GameEntry.UI.HasUIGroup(groupName))
        {
            UIGroupBootstrap.EnsureAll();
        }

        int formId = GameEntry.UI.OpenUIForm(formPath, groupName, userData);
        if (formId <= 0)
        {
            Debug.LogError($"[UIRouter] OpenUIForm 返回非法 formId（{formId})：{formPath}，UI 组={groupName}");
            return 0;
        }

        // EditorResourceMode 下资源加载是同步的，返回时界面已经就绪；
        // 仍然用 HasUIForm 兜一次，避免把「打开失败」记成「已打开」而再也打不开
        if (!GameEntry.UI.HasUIForm(formId))
        {
            Debug.LogError($"[UIRouter] 界面打开失败（路径必须 Assets/ 开头且带 .prefab 后缀）：{formPath}");
            return 0;
        }

        FormRecord record = new FormRecord { FormId = formId, Path = formPath, Group = groupName };
        m_ByPath[formPath] = record;
        m_ById[formId] = record;
        return formId;
    }

    /// <summary>按路径关闭界面。没打开时是空操作</summary>
    public static void Close(string formPath)
    {
        if (!TryGetRecord(formPath, out FormRecord record)) return;
        Close(record.FormId);
    }

    /// <summary>按 formId 关闭界面。没记录时也会尝试关一次，防止记录丢失后界面关不掉</summary>
    public static void Close(int formId)
    {
        if (formId <= 0)
        {
            Debug.LogWarning("[UIRouter] Close 收到非法 formId，已忽略");
            return;
        }

        // 先删记录再关：OnClose 里可能再触发一次「是否还开着」的查询，
        // 这时应该已经得到 false，避免开关递归
        if (m_ById.TryGetValue(formId, out FormRecord record))
        {
            RemoveRecord(record);
        }

        // GF 侧已经关掉时不要再调一次，否则每次重复关闭都会刷一条 Warning
        if (GameEntry.UI == null || !GameEntry.UI.HasUIForm(formId)) return;

        GameEntry.UI.CloseUIForm(formId);
    }

    /// <summary>界面是否已经打开（记录或 GF 侧任一侧失效都会返回 false，并顺手清理记录）</summary>
    public static bool IsOpen(string formPath)
    {
        return TryGetRecord(formPath, out _);
    }

    /// <summary>
    /// 关闭全部界面；传 groupName 只关该组，传空关全部。
    /// 用于流程切换（回主菜单 / 关卡重载）时清场。
    /// </summary>
    public static void CloseAll(string groupName = null)
    {
        // 先拷一份 formId：关闭过程中会改 m_ById，不能边遍历边删
        List<int> targets = new List<int>(m_ById.Count);
        foreach (FormRecord record in m_ById.Values)
        {
            if (string.IsNullOrEmpty(groupName) || record.Group == groupName)
            {
                targets.Add(record.FormId);
            }
        }

        for (int i = 0; i < targets.Count; i++)
        {
            Close(targets[i]);
        }
    }

    /// <summary>
    /// 清空路由记录（不关闭界面本体）。流程退出时兜底，防止下一次进游戏
    /// 因为残留记录而误判「界面已打开」。
    /// </summary>
    public static void ClearRecords()
    {
        m_ByPath.Clear();
        m_ById.Clear();
    }

    /// <summary>
    /// 查记录并自愈：GF 侧已经不是有效界面时立刻移除记录。
    /// </summary>
    private static bool TryGetRecord(string formPath, out FormRecord record)
    {
        record = null;
        if (string.IsNullOrEmpty(formPath)) return false;
        if (!m_ByPath.TryGetValue(formPath, out record)) return false;

        if (GameEntry.UI == null || !GameEntry.UI.HasUIForm(record.FormId))
        {
            RemoveRecord(record);
            record = null;
            return false;
        }

        return true;
    }

    private static void RemoveRecord(FormRecord record)
    {
        if (record == null) return;
        m_ById.Remove(record.FormId);
        if (!string.IsNullOrEmpty(record.Path) && m_ByPath.TryGetValue(record.Path, out FormRecord current)
            && ReferenceEquals(current, record))
        {
            m_ByPath.Remove(record.Path);
        }
    }

    /// <summary>
    /// 关闭 Domain Reload 时静态字段会跨 Play 会话存活，进入 Play 前必须清空。
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        m_ByPath.Clear();
        m_ById.Clear();
    }
}

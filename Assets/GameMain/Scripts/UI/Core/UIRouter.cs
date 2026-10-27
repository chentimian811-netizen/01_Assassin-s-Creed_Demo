using GameFramework.Event;
using System.Collections.Generic;
using UnityEngine;
using UnityGameFramework.Runtime;

/// <summary>
/// 界面路由：全工程唯一的 UIForm 打开 / 关闭入口。
///
/// 【为什么不让各界面自己调 GameEntry.UI】
///   GF 的 OpenUIForm 不查重：同一路径调用两次就会实例化两个界面（GF 会把第二个挂在
///   同一组里，谁也关不掉谁）。而本工程的入口有三条 —— HUD 按钮（经 OpenPanelEventArgs
///   由 ProcedureGame 打开）、反引号（PlayerController）、GM 菜单（GmCmd）。
///   统一走路由之后，三条入口共享同一份「已打开」记录，重复打开会返回已有 formId。
///
/// 【重要：GF 的 OpenUIForm 是异步的】
///   真实表现（2026-10-28 实测）：OpenUIForm 立刻返回 serialId，但实例是在之后的
///   UIComponent.Update 里才创建出来。因此
///     - 打开后立刻调 GameEntry.UI.HasUIForm(serialId) 一定是 false，
///     - 打开后立刻调 GameEntry.UI.HasUIForm(路径) 也不可靠。
///   早期版本就是拿 HasUIForm 当成功判据，结果每次都判定「打开失败 → 不记账」，
///   于是每按一次反引号就多一个背包，且再按也关不掉。
///   现在的策略是**乐观记账 + 生命周期自校正**：
///     1. OpenUIForm 返回正数 formId 就立刻记账（不查 GF 状态）；
///     2. 界面自己的 OnOpen 会调 EnsureRegistered 把记录校正成真实 id / 真实路径；
///     3. 界面自己的 OnClose 会调 Unregister 删记录（这条覆盖「被任何人关掉」的情况，
///        包括 CloseAll 和 GF 关闭界面管理器）；
///     4. 兜底订阅 GF 的事件：打开成功补记录、打开失败删记录（否则一个错路径会把
///        这个界面永久锁死）、关闭完成删记录。
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

    /// <summary>是否已经订阅过 GF 的界面事件（事件组件只在运行时可用，所以懒订阅）</summary>
    private static bool m_EventSubscribed;

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
        if (m_ByPath.TryGetValue(formPath, out FormRecord existing))
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

        EnsureEventSubscription();

        int formId = GameEntry.UI.OpenUIForm(formPath, groupName, userData);
        if (formId <= 0)
        {
            Debug.LogError($"[UIRouter] OpenUIForm 返回非法 formId（{formId}）：{formPath}，UI 组={groupName}");
            return 0;
        }

        // 乐观记账：此刻实例还没创建完（GF 是异步的），不能拿 HasUIForm 去验证。
        // 真正失败会由 OpenUIFormFailureEventArgs 把这条记录删掉。
        AddRecord(new FormRecord { FormId = formId, Path = formPath, Group = groupName });
        return formId;
    }

    /// <summary>
    /// 界面打开时由 <see cref="GameUIForm"/> 调用，把记录校正成界面的真实 id / 路径 / 分组。
    /// 也覆盖「不经过 UIRouter 直接 OpenUIForm」的界面，保证它们同样能被 CloseSelf 关掉。
    /// </summary>
    public static void EnsureRegistered(int formId, string formPath, string groupName = null)
    {
        if (formId <= 0 || string.IsNullOrEmpty(formPath)) return;

        if (m_ById.TryGetValue(formId, out FormRecord existing))
        {
            // Open 时已经记过账：只补可能为空的分组名
            if (string.IsNullOrEmpty(existing.Group) && !string.IsNullOrEmpty(groupName))
            {
                existing.Group = groupName;
            }
            return;
        }

        AddRecord(new FormRecord
        {
            FormId = formId,
            Path = formPath,
            Group = string.IsNullOrEmpty(groupName) ? DefaultGroup : groupName
        });
    }

    /// <summary>
    /// 界面关闭时由 <see cref="GameUIForm"/> 调用。任何关闭途径（点关闭、CloseAll、
    /// GF 关闭界面管理器）都会经过界面的 OnClose，所以这里是记录最可靠的清理点。
    /// </summary>
    public static void Unregister(int formId)
    {
        if (formId > 0 && m_ById.TryGetValue(formId, out FormRecord record))
        {
            RemoveRecord(record);
        }
    }

    /// <summary>按路径关闭界面。没打开时是空操作</summary>
    public static void Close(string formPath)
    {
        if (string.IsNullOrEmpty(formPath)) return;
        if (m_ByPath.TryGetValue(formPath, out FormRecord record)) Close(record.FormId);
    }

    /// <summary>按 formId 关闭界面。不是路由记录的界面（如 Procedure 直接开的 HUD）会告警并忽略</summary>
    public static void Close(int formId)
    {
        if (formId <= 0)
        {
            Debug.LogWarning("[UIRouter] Close 收到非法 formId，已忽略");
            return;
        }

        if (!m_ById.TryGetValue(formId, out FormRecord record))
        {
            Debug.LogWarning($"[UIRouter] formId={formId} 不在路由记录里，已忽略关闭请求");
            return;
        }

        // 先删记录再关：OnClose 里可能再触发一次「是否还开着」的查询，
        // 这时应该已经得到 false，避免开关递归
        RemoveRecord(record);

        if (GameEntry.UI != null)
        {
            GameEntry.UI.CloseUIForm(formId);
        }
    }

    /// <summary>界面是否已经打开（只查路由记录，不查 GF 内部状态：GF 的打开是异步的）</summary>
    public static bool IsOpen(string formPath)
    {
        return !string.IsNullOrEmpty(formPath) && m_ByPath.ContainsKey(formPath);
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

    private static void AddRecord(FormRecord record)
    {
        if (record == null) return;

        // 同一路径还挂着别的 id（理论上不该发生）：先把旧记录清掉，避免查重失真
        if (m_ByPath.TryGetValue(record.Path, out FormRecord stale) && stale.FormId != record.FormId)
        {
            RemoveRecord(stale);
        }

        m_ByPath[record.Path] = record;
        m_ById[record.FormId] = record;
    }

    private static void RemoveRecord(FormRecord record)
    {
        if (record == null) return;

        m_ById.Remove(record.FormId);
        if (!string.IsNullOrEmpty(record.Path)
            && m_ByPath.TryGetValue(record.Path, out FormRecord current)
            && ReferenceEquals(current, record))
        {
            m_ByPath.Remove(record.Path);
        }
    }

    // ==================== GF 事件兜底 ====================

    /// <summary>
    /// 懒订阅 GF 的界面事件。重复订阅在 GF 内部会被忽略，所以这里只需要一个 bool 标记。
    /// </summary>
    private static void EnsureEventSubscription()
    {
        if (m_EventSubscribed) return;

        EventComponent eventComponent = GameEntry.Event;
        if (eventComponent == null)
        {
            Debug.LogWarning("[UIRouter] GameEntry.Event 为空，界面记录将只能靠界面自身 OnOpen/OnClose 维护");
            return;
        }

        eventComponent.Subscribe(OpenUIFormSuccessEventArgs.EventId, OnOpenUIFormSuccess);
        eventComponent.Subscribe(OpenUIFormFailureEventArgs.EventId, OnOpenUIFormFailure);
        eventComponent.Subscribe(CloseUIFormCompleteEventArgs.EventId, OnCloseUIFormComplete);
        m_EventSubscribed = true;
    }

    private static void OnOpenUIFormSuccess(object sender, GameEventArgs e)
    {
        if (e is not OpenUIFormSuccessEventArgs args) return;

        UIForm uiForm = args.UIForm;
        if (uiForm == null) return;

        string group = uiForm.UIGroup != null ? uiForm.UIGroup.Name : null;
        EnsureRegistered(uiForm.SerialId, uiForm.UIFormAssetName, group);
    }

    private static void OnOpenUIFormFailure(object sender, GameEventArgs e)
    {
        if (e is not OpenUIFormFailureEventArgs args) return;

        // 打开失败：把乐观记录删掉，否则这个路径会被永久判定为「已打开」，按钮再也点不动
        if (m_ById.TryGetValue(args.SerialId, out FormRecord record))
        {
            RemoveRecord(record);
        }
        else if (!string.IsNullOrEmpty(args.UIFormAssetName)
                 && m_ByPath.TryGetValue(args.UIFormAssetName, out record))
        {
            RemoveRecord(record);
        }

        Debug.LogError($"[UIRouter] 界面打开失败：{args.UIFormAssetName}，{args.ErrorMessage}");
    }

    private static void OnCloseUIFormComplete(object sender, GameEventArgs e)
    {
        if (e is not CloseUIFormCompleteEventArgs args) return;
        if (m_ById.TryGetValue(args.SerialId, out FormRecord record)) RemoveRecord(record);
    }

    /// <summary>
    /// 关闭 Domain Reload 时静态字段会跨 Play 会话存活，进入 Play 前必须清空。
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        m_ByPath.Clear();
        m_ById.Clear();
        m_EventSubscribed = false;
    }
}

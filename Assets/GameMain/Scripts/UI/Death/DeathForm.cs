//------------------------------------------------------------
// ACDemo — 死亡界面（GF UIFormLogic 版）
// 放置路径: Assets/GameMain/Scripts/UI/Death/DeathForm.cs
// 对应 Prefab: Assets/UI/Prefabs/Death/DeathForm.prefab
// 分组: Popup（模态：暂停 + 锁光标由 GameUIForm 统一处理）
//------------------------------------------------------------

using UnityEngine;
using UnityEngine.UI;
using UnityGameFramework.Runtime;

/// <summary>
/// 死亡界面。玩家血量归零（Health.isPlayer）→ GameEvents.OnPlayerDeath →
/// MainHUDForm（常驻 HUD，唯一在游戏流程里一直活着的界面）打开本界面。
///
/// 【为什么由 HUD 打开，而不是死亡界面自己订阅】
///   界面在打开之前实例还不存在，不可能订阅到「我已经死了」这件事。
///   所以监听点在常驻的 HUD 上；本界面只负责「死相 + 出口」。
///
/// 【当前阶段的范围】
///   只做「死亡 → 暂停 + 出界面 → 返回主菜单（开发期＝重载场景）」这条最小闭环。
///   真正的死亡惩罚（掉落魂、篝火复活、读档）属于 Phase 4 死亡循环，届时把
///   ReturnToMainMenu 换成「在篝火复活」即可，界面本身不用改。
/// </summary>
public class DeathForm : GameUIForm
{
    [Header("按钮")]
    [Tooltip("返回主菜单（开发期等价于重开关卡）")]
    [SerializeField] private Button m_ReturnBtn;

    /// <summary>只跑一次：绑监听（池化实例不能重复 AddListener）</summary>
    protected override void OnInitForm(object userData)
    {
        if (m_ReturnBtn != null) m_ReturnBtn.onClick.AddListener(OnClickReturn);
    }

    /// <summary>
    /// 每次打开：当前没有动态数据要刷新。
    /// 将来要显示「距上次篝火存活时长 / 掉落魂数量」时，在这里从数据源拉一次。
    /// </summary>
    protected override void OnOpenForm(object userData) { }

    /// <summary>监听绑在 OnInitForm 上，随实例销毁一起释放。</summary>
    protected override void OnCloseForm() { }

    private void OnClickReturn()
    {
        GameEntry.Event.Fire(this, MenuCommandEventArgs.Create(ProcedureGame.CommandReturnToMainMenu));
    }
}

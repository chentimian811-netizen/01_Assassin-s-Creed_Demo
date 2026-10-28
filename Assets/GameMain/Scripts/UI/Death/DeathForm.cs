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
///   所以监听点在常驻的 HUD 上；本界面只负责「死亡画面 + 出口」。
///
/// 【出口（P0 第 2 步改动）】
///   原先只有「返回主菜单」（等于放弃这一局）。现在补上主出口
///   「在篝火重生」→ 发 CommandRespawnAtBonfire → ProcedureGame 调
///   CheckpointManager.RespawnPlayer() 复活，再关掉本界面（关掉即解除暂停）。
///   复活落点、回满、清敌人仇恨都在 CheckpointManager 里，界面不碰玩法状态。
/// </summary>
public class DeathForm : GameUIForm
{
    [Header("按钮")]
    [Tooltip("在篝火重生（主出口）。没点亮过篝火时回出生点")]
    [SerializeField] private Button m_RespawnBtn;

    [Tooltip("返回主菜单（放弃这一局，单场景阶段只切流程、不重载场景）")]
    [SerializeField] private Button m_ReturnBtn;

    /// <summary>只跑一次：绑监听（池化实例不能重复 AddListener）</summary>
    protected override void OnInitForm(object userData)
    {
        if (m_RespawnBtn != null) m_RespawnBtn.onClick.AddListener(OnClickRespawn);
        if (m_ReturnBtn != null) m_ReturnBtn.onClick.AddListener(OnClickReturn);
    }

    /// <summary>
    /// 每次打开都跑：当前没有动态数据要刷新。
    /// 将来要显示「距上次篝火存活时长 / 掉落魂数量」时，在这里从数据源拉一次。
    /// </summary>
    protected override void OnOpenForm(object userData) { }

    /// <summary>监听绑在 OnInitForm 上，随实例销毁一起释放</summary>
    protected override void OnCloseForm() { }

    private void OnClickRespawn()
    {
        GameEntry.Event.Fire(this, MenuCommandEventArgs.Create(ProcedureGame.CommandRespawnAtBonfire));
    }

    private void OnClickReturn()
    {
        GameEntry.Event.Fire(this, MenuCommandEventArgs.Create(ProcedureGame.CommandReturnToMainMenu));
    }
}

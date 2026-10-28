//------------------------------------------------------------
// ACDemo — 胜利界面（GF UIFormLogic 版）
// 放置路径: Assets/GameMain/Scripts/UI/Victory/VictoryForm.cs
// 对应 Prefab: Assets/UI/Prefabs/Victory/VictoryForm.prefab
// 分组: Popup（模态：暂停 + 锁光标由 GameUIForm 统一处理）
//------------------------------------------------------------

using UnityEngine;
using UnityEngine.UI;
using UnityGameFramework.Runtime;

/// <summary>
/// 胜利界面（Boss 通关）。当前是空壳：
///   - Boss 还没有实例（BossPhase 表故意为空），GameEvents.OnBossKilled 目前无人发布；
///   - 所以本界面没有自动打开者，只保证「预制体 + 生命周期 + 出口」是就绪的。
///
/// 【接 Boss 时要补的两步】
///   1. 在常驻 HUD（MainHUDForm）里订阅 GameEvents.OnBossKilled，收到后
///      UIRouter.Open(UIPaths.VictoryForm, "Popup") —— 与 DeathForm 的打开方式保持一致；
///   2. 把出口按钮从「继续」换成实际需求（回主菜单 / 进下一关）。
/// </summary>
public class VictoryForm : GameUIForm
{
    [Header("按钮")]
    [Tooltip("继续（关闭界面，解除暂停）")]
    [SerializeField] private Button m_ContinueBtn;

    /// <summary>只跑一次：绑监听（池化实例不能重复 AddListener）</summary>
    protected override void OnInitForm(object userData)
    {
        if (m_ContinueBtn != null) m_ContinueBtn.onClick.AddListener(OnClickContinue);
    }

    /// <summary>每次打开：当前没有动态数据要刷新（可在此显示击杀数 / 用时）</summary>
    protected override void OnOpenForm(object userData) { }

    /// <summary>监听绑在 OnInitForm 上，随实例销毁一起释放。</summary>
    protected override void OnCloseForm() { }

    private void OnClickContinue()
    {
        // 胜利界面没有后续流程，直接关自己；GameUIForm 会 Pop 掉暂停
        CloseSelf();
    }
}

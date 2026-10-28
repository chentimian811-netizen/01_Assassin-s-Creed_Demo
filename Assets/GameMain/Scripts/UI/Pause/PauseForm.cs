//------------------------------------------------------------
// ACDemo — 暂停界面（GF UIFormLogic 版）
// 放置路径: Assets/GameMain/Scripts/UI/Pause/PauseForm.cs
// 对应 Prefab: Assets/UI/Prefabs/Pause/PauseForm.prefab
// 分组: Popup（模态：暂停 + 锁光标由 GameUIForm 统一处理）
//------------------------------------------------------------

using UnityEngine;
using UnityEngine.UI;
using UnityGameFramework.Runtime;

/// <summary>
/// 暂停界面。Esc 打开（PlayerController.GetPauseInput → MenuCommandEventArgs("Pause") → ProcedureGame）。
///
/// 【暂停由谁做】
///   继承 <see cref="GameUIForm"/> 后，OnOpen 即 UIPauseService.Push → HitStopManager.Pause，
///   OnClose 反向 Pop，只有最后一个模态界面关掉才恢复。本类不写 Time.timeScale，
///   也不直接调 HitStopManager —— 那是 HitStopManager 的独占权力。
///
/// 【按钮为什么只发事件、不自己关界面】
///   「继续」要恢复输入、「返回主菜单」要切场景，这两件事都会动流程状态，属于 ProcedureGame。
///   界面只表达意图（GF Event）→ 流程执行，以后加「设置 / 存档」还是同一条通路，
///   不会出现「界面里偷偷 ChangeState」的分叉。
/// </summary>
public class PauseForm : GameUIForm
{
    [Header("按钮")]
    [Tooltip("继续游戏（恢复输入 + 解除暂停）")]
    [SerializeField] private Button m_ResumeBtn;
    [Tooltip("返回主菜单（开发期等价于重开关卡）")]
    [SerializeField] private Button m_MainMenuBtn;

    /// <summary>
    /// 只跑一次：绑监听。GF 会池化界面实例，所以不能在 OnOpenForm 里重复 AddListener
    /// （第二次打开就会一次点击触发两次）。
    /// </summary>
    protected override void OnInitForm(object userData)
    {
        if (m_ResumeBtn != null) m_ResumeBtn.onClick.AddListener(OnClickResume);
        if (m_MainMenuBtn != null) m_MainMenuBtn.onClick.AddListener(OnClickMainMenu);
    }

    /// <summary>
    /// 每次打开：暂停界面没有需要刷新的动态数据（标题与按钮都是静态文本），
    /// 这里保留空实现只为满足基类「每次打开都跑」的契约。
    /// </summary>
    protected override void OnOpenForm(object userData) { }

    /// <summary>监听绑在 OnInitForm 上、实例销毁时一起消失，所以这里不需要解绑。</summary>
    protected override void OnCloseForm() { }

    private void OnClickResume()
    {
        GameEntry.Event.Fire(this, MenuCommandEventArgs.Create(ProcedureGame.CommandResume));
    }

    private void OnClickMainMenu()
    {
        GameEntry.Event.Fire(this, MenuCommandEventArgs.Create(ProcedureGame.CommandReturnToMainMenu));
    }
}

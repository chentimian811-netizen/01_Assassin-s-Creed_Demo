using UnityEngine;

/// <summary>
/// 可交互物的「范围 + 提示」传感器：挂在任何实现了 IInteractable 的物体上，
/// 负责判断玩家是否在交互范围内、登记/注销最近目标，并显示/隐藏交互提示。
///
/// 【为什么用距离轮询，而不是 Trigger + OnTriggerEnter】
///   1. 项目里两套老写法各有一份坑：WeaponPickup 用 Trigger，商店 NPC 用 Update 距离；
///      而 CharacterController 被代码瞬移（复活 / 篝火落点）时 OnTriggerExit 不保证触发，
///      会留下「人已经走了，E 还对着旧目标」的假登记。距离轮询没有这个状态；
///   2. 不需要给关卡物件配碰撞体，部署工具只要挂组件；
///   3. 可交互物是个位数，每帧几个 sqrMagnitude 的开销可以忽略。
///
/// 【为什么触发逻辑不写在各关卡脚本里】
///   篝火 / 掉魂 / 雾门 / 捷径门需要的是同一套「谁进了范围、提示什么、离开清掉」。
///   抄四份就会踩同一个老坑：A 的离开逻辑把 B 的登记清掉。
///   统一收到这里，由 PlayerController 用列表去重、按距离取最近。
///
/// 【提示走 InteractionPromptForm（第一版复用 PickupForm，实测看不见）】
///   PickupForm 根节点没有背景图、文字框净宽只有 120px，用它显示长句会折行溢出，
///   白字贴在屏幕右侧等于没有。交互提示必须一眼可见，所以单独做了
///   `Assets/UI/Prefabs/HUD/InteractionPromptForm.prefab`（底部居中、带底色条、字号 32）。
///   PickupForm 继续只负责「地面武器拾取」。
/// </summary>
[DisallowMultipleComponent]
public class InteractableSensor : MonoBehaviour
{
    [Header("交互范围")]
    [Tooltip("玩家进入该半径即可交互（米）")]
    [SerializeField] private float m_Radius = 2.5f;

    [Tooltip("判定中心相对本物体的偏移。篝火在地面上，抬到腰部高度更符合手感")]
    [SerializeField] private Vector3 m_Center = new Vector3(0f, 1f, 0f);

    private IInteractable m_Target;
    private PlayerController m_Player;
    private bool m_InRange;
    private string m_LastPrompt;

    /// <summary>判定中心（世界坐标）</summary>
    public Vector3 Center => transform.TransformPoint(m_Center);

    private void Awake()
    {
        m_Target = GetComponent<IInteractable>();
        if (m_Target == null)
        {
            Debug.LogError("[InteractableSensor] 同物体上没有实现 IInteractable 的组件，交互不可用", this);
        }
    }

    private void Update()
    {
        if (m_Target == null) return;

        if (m_Player == null)
        {
            m_Player = FindPlayer();
            if (m_Player == null) return;
        }

        // 1) 进出范围只改一次登记（列表在 PlayerController 里，天然去重）
        bool inRange = Vector3.Distance(Center, m_Player.transform.position) <= m_Radius;
        if (inRange != m_InRange)
        {
            m_InRange = inRange;
            if (inRange) m_Player.RegisterInteractable(m_Target);
            else m_Player.UnregisterInteractable(m_Target);
        }

        // 2) 提示随状态变化（点燃篝火 / 打开捷径门后文案与可用性都会变）。
        //    模态界面（死亡 / 暂停 / 背包）开着时也要收起：提示条在 Top 组（depth 3），
        //    不收起会直接盖在死亡界面（Popup，depth 2）上面。
        string prompt = inRange && !UIPauseService.IsPausedByUI && m_Target.CanInteract
            ? m_Target.InteractPrompt
            : null;

        if (prompt == m_LastPrompt) return;

        m_LastPrompt = prompt;
        if (string.IsNullOrEmpty(prompt)) InteractionPromptForm.Hide();
        else InteractionPromptForm.Show(prompt);
    }

    private void OnDisable()
    {
        // 掉魂被拾走 / 雾门清掉后 SetActive(false) / 场景卸载都会走到这里：
        // 必须把玩家手里的引用收回来，否则列表里会留下一个已销毁的物体
        if (m_Player != null && m_Target != null) m_Player.UnregisterInteractable(m_Target);

        m_Player = null;
        m_InRange = false;
        m_LastPrompt = null;
        InteractionPromptForm.Hide();
    }

    private static PlayerController FindPlayer()
    {
        GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
        return playerObject != null ? playerObject.GetComponent<PlayerController>() : null;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(Center, Mathf.Max(0.1f, m_Radius));
    }
}

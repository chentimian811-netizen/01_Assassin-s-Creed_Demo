using System.Collections;
using UnityEngine;

/// <summary>
/// 动作集切换的控制器替换：快照 → 赋值 → 等一帧 → 回灌。
///
/// 【为什么必须有】`animator.runtimeAnimatorController = x` 会把**全部参数打回默认值**。
/// 不处理的表现是：角色跑步中换武器，动画瞬间跳回待机。
///
/// 【准入】翻滚 / 攻击 / 死亡期间拒绝切换；请求**暂存**并在下一次 Update 重试，
/// 而不是丢弃 —— 否则玩家在翻滚中换武器会“按键没反应”。
/// </summary>
public class PlayerAnimationSet : MonoBehaviour
{
    /// <summary>PlayerMove.controller 上需要跨控制器保留的 float 参数</summary>
    static readonly string[] FloatParams =
    {
        "PlayerState",
        "MoveSpeed",
        "TurnSpeed",
        "JumpSpeed",
        "FeetTween",
    };

    Animator m_animator;
    PlayerDodge m_dodge;
    MeleeFighter m_fighter;
    Health m_health;

    RuntimeAnimatorController m_pending;
    bool m_hasPending;
    Coroutine m_switchRoutine;

    /// <summary>
    /// 当前是否允许切换动作集控制器。
    /// 【为什么收敛成一个属性】散落 if 必漏改，且无法一眼看出“现在为什么切不了”。
    /// </summary>
    public bool CanSwitchMoveset
    {
        get
        {
            if (m_health != null && m_health.IsDead) return false;
            if (m_dodge != null && m_dodge.IsDodging) return false;
            if (m_fighter != null && m_fighter.inAction) return false;
            if (m_fighter != null && m_fighter.inCounter) return false;
            return true;
        }
    }

    /// <summary>是否有一笔待生效的切换请求（供调试）</summary>
    public bool HasPendingSwitch => m_hasPending;

    void Awake()
    {
        m_animator = GetComponent<Animator>();
        m_dodge = GetComponent<PlayerDodge>();
        m_fighter = GetComponent<MeleeFighter>();
        m_health = GetComponent<Health>();
    }

    void Update()
    {
        if (m_hasPending && CanSwitchMoveset && m_switchRoutine == null)
        {
            RuntimeAnimatorController target = m_pending;
            m_pending = null;
            m_hasPending = false;
            m_switchRoutine = StartCoroutine(SwitchRoutine(target));
        }
    }

    /// <summary>
    /// 请求切换控制器。target 为 null 表示回退到调用方持有的 originalController。
    /// 不满足 CanSwitchMoveset 时暂存，待条件满足后自动生效。
    /// </summary>
    public void RequestController(RuntimeAnimatorController target)
    {
        if (m_animator == null) return;
        if (m_animator.runtimeAnimatorController == target) return;

        if (!CanSwitchMoveset)
        {
            m_pending = target;
            m_hasPending = true;
            return;
        }

        if (m_switchRoutine != null)
            StopCoroutine(m_switchRoutine);

        m_switchRoutine = StartCoroutine(SwitchRoutine(target));
    }

    IEnumerator SwitchRoutine(RuntimeAnimatorController target)
    {
        float[] snap = Capture();

        m_animator.runtimeAnimatorController = target;

        // 等一帧，让新 controller 接管后再回灌，否则写回的默认值会被立刻冲掉
        yield return null;

        Restore(snap);
        m_switchRoutine = null;
    }

    float[] Capture()
    {
        float[] snap = new float[FloatParams.Length];
        if (m_animator == null || m_animator.runtimeAnimatorController == null)
            return snap;
        for (int i = 0; i < FloatParams.Length; i++)
            snap[i] = m_animator.GetFloat(FloatParams[i]);
        return snap;
    }

    void Restore(float[] snap)
    {
        // 控制器无效/未初始化时写参数会刷 “Animator has not been initialized”
        if (m_animator == null || snap == null) return;
        if (m_animator.runtimeAnimatorController == null) return;
        for (int i = 0; i < FloatParams.Length && i < snap.Length; i++)
        {
            // 立即写回，不用 damping，避免换武器后还要等插值才恢复速度档
            m_animator.SetFloat(FloatParams[i], snap[i]);
        }
    }
}

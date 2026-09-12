using System.Collections;
using UnityEngine;

/// <summary>
/// 翻滚 + 无敌帧。对接 PlayerController 中已预留的 playerDodge 占位。
///
/// 【i-Frame 计时为什么不用 Time.time】
///   卡肉会把 Time.timeScale 压到 0.05，暂停会压到 0。
///   用 Time.time 计时会导致"最需要无敌帧的时刻，无敌帧反而不计时"。
///   因此无敌窗口统一用 Time.unscaledTime。
///
/// 【位移为什么交给 Root Motion】
///   PlayerMovement 已预留"翻滚期间完全由 Root Motion 驱动位移，跳过代码移动"的位置，
///   这里不再用代码位移，避免双重驱动。
/// </summary>
[DisallowMultipleComponent]
public class PlayerDodge : MonoBehaviour
{
    // 无敌帧来源 key：必须在 Health 的全工程唯一常量里注册
    public const string InvulnerableReason = InvulnReasons.DodgeIFrame;

    [SerializeField] private StaminaConfig staminaConfig;
    [SerializeField] private float dodgeDuration = 0.9f;
    [SerializeField] private float dodgeCooldown = 0.2f;

    [Tooltip("无方向输入时是否允许原地翻滚（false = 必须有移动输入）")]
    [SerializeField] private bool allowNeutralDodge = true;

    private Animator animator;
    private PlayerController playerController;
    private PlayerMovement playerMovement;
    private PlayerStamina stamina;
    private Health health;
    private CharacterController characterController;

    private bool isDodging;
    private float dodgeEndRealtime;
    private float nextDodgeRealtime;
    private Vector3 dodgeDirection;

    public bool IsDodging => isDodging;

    public void Init(Animator anim, PlayerController controller, PlayerMovement movement,
        PlayerStamina staminaRef, Health healthRef, CharacterController cc)
    {
        animator = anim;
        playerController = controller;
        playerMovement = movement;
        stamina = staminaRef;
        health = healthRef;
        characterController = cc;
    }

    /// <summary>由 PlayerController 的 Dodge 回调调用（对应已有空壳 GetDodgeInput）</summary>
    public void TryDodge()
    {
        if (isDodging) return;
        if (Time.unscaledTime < nextDodgeRealtime) return;
        if (health != null && health.IsDead) return;
        if (playerController != null && !playerController.acceptInput) return;

        // 耐力不足直接失败（不做"无耐力也翻滚"的宽松处理，保持魂类手感）
        if (stamina != null && staminaConfig != null)
        {
            if (!stamina.TryConsume(staminaConfig.dodgeCost)) return;
        }

        dodgeDirection = ResolveDirection();
        StartCoroutine(CoDodge());
    }

    private Vector3 ResolveDirection()
    {
        Vector3 input = playerMovement != null ? playerMovement.GetPlayerMovement() : Vector3.zero;
        if (input.sqrMagnitude < 0.01f)
        {
            input = allowNeutralDodge ? Vector3.forward : Vector3.zero;
        }
        return input.sqrMagnitude < 0.01f ? Vector3.zero : input.normalized;
    }

    private IEnumerator CoDodge()
    {
        isDodging = true;

        // 翻滚期间清空垂直速度，避免"空中翻滚"的物理残留
        playerMovement?.ResetVerticalVelocity();

        // 按方向选真实状态名。⚠️ 工程里【没有】"Dodge_Roll" 这个状态，
        // 实际 clip 是 Blink 的 RollForward / RollBackward / RollLeft / RollRight
        if (animator != null) animator.CrossFade(ResolveRollStateName(dodgeDirection), 0.1f);

        dodgeEndRealtime = Time.unscaledTime + dodgeDuration;

        // 兜底：即使动画事件没触发，也要结束翻滚并清除无敌
        while (Time.unscaledTime < dodgeEndRealtime)
        {
            yield return null;
        }

        EndDodge();
    }

    /// <summary>
    /// 按翻滚方向选择 Animator 状态名。
    /// local.z 为正 = 朝向角色前方。
    /// </summary>
    private string ResolveRollStateName(Vector3 dir)
    {
        if (dir.sqrMagnitude < 0.01f) return "RollBackward";   // 无输入 → 后撤
        Vector3 local = transform.InverseTransformDirection(dir);
        if (Mathf.Abs(local.z) >= Mathf.Abs(local.x))
            return local.z > 0f ? "RollForward" : "RollBackward";
        return local.x > 0f ? "RollRight" : "RollLeft";
    }

    // ==================== 动画事件 ====================

    /// <summary>动画事件：开无敌帧</summary>
    public void OpenIFrame()
    {
        health?.SetInvulnerable(InvulnerableReason, true);
    }

    /// <summary>动画事件：关无敌帧</summary>
    public void CloseIFrame()
    {
        health?.SetInvulnerable(InvulnerableReason, false);
    }

    private void EndDodge()
    {
        isDodging = false;
        nextDodgeRealtime = Time.unscaledTime + dodgeCooldown;

        // 无论动画事件是否触发，结束翻滚必须清无敌（防止"卡出永久无敌"）
        health?.SetInvulnerable(InvulnerableReason, false);
    }

    private void OnDisable()
    {
        // 组件被禁用时也要清，否则无敌会残留
        health?.SetInvulnerable(InvulnerableReason, false);
    }
}

/// <summary>
/// 全工程无敌帧来源 key 常量集中处。
/// 集中管理的目的：避免各处写裸字符串导致"清错了别人的无敌"。
/// </summary>
public static class InvulnReasons
{
    public const string DodgeIFrame = "dodge_iframe";     // 翻滚无敌帧
    public const string Counter = "counter";              // 反击/处决演出
    public const string ParrySuccess = "parry_success";   // 弹反成功后短暂无敌
    public const string Respawn = "respawn";              // 复活保护
    public const string Bonfire = "bonfire";              // 篝火交互演出
}

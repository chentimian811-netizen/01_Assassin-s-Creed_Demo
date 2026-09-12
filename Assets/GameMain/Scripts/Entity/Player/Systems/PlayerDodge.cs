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
/// 【位移为什么用代码而不是 Root Motion】
///   Blink 的 Roll clip（human_male_roll_01）Average Velocity = 0，
///   没有 Root Motion 位移曲线，只播动画会原地滚。
///   故位移由本组件在协程里 CharacterController.Move 驱动；
///   PlayerMovement 在 IsDodging 时跳过常规移动，避免双重驱动。
///
/// 【位移方向与转身为什么解耦】
///   非锁定翻滚若先瞬时转身再取 transform.forward 位移，90°/135° 的朝向跳变
///   会让画面"卡一下"。解耦后：位移方向在翻滚开始时按目标方向立即锁定（不等转身），
///   转身只是视觉，前 turnToDodgeDirTime 秒内 Slerp 平滑过渡。
/// </summary>
[DisallowMultipleComponent]
public class PlayerDodge : MonoBehaviour
{
    // 无敌帧来源 key：必须在 Health 的全工程唯一常量里注册
    public const string InvulnerableReason = InvulnReasons.DodgeIFrame;

    [SerializeField] private StaminaConfig staminaConfig;
    [SerializeField] private float dodgeDuration = 0.9f;
    [SerializeField] private float dodgeCooldown = 0.2f;

    [Header("位移（对齐 CBTFM DodgeV2：曲线插值，非匀速）")]
    [Tooltip("翻滚总位移（米）")]
    [SerializeField] private float dodgeDistance = 3.5f;
    [Tooltip("进入 Roll 状态后再等这么久才开始位移（秒）。0 = 立刻跟上，减少「先滑后滚」")]
    [SerializeField] private float moveStartDelay = 0f;
    [Tooltip("位移耗时（秒）。仅在无法读到 Animator 状态时作为兜底")]
    [SerializeField] private float moveDuration = 0.45f;
    [Tooltip("0→1 的位移曲线。前段压平（动画下蹲蓄力期不位移），中段前扑爆发，0.84（= Roll 过渡 ExitTime）收满，淡出期不再位移")]
    [SerializeField] private AnimationCurve moveCurve =
        new AnimationCurve(
            new Keyframe(0f, 0f, 0f, 0.3f),
            new Keyframe(0.15f, 0.08f, 0.5f, 2.2f),
            new Keyframe(0.5f, 0.8f, 2.2f, 0.8f),
            new Keyframe(0.84f, 1f, 0.4f, 0f),
            new Keyframe(1f, 1f, 0f, 0f));

    [Header("无敌帧（动画事件未配置时的兜底窗口）")]
    [Tooltip("翻滚开始后多久进入无敌（秒，真实时间）")]
    [SerializeField] private float iframeStartDelay = 0.05f;
    [Tooltip("无敌持续时长（秒，真实时间）。动画事件 OpenIFrame/CloseIFrame 仍可覆盖")]
    [SerializeField] private float iframeDuration = 0.4f;

    [Header("转向（仅非锁定翻滚）")]
    [Tooltip("非锁定翻滚的转身时长（秒）。0 = 瞬时转向（生硬），0.08~0.12 较自然")]
    [SerializeField] private float turnToDodgeDirTime = 0.1f;

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

    // 平滑转身运行时状态（仅非锁定翻滚使用）
    private Quaternion turnStartRot;    // 翻滚开始时的朝向
    private Quaternion dodgeTargetRot;  // 目标朝向
    private float turnElapsed;          // 转身已进行时间
    private bool hasTurnTarget;         // 本次翻滚是否需要平滑转身（锁定时为 false）

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
        // 未在 Inspector 指定时，从 PlayerStamina 取同一份配置，避免漏拖导致翻滚不扣耐力
        if (staminaConfig == null && stamina != null)
            staminaConfig = stamina.Config;

        LogRollClipLengths();
    }

    /// <summary>由 PlayerController 的 Dodge 回调调用（对应已有空壳 GetDodgeInput）</summary>
    public void TryDodge()
    {
        if (isDodging) return;
        if (Time.unscaledTime < nextDodgeRealtime) return;
        if (health != null && health.IsDead) return;
        if (playerController != null && !playerController.acceptInput) return;

        var fighter = playerController != null ? playerController.MeleeFighter : null;
        if (fighter != null)
        {
            // 受击硬直中不可翻滚——魂类惯例：被打必须吃完整段硬直
            if (fighter.IsAttackingHit) return;
            // 处决演出中不可翻滚——双人配对动画，中途打断两边状态都会脏
            if (fighter.inCounter) return;
        }

        // 耐力不足直接失败（不做"无耐力也翻滚"的宽松处理，保持魂类手感）
        if (stamina != null && staminaConfig != null)
        {
            if (!stamina.TryConsume(staminaConfig.dodgeCost)) return;
        }

        // 翻滚打断攻击：清掉攻击协程/战斗层动画/命中盒/连击后再进翻滚。
        // 放在耐力判定之后——耐力不足翻滚失败时，攻击不应被取消
        if (fighter != null && fighter.inAction)
            fighter.CancelActionByDodge();

        // 立刻锁翻滚态，避免同帧 Update 仍走走路位移
        isDodging = true;
        dodgeDirection = ResolveDirection();
        StartCoroutine(CoDodge());
    }

    private Vector3 ResolveDirection()
    {
        // GetPlayerMovement() 已是本地空间（PlayerMovement 内做过 InverseTransformVector）
        Vector3 local = playerMovement != null ? playerMovement.GetPlayerMovement() : Vector3.zero;
        if (local.sqrMagnitude < 0.01f)
        {
            // 无输入：非锁定后撤；锁定则原地后撤（保持面向敌人）
            return allowNeutralDodge ? Vector3.back : Vector3.zero;
        }
        local.y = 0f;
        return local.sqrMagnitude < 0.01f ? Vector3.zero : local.normalized;
    }

    private IEnumerator CoDodge()
    {
        isDodging = true;

        // 翻滚期间清空垂直速度，避免"空中翻滚"的物理残留
        playerMovement?.ResetVerticalVelocity();

        bool locking = playerController != null && playerController.IsLocking;
        string rollState;
        Vector3 worldMoveDir;

        if (locking)
        {
            // 锁定：保持面向敌人，按本地方向播四向翻滚，位移也按该本地方向
            rollState = ResolveRollStateName(dodgeDirection);
            worldMoveDir = transform.TransformDirection(dodgeDirection);
            hasTurnTarget = false;  // 锁定不转身
        }
        else
        {
            // 非锁定：位移方向按"目标方向"立即锁定（不等转身，否则位移方向是错的）；
            // 转身只做视觉，交给 TickDodgeTurn 在翻滚前段 Slerp 过去
            Vector3 worldDir = transform.TransformDirection(dodgeDirection);
            worldDir.y = 0f;
            if (worldDir.sqrMagnitude < 0.001f)
                worldDir = transform.forward;   // 无输入后撤等边界：沿用当前朝向
            worldDir.Normalize();

            rollState = "RollForward";
            worldMoveDir = worldDir;
            turnStartRot = transform.rotation;
            dodgeTargetRot = Quaternion.LookRotation(worldDir);
            turnElapsed = 0f;
            hasTurnTarget = true;
        }

        worldMoveDir.y = 0f;
        if (worldMoveDir.sqrMagnitude < 0.001f)
            worldMoveDir = transform.forward;
        worldMoveDir.Normalize();

        // 硬切进翻滚（不用 CrossFade）：混合期会残留走路姿态/根运动观感
        if (animator != null)
        {
            animator.Play(rollState, 0, 0f);
            animator.Update(0f); // 本帧立刻求值，Current 就是 Roll
        }

        dodgeEndRealtime = Time.unscaledTime + dodgeDuration;

        // 无敌窗口与位移并行
        var iframeRoutine = StartCoroutine(CoIFrameWindow());

        // 位移协程内部会等到 Current 真正变成 Roll 再锁起点、再动
        if (moveStartDelay > 0f)
            yield return new WaitForSeconds(moveStartDelay);

        yield return StartCoroutine(CoMoveAlongCurve(worldMoveDir, rollState));

        // 等翻滚总时长走完
        while (Time.unscaledTime < dodgeEndRealtime)
            yield return null;

        if (iframeRoutine != null) StopCoroutine(iframeRoutine);
        EndDodge();
    }

    private IEnumerator CoIFrameWindow()
    {
        float iframeOpenAt = Time.unscaledTime + iframeStartDelay;
        float iframeCloseAt = iframeOpenAt + iframeDuration;
        int phase = 0;
        while (Time.unscaledTime < dodgeEndRealtime)
        {
            float now = Time.unscaledTime;
            if (phase == 0 && now >= iframeOpenAt)
            {
                OpenIFrame();
                phase = 1;
            }
            else if (phase == 1 && now >= iframeCloseAt)
            {
                CloseIFrame();
                phase = 2;
            }
            yield return null;
        }
    }

    /// <summary>
    /// 每帧推进平滑转身。转身只影响视觉；
    /// 位移方向已在 CoDodge 里按目标方向锁定，与当前朝向无关。
    /// </summary>
    private void TickDodgeTurn()
    {
        if (!hasTurnTarget) return;
        turnElapsed += Time.deltaTime;
        float k = turnToDodgeDirTime <= 0f ? 1f : Mathf.Clamp01(turnElapsed / turnToDodgeDirTime);
        transform.rotation = Quaternion.Slerp(turnStartRot, dodgeTargetRot, k);
        if (k >= 1f) hasTurnTarget = false;
    }

    /// <summary>
    /// 曲线位移：只在 Current 已是 Roll 时读 normalizedTime。
    /// 过渡期若误用走路状态的 time，会「先冲一段再播滚」。
    /// </summary>
    private IEnumerator CoMoveAlongCurve(Vector3 worldMoveDir, string rollState)
    {
        if (characterController == null) yield break;

        // 先等到 Current == Roll，再锁起点（避免用过渡前的位置/时间）
        float enterWait = 0.25f;
        while (enterWait > 0f && animator != null
               && !animator.GetCurrentAnimatorStateInfo(0).IsName(rollState))
        {
            enterWait -= Time.deltaTime;
            TickDodgeTurn();    // 等待期间转身照走，不浪费前段过渡时间
            yield return null;
        }

        Vector3 startPos = transform.position;
        Vector3 endPos = startPos + worldMoveDir * dodgeDistance;
        Vector3 previousPos = startPos;
        bool moved = false;
        bool firstInRoll = true;

        if (animator != null)
        {
            float safety = moveDuration + 0.4f;
            while (safety > 0f)
            {
                safety -= Time.deltaTime;
                TickDodgeTurn();

                var info = animator.GetCurrentAnimatorStateInfo(0);
                if (!info.IsName(rollState))
                {
                    if (moved) yield break;
                    yield return null;
                    continue;
                }

                float t = Mathf.Clamp01(info.normalizedTime);
                // 刚进 Roll 的第一帧：强制 t=0 并重锁起点，防止时间轴跳变导致瞬移
                if (firstInRoll)
                {
                    firstInRoll = false;
                    startPos = transform.position;
                    endPos = startPos + worldMoveDir * dodgeDistance;
                    previousPos = startPos;
                    t = 0f;
                }

                float ratio = moveCurve.Evaluate(t);
                Vector3 targetPos = Vector3.Lerp(startPos, endPos, ratio);
                Vector3 delta = targetPos - previousPos;
                delta.y = 0f;
                if (delta.sqrMagnitude > 1e-10f)
                {
                    characterController.Move(delta);
                    moved = true;
                }
                previousPos = new Vector3(targetPos.x, previousPos.y, targetPos.z);

                if (t >= 0.999f)
                {
                    Vector3 finalDelta = endPos - previousPos;
                    finalDelta.y = 0f;
                    if (finalDelta.sqrMagnitude > 1e-10f)
                        characterController.Move(finalDelta);
                    yield break;
                }
                yield return null;
            }
        }

        if (moved) yield break;

        // 动画状态从未进入时的计时兜底
        previousPos = transform.position;
        startPos = previousPos;
        endPos = startPos + worldMoveDir * dodgeDistance;
        float moveElapsed = 0f;
        float moveDur = Mathf.Max(0.05f, moveDuration);
        while (moveElapsed < moveDur)
        {
            moveElapsed += Time.deltaTime;
            TickDodgeTurn();
            float t = Mathf.Clamp01(moveElapsed / moveDur);
            float ratio = moveCurve.Evaluate(t);
            Vector3 targetPos = Vector3.Lerp(startPos, endPos, ratio);
            Vector3 delta = targetPos - previousPos;
            delta.y = 0f;
            if (delta.sqrMagnitude > 1e-10f)
                characterController.Move(delta);
            previousPos = new Vector3(targetPos.x, previousPos.y, targetPos.z);
            yield return null;
        }
    }

    /// <summary>
    /// 按翻滚方向选择 Animator 状态名。参数为本地空间方向。
    /// local.z 为正 = 朝向角色前方。
    /// </summary>
    private string ResolveRollStateName(Vector3 localDir)
    {
        if (localDir.sqrMagnitude < 0.01f) return "RollBackward";   // 无输入 → 后撤
        if (Mathf.Abs(localDir.z) >= Mathf.Abs(localDir.x))
            return localDir.z > 0f ? "RollForward" : "RollBackward";
        return localDir.x > 0f ? "RollRight" : "RollLeft";
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
        hasTurnTarget = false;
        nextDodgeRealtime = Time.unscaledTime + dodgeCooldown;

        // 无论动画事件是否触发，结束翻滚必须清无敌（防止"卡出永久无敌"）
        health?.SetInvulnerable(InvulnerableReason, false);
    }

    private void OnDisable()
    {
        // 组件被禁用时也要清，否则无敌会残留
        health?.SetInvulnerable(InvulnerableReason, false);
        hasTurnTarget = false;
    }

    /// <summary>
    /// 启动时打印四个 Roll clip 的实际时长，用于核对 dodgeDuration。
    /// 对齐公式：dodgeDuration ≈ clip时长 × 0.837（Roll 过渡 ExitTime）+ 0.15（过渡时长）。
    /// clip 偏短 → 滚完罚站；偏长 → 动画尾巴被切。
    /// </summary>
    private void LogRollClipLengths()
    {
        if (animator == null || animator.runtimeAnimatorController == null) return;
        foreach (var clip in animator.runtimeAnimatorController.animationClips)
        {
            if (clip.name.Contains("Roll") || clip.name.Contains("roll"))
                Debug.Log($"[PlayerDodge] Roll clip「{clip.name}」时长 {clip.length:F2}s，" +
                          $"建议 dodgeDuration ≈ {clip.length * 0.837f + 0.15f:F2}s", this);
        }
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

using System.Collections;
using System.Collections.Generic;
using UnityEngine;


/// <summary>
/// 玩家动画状态同步组件
/// 职责：将玩家状态映射到animator组件
/// </summary>
public class PlayerAnimator : MonoBehaviour
{
    Animator animator;
    PlayerController playerController;
    PlayerMovement playerMovement;

    int postrueHash;
    int moveSpeedHash;
    int turnSpeedHash;
    int jumpSpeedHash;

    float crouchThreshold = 0f;         //蹲下状态阈值
    float standThreshold = 1f;          //站立状态阈值
    float midAirThreshold = 2.2f;       //滞空状态阈值
    float lockRotateSpeed = 8f;         //锁定时旋转速度

    [Tooltip("非锁定转身增益：每弧度误差每秒转多少度。200 ≈ 0.3s 收敛，调大更跟手")]
    [SerializeField] private float turnGainDegPerRad = 200f;

    #region MoveSpeed 档位（必须与 PlayerMove.controller / StandState 树里各行的 Y 坐标一致）
    // StandState 是一棵 FreeformCartesian2D 树：X = TurnSpeed（弧度）Y = MoveSpeed。
    // 注意 Y 是「档位」不是「米/秒」：待机 0 / 走 2 / 跑 5 / 后跑 3，蹲行在 SquatState 树里是 1.1214。
    // 【为什么以前走路像在加速】
    //   旧代码写的是 movement.magnitude * walkSpeed（走 = 3、跑 = 6），
    //   走路的点落在 y=2(走) 和 y=5(跑) 之间 → 混进约 1/3 的跑步动画，腿变成跑步节奏。
    //   位移快慢必须交给 PlayerMovement 里的 Animator.speed 单独负责，档位只管「选哪套 clip」。
    //   改动画树（挪动子节点位置）就必须同步改这里的数字。
    #endregion
    [Header("MoveSpeed 档位（= PlayerMove.controller 里各行的 Y 坐标，不是 m/s）")]
    [Tooltip("待机行 Y：Idle1(0,0) + Turn_90L / Turn_90R(±1.39 / ±2.47, 0)")]
    [SerializeField] private float idleMoveTier = 0f;
    [Tooltip("走路行 Y：Walk_Forward / Leftward / Rightward / Forward_Left / Forward_Right")]
    [SerializeField] private float walkMoveTier = 2f;
    [Tooltip("跑步行 Y：Run_Forward / Leftward / Rightward / Backward_Left / Backward_Right")]
    [SerializeField] private float runMoveTier = 5f;
    [Tooltip("蹲行行 Y：SquatState 树里的移动行（树里已写 TimeScale=2，不要再叠加倍率）")]
    [SerializeField] private float crouchMoveTier = 1.1214f;

    public void Init(Animator anim, PlayerController pc, PlayerMovement pm)
    {
        animator = anim;
        playerController = pc;
        playerMovement = pm;

        //缓存ainimator 参数Hash
        postrueHash = Animator.StringToHash("PlayerState");
        moveSpeedHash = Animator.StringToHash("MoveSpeed");
        turnSpeedHash = Animator.StringToHash("TurnSpeed");
        jumpSpeedHash = Animator.StringToHash("JumpSpeed");

        //初始化默认值
        animator.SetFloat(postrueHash,standThreshold);
        animator.SetFloat(moveSpeedHash,0f);
        animator.SetFloat(turnSpeedHash,0f);
        
    }

    public void Tick()
    {
        SetupAnimator();
    }

    void SetupAnimator()
    {
        // 翻滚期间不更新 BlendTree 参数，避免 locomotion 抢状态
        if(playerController.playerDodge != null && playerController.playerDodge.IsDodging) return;

        PlayerController.E_PlayerPosture posture = playerController.PlayerPosture;
        PlayerController.E_LocomotionState loco = playerController.LocomotionState;
        PlayerController.E_ArmState arm = playerController.ArmState;
        Vector3 movement = playerMovement.GetPlayerMovement();

        // --- 姿态参数设置 ---
        if (posture == PlayerController.E_PlayerPosture.Stand)
        {
            animator.SetFloat(postrueHash, standThreshold, 0.1f, Time.deltaTime);
            SetLocomotionSpeed(loco, movement);
        }
        else if (posture == PlayerController.E_PlayerPosture.Crouch)
        {
            animator.SetFloat(postrueHash, crouchThreshold, 0.1f, Time.deltaTime);
            SetLocomotionSpeedCrouch(loco, movement);
        }
        else if (posture == PlayerController.E_PlayerPosture.Jumping)
        {
            animator.SetFloat(postrueHash, midAirThreshold, 0.1f, Time.deltaTime);
            animator.SetFloat(jumpSpeedHash, playerMovement.GetVerticalVelocity(), 0.1f, Time.deltaTime);
            animator.SetFloat("FeetTween", playerMovement.GetFeetTween());
        }
        else if (posture == PlayerController.E_PlayerPosture.Landing)
        {
            // 着陆期间保持 StandState（档位 1）。
            // 旧代码写的是 GetLandingThreshold()（0.5~1），等于把 SquatState 蹲姿树
            // 混进 StandState 最多 50%，落地后 0.15s 内走路动画是脏的
            // （蹲姿树的 X/Y 要的是 TurnSpeed/MoveSpeed，本身就不是给这个用法设计的）。
            // 落地缓冲应该走 AirState 树里 (0,-10) 的 ARPG_Warrior_Landing 剪辑，
            // 那需要把 PlayerState 设为 2 并让 JumpSpeed 压到 -10，属于跳跃系统的后续改动。
            animator.SetFloat(postrueHash, standThreshold, 0.08f, Time.deltaTime);
            SetLocomotionSpeed(loco, movement);
        }
        else if (posture == PlayerController.E_PlayerPosture.Falling)
        {
            animator.SetFloat(postrueHash, midAirThreshold, 0.1f, Time.deltaTime);
            animator.SetFloat(jumpSpeedHash, playerMovement.GetVerticalVelocity(), 0.1f, Time.deltaTime);
        }

        // --- 手臂状态：旋转和 turnSpeed ---
        if (arm == PlayerController.E_ArmState.Lock && playerController.LockedEnemy != null)
        {
            // 锁定模式：转向敌人
            Vector3 dirToEnemy = playerController.LockedEnemy.transform.position - transform.position;
            dirToEnemy.y = 0;
            if (dirToEnemy.sqrMagnitude > 0.01f)
            {
                Quaternion targetRot = Quaternion.LookRotation(dirToEnemy);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, lockRotateSpeed * Time.deltaTime);
            }

            // turnSpeed 设为移动方向角度，供 Blend Tree 混合 strafe 动画
            float rad = Mathf.Atan2(movement.x, movement.z);
            animator.SetFloat(turnSpeedHash, rad, 0.1f, Time.deltaTime);
        }
        else if (arm == PlayerController.E_ArmState.Normal)
        {
            // 正常模式：跟随移动方向旋转
            float rad = Mathf.Atan2(movement.x, movement.z);
            animator.SetFloat(turnSpeedHash, rad, 0.1f, Time.deltaTime);

            if (!playerController.MeleeFighter.inAction)
            {
                // rad 是「朝向误差」（弧度），这里转成度：gain 200 → 每弧度误差每秒转 200 度。
                // 60fps 下每帧只吃掉误差的约 5.8%，等效时间常数 ≈0.29s，不会过冲振荡。
                // 觉得转身发飘就把 turnGainDegPerRad 调到 350~500。
                transform.Rotate(0, rad * turnGainDegPerRad * Time.deltaTime, 0f);
            }
        }
    }

    /// <summary>
    /// 设置站立或者着陆姿态下的 MoveSpeed 档位
    /// </summary>
    /// <param name="loco">当前行动状态</param>
    /// <param name="movement">本地空间移动向量（长度 0~1 表示输入量）</param>
    void SetLocomotionSpeed(PlayerController.E_LocomotionState loco, Vector3 movement)
    {
        float tier = ResolveMoveTier(loco, movement, idleMoveTier, walkMoveTier, runMoveTier);
        animator.SetFloat(moveSpeedHash, tier, 0.1f, Time.deltaTime);
    }

    /// <summary>
    /// 设置蹲下姿态的 MoveSpeed 档位（SquatState 树只有「待机」和「蹲行」两行）
    /// </summary>
    /// <param name="loco">当前行动状态</param>
    /// <param name="movement">本地空间移动向量（长度 0~1 表示输入量）</param>
    void SetLocomotionSpeedCrouch(PlayerController.E_LocomotionState loco, Vector3 movement)
    {
        float tier = ResolveMoveTier(loco, movement, idleMoveTier, crouchMoveTier, crouchMoveTier);
        animator.SetFloat(moveSpeedHash, tier, 0.1f, Time.deltaTime);
    }

    /// <summary>
    /// 把行动状态翻译成 BlendTree 的 MoveSpeed 档位（注意：是档位，不是米/秒）。
    ///
    /// 乘输入量是为了保留手柄摇杆「半推」的模拟混合：0→1 就是待机↔走/跑之间的平滑过渡，
    /// 这样根位移也会跟着按比例缩小，脚和速度依然同步。
    ///
    /// 【必须 Clamp01】键盘斜向 WASD 得到的输入向量长度是 √2≈1.414，
    /// 不夹住的话走路档位会变成 2 × 1.414 = 2.83，又落回「走↔跑之间」把跑步动画混进来。
    /// </summary>
    /// <param name="loco">当前行动状态</param>
    /// <param name="movement">本地空间移动向量（长度 0~1 表示输入量）</param>
    /// <param name="idleTier">待机档位</param>
    /// <param name="walkTier">低速档位（走路 / 蹲行）</param>
    /// <param name="runTier">高速档位（奔跑；蹲下没有跑，传蹲行档位即可）</param>
    float ResolveMoveTier(PlayerController.E_LocomotionState loco, Vector3 movement,
                          float idleTier, float walkTier, float runTier)
    {
        float amount = Mathf.Clamp01(movement.magnitude);

        switch (loco)
        {
            case PlayerController.E_LocomotionState.Run:
                return Mathf.Lerp(idleTier, runTier, amount);

            case PlayerController.E_LocomotionState.Walk:
                return Mathf.Lerp(idleTier, walkTier, amount);

            default: // Idle
                return idleTier;
        }
    }
}


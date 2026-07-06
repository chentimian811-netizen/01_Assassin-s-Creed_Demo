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
        // 翻滚期间不更新BlendTree参数（阶段2实现PlayerDodge后取消注释）
        // if(playerController.playerDodge != null && playerController.playerDodge.IsDodging) return;

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
            animator.SetFloat(postrueHash, playerMovement.GetLandingThreshold(), 0.08f, Time.deltaTime);
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
                transform.Rotate(0, rad * 200 * Time.deltaTime, 0f);
            }
        }
    }

    /// <summary>
    /// 设置站立或者着陆姿态下的移动速度参数
    /// </summary>
    /// <param name="loco"></param>
    /// <param name="movement"></param>
    void SetLocomotionSpeed(PlayerController.E_LocomotionState loco,Vector3 movement)
    {
        switch (loco)
        {
            case PlayerController.E_LocomotionState.Idle:
                animator.SetFloat(moveSpeedHash,0f,0.1f,Time.deltaTime);
                break;
        
            case PlayerController.E_LocomotionState.Walk:
                animator.SetFloat(moveSpeedHash,movement.magnitude * playerMovement.walkSpeed,0.1f,Time.deltaTime);
                break;
        
            case PlayerController.E_LocomotionState.Run:
                animator.SetFloat(moveSpeedHash,movement.magnitude * playerMovement.runSpeed,0.1f,Time.deltaTime);
                break;
        }
    }

    /// <summary>
    /// 设置蹲下姿态的移动速度参数
    /// </summary>
    /// <param name="loco"></param>
    /// <param name="movement"></param>
    void SetLocomotionSpeedCrouch(PlayerController.E_LocomotionState loco,Vector3 movement)
    {
        switch (loco)
        {
            case PlayerController.E_LocomotionState.Idle:
                animator.SetFloat(moveSpeedHash,0f,0.1f,Time.deltaTime);
                break;
            default:
                animator.SetFloat(moveSpeedHash,movement.magnitude * playerMovement.crouchSpeed,0.1f,Time.deltaTime);
                break;
        }
    }
}


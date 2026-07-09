using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 玩家移动与物理组件
/// 职责：地面检测 重力计算 跳跃 输入方向计算 CharacterController 移动
/// </summary>
/// </summary>
public class PlayerMovement : MonoBehaviour
{
    #region 引用
    CharacterController characterController;
    Animator Animator;
    MeleeFighter meleeFighter;
    PlayerController playerController;
    #endregion

    #region 移动速度
    public float crouchSpeed = 1.5f; //蹲下移动速度
    public float walkSpeed = 3f;     //行走速度
    public float runSpeed = 6f;      //奔跑速度
    #endregion

    #region 重力与跳跃参数
    
    public float gravity = -9.8f;        // 重力加速度
    public float maxHeight = 1.5f;       // 最大跳跃高度
    float fallMultiplier = 1.5f;         // 下落加速度倍数
    float jumpCD = 0.15f;                // 跳跃冷却时间
    float groundCheckOffset = 0.5f;  //地面检测射线的偏移量
    float fallHeight = 0.5f; //跌落的最小阈值
    #endregion

    #region 运行时状态
    Vector2 moveInput;      //输入的二维向量
    Vector3 playerMovement = Vector3.zero;//玩家移动向量为(0,0,0)
    float VerticalVelocity;//垂直速度
    bool isGround;         //是否着陆
    bool isLanding;        //是否处于落地CD中
    bool couldFall;        //是否可能跌落
    float feetTween;       //滞空左右脚动画混合值
    float LandingThreshold;//着陆动画混合值
    //输入标记
    bool isRunning;//是否处于奔跑状态
    bool isCrouch;
    bool isJumping;
    #endregion

    #region 速度缓存(用于平滑空中移动)
    static readonly int CACHE_SIZE = 3;//缓存三帧
    Vector3[] velCache = new Vector3[CACHE_SIZE];
    int currentChaCheIndex = 0;
    Vector3 averageVel = Vector3.zero;
    #endregion
    
    #region 对外暴露(给playercontroller读取)
    /// <summary>当前移动向量（本地空间）</summary>
    public Vector3 GetPlayerMovement() => playerMovement;
    /// <summary>当前垂直速度</summary>
    public float GetVerticalVelocity() => VerticalVelocity;

    /// <summary>当前着陆混合值</summary>
    public float GetLandingThreshold() => LandingThreshold;
    /// <summary>当前滞空脚部混合值</summary>
    public float GetFeetTween() => feetTween;
    /// <summary>是否正在奔跑</summary>
    public bool IsRunning() => isRunning;
    /// <summary>是否蹲下</summary>
    public bool IsCrouching() => isCrouch;
    /// <summary>获取原始移动输入（供翻滚方向计算使用）</summary>
    public Vector2 GetMoveInputRaw() => moveInput;
    
    ///<summary>
    /// 重置垂直速度（翻滚开始时调用，防止空中翻滚继承下落速度）
    /// </summary>
    public void ResetVerticalVelocity()
    {
        VerticalVelocity = 0f;
    }
    #endregion

    /// <summary>
    /// 初始化组件
    /// </summary>
    /// <param name="cc"></param>
    /// <param name="anim"></param>
    /// <param name="mf"></param>
    /// <param name="pc"></param>
    public void Init(CharacterController cc,Animator anim,MeleeFighter mf,PlayerController pc)
    {
        characterController = cc;
        Animator = anim;
        meleeFighter = mf;
        playerController = pc;
    }

    /// <summary>
    /// 每帧调用 由playercontroller.updateq驱动
    /// </summary>
    public void Tick()
    {
        CheckGround();
        SwitchPlayerState();
        CaculateGravity();
        Jump();
        CaculateInputDirection();
        AnimatorMove();
    }

    #region 输入处理
    public void HandleMoveInput(InputAction.CallbackContext context)
    {
        moveInput = context.ReadValue<Vector2>();//将输入原始数据转为二维向量 方便后续调取
    }

    public void HandleRunInput(InputAction.CallbackContext context)
    {
        isRunning = context.ReadValueAsButton();
    }

    public void HandleCrouchInput(InputAction.CallbackContext context)
    {
        isCrouch = context.ReadValueAsButton();
    }

    public void HandleJumpInput(InputAction.CallbackContext context)
    {
        isJumping = context.ReadValueAsButton();
    }

    /// <summary>
    /// 角色死亡时 清空所有输入状态
    /// </summary>
    public void ClearInput()
    {
        moveInput = Vector2.zero;
        isRunning = false;
        isCrouch = false;
        isJumping = false;
    }
    #endregion

    /// <summary>
    /// 地面检测 球形射线检测是否着地
    /// </summary>
    void CheckGround()
    {
        if (Physics.SphereCast(
            transform.position + (Vector3.up * groundCheckOffset), //球形检测射线从人物向上0.5米开始
            characterController.radius,// 使用角色胶囊的半径, 
            Vector3.down,//向下探测
            out RaycastHit hit,//输入碰撞的信息
            groundCheckOffset - characterController.radius + 2 * characterController.skinWidth))//检测距离
        {
            isGround = true;
        }
        else
        {
            isGround = false;
            couldFall = !Physics.Raycast(transform.position, Vector3.down, fallHeight);
        }
    }

    /// <summary>
    /// 状态切换 根据当前物理状态何输入切换玩家姿态何行动状态
    /// </summary>
    void SwitchPlayerState()
    {
        //如果不在地面则切换成滞空状态
        if (!isGround)
        {
            if(isLanding) return;

            //垂直速度大于0
            if (VerticalVelocity > 0)
            {
                //在跳跃中
                playerController.PlayerPosture = PlayerController.E_PlayerPosture.Jumping;
            }
            //如果不是处于坠落
            else if (playerController.PlayerPosture != PlayerController.E_PlayerPosture.Falling)
            {
                //并且是跌落
                if (couldFall)
                {
                    //在坠落中
                    playerController.PlayerPosture = PlayerController.E_PlayerPosture.Falling;
                }
            }

        }
        //如果是处于跳跃
        else if (playerController.PlayerPosture == PlayerController.E_PlayerPosture.Jumping 
              || playerController.PlayerPosture == PlayerController.E_PlayerPosture.Falling)
        {
            if(VerticalVelocity <= 0)
            {
                StartCoroutine(CoolDownJump());
            }
        }
        else if(playerController.PlayerPosture == PlayerController.E_PlayerPosture.Landing)
        {
            
        }

        else if (isLanding)
        {
            playerController.PlayerPosture = PlayerController.E_PlayerPosture.Landing;
        }
        else if (isCrouch)
        {
            playerController.PlayerPosture = PlayerController.E_PlayerPosture.Crouch;
        }
        else
        {
            playerController.PlayerPosture = PlayerController.E_PlayerPosture.Stand;
        }

        //行动状态切换
         if (moveInput.magnitude == 0)
        {
            playerController.LocomotionState = PlayerController.E_LocomotionState.Idle;
        }
        else if (isRunning)
        {
            playerController.LocomotionState = PlayerController.E_LocomotionState.Run;
        }
        else
        {
            playerController.LocomotionState = PlayerController.E_LocomotionState.Walk;
        }
    }

    /// <summary>
    /// 着陆冷却协程
    /// </summary>
    /// <returns></returns>
    IEnumerator CoolDownJump()
    {
        LandingThreshold = Mathf.Clamp(VerticalVelocity, -10, 0);
        LandingThreshold /= 20f;//[-0.5,0]
        LandingThreshold += 1f;//[0.5,1.0]
        isLanding = true;
        playerController.PlayerPosture = PlayerController.E_PlayerPosture.Landing;
        yield return new WaitForSeconds(jumpCD);
        isLanding = false;
        playerController.PlayerPosture = PlayerController.E_PlayerPosture.Stand;
    }

    /// <summary>
    /// 重力计算 根据当前的姿态计算重力何垂直速度
    /// </summary>
    void CaculateGravity()//重力
    {
        if (playerController.PlayerPosture != PlayerController.E_PlayerPosture.Jumping 
            && playerController.PlayerPosture != PlayerController.E_PlayerPosture.Falling)
        {
            if (!isGround)
            {
                VerticalVelocity += gravity * fallMultiplier * Time.deltaTime;
            }
            else
            {
                //当在地面上时 给予一个向下的力 使得贴地面
                VerticalVelocity = gravity * Time.deltaTime;
            }
        }
        else
        {
            if (VerticalVelocity <= 0)
            {
                VerticalVelocity += gravity * fallMultiplier * Time.deltaTime;
            }
            else
            {
                //当不在地面上是 给予向下的力 实现自由落体
                VerticalVelocity += gravity * Time.deltaTime;
            }
        }
    }

    /// <summary>
    /// 跳跃 地面+跳跃输入时给予瞬时向上的力
    /// </summary>
    void Jump()//跳跃
    {
        //当角色在地面并且 按下跳跃 则获得一个瞬时向上的力
        if (playerController.PlayerPosture == PlayerController.E_PlayerPosture.Stand && isJumping)
        {
            VerticalVelocity = Mathf.Sqrt(-2 * gravity * maxHeight);

            playerController.PlayerPosture = PlayerController.E_PlayerPosture.Jumping;
            //计算动画脚本混合值
            feetTween = Mathf.Repeat(Animator.GetCurrentAnimatorStateInfo(0).normalizedTime, 1);
            feetTween = feetTween < 0.5 ? 1 : -1; // 0-0.5 前半步 左脚在前 1 ; 0.5-1 后半部 右脚在前 -1

            if (playerController.LocomotionState == PlayerController.E_LocomotionState.Run)
            {
                feetTween *= 3;
            }
            else if (playerController.LocomotionState == PlayerController.E_LocomotionState.Walk)
            {
                feetTween *= 2;
            }
            else
            {
                feetTween = UnityEngine.Random.Range(0.5f, 1f) * feetTween;
            }
        }
    }

    /// <summary>
    /// 根据锁定状态计算本地空间的移动方向
    /// </summary>
    void CaculateInputDirection()
    {
        if (playerController.IsLocking && playerController.LockedEnemy != null)
        {
            // 索敌模式：以玩家到敌人的方向为前方向
            Vector3 toEnemy = playerController.LockedEnemy.transform.position - transform.position;
            toEnemy.y = 0;
            Vector3 forward = toEnemy.normalized;
            Vector3 right = Vector3.Cross(Vector3.up, forward);

            playerMovement = forward * moveInput.y + right * moveInput.x;
            playerMovement = transform.InverseTransformVector(playerMovement);
        }
        else
        {
            // 正常模式：跟随摄像机方向
            Transform camTf = CameraManager.Instance.MainCameraTransform;
            Vector3 caneraForward = new Vector3(camTf.forward.x,0,camTf.forward.z).normalized;
            playerMovement = caneraForward * moveInput.y + camTf.right * moveInput.x;
            playerMovement = transform.InverseTransformVector(playerMovement);
        }
    }

    /// <summary>
    /// 根据姿态执行实际的CharacterController移动
    /// </summary>
    void AnimatorMove()//动画驱动移动
    {
        // 翻滚期间完全由Root Motion驱动位移 跳过所有代码移动（阶段2实现PlayerDodge后取消注释）
        // if(playerController.playerDodge != null && playerController.playerDodge.IsDodging)
        // {
        //     Vector3 dogeDelta = Animator.deltaPosition;
        //     dogeDelta.y = VerticalVelocity * Time.deltaTime;
        //     characterController.Move(dogeDelta);
        //     return;
        // }

        if (playerController.PlayerPosture != PlayerController.E_PlayerPosture.Jumping 
            && playerController.PlayerPosture != PlayerController.E_PlayerPosture.Falling)
        {
            if (playerController.IsLocking)
            {
                // 索敌模式：禁用 root motion 水平移动，用代码控制 strafe 方向
                Vector3 worldMove = transform.TransformVector(playerMovement);
                worldMove.y = 0;
                float speed = (isRunning ? runSpeed : walkSpeed);
                characterController.Move(worldMove * speed * Time.deltaTime);
                characterController.Move(Vector3.up * VerticalVelocity * Time.deltaTime);
            }
            else
            {
                Vector3 playerDelataMovement = Animator.deltaPosition;
                playerDelataMovement.y = VerticalVelocity * Time.deltaTime;
                characterController.Move(playerDelataMovement);
            }
            averageVel = AverageVel(Animator.velocity);
        }
        else
        {
            averageVel.y = VerticalVelocity;
            Vector3 playerDelataMovement = averageVel * Time.deltaTime;
            characterController.Move(playerDelataMovement);
        }
    }

    /// <summary>
    /// 速度平滑缓存
    /// </summary>
    /// <param name="newVel"></param>
    /// <returns></returns>
    Vector3 AverageVel(Vector3 newVel)//评价速度计算
    {
        velCache[currentChaCheIndex] = newVel;
        currentChaCheIndex++;
        currentChaCheIndex %= CACHE_SIZE;
        Vector3 average = Vector3.zero;
        foreach (Vector3 vel in velCache)
        {
            average += vel;
        }
        return average / CACHE_SIZE;
    }

    /// <summary>
    /// 回调：动画移动后引用旋转
    /// </summary>
    private void OnAnimatorMove()
    {
        // if (!meleeFighter.inCounter && !playerController.IsLocking)
        // {
            
        // }

        //翻滚期间不应用Root Motion旋转(由翻滚协程控制朝向)（阶段2实现PlayerDodge后取消注释）
        // if(playerController.playerDodge != null && playerController.playerDodge.IsDodging) return;

        // if (!playerController.IsLocking && !meleeFighter.inAction)
        // {
        //     transform.rotation *= Animator.deltaRotation;
        // }
    }
}

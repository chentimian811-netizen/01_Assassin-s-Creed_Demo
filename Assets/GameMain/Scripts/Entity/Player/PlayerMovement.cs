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
    // 【这三个值 = 动画 clip 的「自然速度」，不是随便调的手感数字】
    //   自然速度 = 剪辑根位移总长 ÷ 循环时长，实测值写在下面 walkRootMotionRefSpeed 上。
    //   两者对齐后 Animator.speed 恒为 1，完全按动画师原始节奏播放，滑步最小。
    //   想让人走快一点：改 walkSpeed，Animator.speed 会自动补偿（夹在 min/maxAnimSpeed 内）。
    //   注意：蹲下以外的位移都来自 root motion，所以改这里=同时改动画节奏和实际速度，不会脱节。
    [Tooltip("蹲下移动速度 m/s。仅当 HumanoidCrouch 剪辑没有根位移时作为代码位移的兜底")]
    public float crouchSpeed = 1.12f;
    [Tooltip("行走速度 m/s。实测 ARPG_Warrior_Walk_Forward_Rootmotion：2.2854m ÷ 0.98333s = 2.33 m/s")]
    public float walkSpeed = 2.33f;
    [Tooltip("奔跑速度 m/s。实测 ARPG_Warrior_Run_Forward_Rootmotion：2.5162m ÷ 0.73333s = 3.43 m/s")]
    public float runSpeed = 3.43f;
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
    /// <summary>是否在地面（供翻滚/跳跃判定）</summary>
    public bool IsGrounded => isGround;
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

    /// <summary>由 Shift 双功能逻辑调用：长按疾跑时置 true</summary>
    public void SetRunning(bool value) => isRunning = value;

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
    [Header("根运动自然速度（ARPG Rootmotion：走 2.33 / 跑 3.43 m/s）")]
    [Tooltip("走循环自然速度 m/s。walkSpeed / 此值 = Animator.speed")]
    [SerializeField] private float walkRootMotionRefSpeed = 2.33f;
    [Tooltip("跑循环自然速度 m/s。runSpeed / 此值 = Animator.speed")]
    [SerializeField] private float runRootMotionRefSpeed = 3.43f;
    [Tooltip("Animator 播放倍率下限：目标速度远低于剪辑自然速度时，动画最慢只放到这里")]
    [SerializeField] private float minAnimSpeed = 0.85f;
    [Tooltip("Animator 播放倍率上限：动画绝不被快放超过这个倍数，从根上堵掉「越跑越快」的观感")]
    [SerializeField] private float maxAnimSpeed = 1.25f;
    [Tooltip("判定「剪辑自带根位移」的阈值（米/帧）。低于它认为是原地剪辑，蹲下才会退回代码位移")]
    [SerializeField] private float rootMotionEpsilon = 0.0005f;

    void AnimatorMove()//动画驱动移动
    {
        // 1) 翻滚期间水平位移由 PlayerDodge 曲线驱动。
        // 这里绝不能吃 Animator.deltaPosition：CrossFade 混合期会残留走路位移，
        // 表现为「先滑一小段才开始滚」。
        if (playerController.playerDodge != null && playerController.playerDodge.IsDodging)
        {
            ResetLocomotionAnimSpeed();
            characterController.Move(Vector3.up * VerticalVelocity * Time.deltaTime);
            return;
        }

        // 2) 攻击/受击：吃 clip 自带根位移（前冲/后仰），但不套走路速度缩放，避免被放大成「滑出去」
        if (meleeFighter != null && meleeFighter.inAction)
        {
            ResetLocomotionAnimSpeed();
            Vector3 attackMove = Animator.deltaPosition;
            attackMove.y = VerticalVelocity * Time.deltaTime;
            characterController.Move(attackMove);
            return;
        }

        // 3) 滞空：不吃 Airborne clip 的根位移，用起跳前缓存的地面速度做惯性滑行
        if (playerController.PlayerPosture == PlayerController.E_PlayerPosture.Jumping
            || playerController.PlayerPosture == PlayerController.E_PlayerPosture.Falling)
        {
            ResetLocomotionAnimSpeed();
            averageVel.y = VerticalVelocity;
            characterController.Move(averageVel * Time.deltaTime);
            return;
        }

        // 4) 地面移动：水平位移 100% 来自 root motion，锁定/非锁定走同一套逻辑。
        //
        // 【为什么锁定模式也要吃 root motion】
        //   StandState 树里走路行和跑步行放的都是四向 Rootmotion 剪辑
        //   （Walk_Leftward / Walk_Forward_Right / Run_Backward_Left …），
        //   横移的根位移就烘焙在 clip 里。以前锁定模式把它关掉、改按 walkSpeed 用代码推，
        //   于是「代码按 3m/s 推 + clip 按 2.33m/s 摆腿」，脚必然滑 —— 这就是锁定模式滑步的根源。
        Animator.speed = ResolveLocomotionAnimSpeed();
        characterController.Move(BuildGroundDelta());
        averageVel = AverageVel(Animator.velocity);
    }

    /// <summary>
    /// 本帧地面移动的位移量。
    ///
    /// 正常情况直接吃 Animator.deltaPosition —— 位移和脚步来自同一段剪辑，天然同步，所以不滑步。
    /// 唯一例外是蹲下：SquatState 用的是 HumanoidCrouch 的原地剪辑（树里还写了 TimeScale=2），
    /// 很可能没有根位移曲线。这种情况若还只吃 deltaPosition，人就会「蹲着原地踏步」，
    /// 所以检测到水平根位移≈0 时才退回代码位移。
    /// </summary>
    Vector3 BuildGroundDelta()
    {
        Vector3 rootDelta = Animator.deltaPosition;
        float planar = new Vector2(rootDelta.x, rootDelta.z).magnitude;

        Vector3 horizontal;
        if (!isCrouch || planar > rootMotionEpsilon)
        {
            // 剪辑自带根位移：原样使用，不额外乘任何速度，避免位移比脚步快
            horizontal = new Vector3(rootDelta.x, 0f, rootDelta.z);
        }
        else
        {
            // 原地剪辑兜底：按 crouchSpeed 用代码推
            Vector3 worldMove = transform.TransformVector(playerMovement);
            horizontal = new Vector3(worldMove.x, 0f, worldMove.z) * crouchSpeed * Time.deltaTime;
        }

        horizontal.y = VerticalVelocity * Time.deltaTime;
        return horizontal;
    }

    /// <summary>
    /// 本帧 Locomotion 动画该用多少倍率播放。
    ///
    /// 【待机为什么必须回 1】
    ///   以前算倍率的分支只区分「跑/非跑」（targetSpeed = isRunning ? runSpeed : walkSpeed），
    ///   待机也被算成 walkSpeed/2.33 ≈ 1.29 —— 站着不动动画却在 1.29 倍速播，
    ///   这就是「待机也像在加速」的原因。
    ///
    /// 【蹲下为什么也回 1】
    ///   SquatState 树里移动剪辑已经写了 TimeScale = 2，节奏交给树负责，这里不再叠一层。
    ///
    /// 【为什么倍率只放 Animator.speed，不放 MoveSpeed】
    ///   MoveSpeed 是 blend 档位（决定播哪套 clip），Animator.speed 才是「多快」。
    ///   两者同时表达速度 = 双重记账，脚和位移脱钩 = 滑步。
    /// </summary>
    float ResolveLocomotionAnimSpeed()
    {
        if (playerController.LocomotionState == PlayerController.E_LocomotionState.Idle || isCrouch)
            return 1f;

        bool running = playerController.LocomotionState == PlayerController.E_LocomotionState.Run;
        float targetSpeed = running ? runSpeed : walkSpeed;
        float naturalSpeed = running ? runRootMotionRefSpeed : walkRootMotionRefSpeed;
        if (naturalSpeed <= 0.01f) return 1f;

        // root motion 位移和脚步节奏会被同一个倍率缩放，所以调速度不会引入滑步
        return Mathf.Clamp(targetSpeed / naturalSpeed, minAnimSpeed, maxAnimSpeed);
    }

    void ResetLocomotionAnimSpeed()
    {
        if (Animator != null && !Mathf.Approximately(Animator.speed, 1f))
            Animator.speed = 1f;
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

        //翻滚期间不应用 Root Motion 旋转（朝向由 PlayerDodge 处理）
        if(playerController.playerDodge != null && playerController.playerDodge.IsDodging) return;

        // if (!playerController.IsLocking && !meleeFighter.inAction)
        // {
        //     transform.rotation *= Animator.deltaRotation;
        // }
    }
}

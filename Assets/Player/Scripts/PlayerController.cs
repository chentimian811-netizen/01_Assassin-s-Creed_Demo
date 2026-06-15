using System;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem;
using Cinemachine;
using UnityEngine.Rendering;

public class PlayerController : MonoBehaviour
{
    Transform PlayerTransform;
    Animator Animator;
    CharacterController characterController;
    MeleeFighter meleeFighter;
    [Header("远程武器")]
    RangedFighter rangedFighter;

    public EnemyController targetEnemy;

    public enum E_PlayerPosture//玩家姿态枚举
    {
        Crouch,//蹲下
        Falling,//下落
        Stand,//站立
        Jumping,//滞空
        Landing//着陆
    }
    public E_PlayerPosture PlayerPosture = E_PlayerPosture.Stand;//规定玩家的初始姿态

    float crouchThreshold = 0f;//姿态阈值设定
    float standThreshold = 1f;
    float midAirThreshold = 2.2f; //出现抖动可以调高阈值
    float LandingThreshold = 1f;

    public enum E_LocomotionState//玩家行动状态枚举
    {
        Idle,
        Walk,
        Run
    }
    public E_LocomotionState LocomotionState = E_LocomotionState.Idle;//规定玩家的初始动作

    public enum E_ArmState//玩家瞄准状态枚举 
    {
        Normal,
        Aim,
        Lock,
    }
    public E_ArmState ArmState = E_ArmState.Normal;//初始攻击

    public float crouchSpeed = 1.5f;
    public float walkSpeed = 3f;
    public float runSpeed = 6f;

    Vector2 moveInput;//用二维值 存贮玩家输入的前后左右的值

    bool isRunning;//是否处于奔跑状态
    bool isCrouch;
    bool isAiming = false;
    bool isJumping;
    public bool isLocking { get; private set; }
    EnemyController lockedEnemy;
    float lockRotateSpeed = 8f;
    float lockDistance = 15f;
    [HideInInspector] public bool acceptInput = true; //拾取时 冻结玩家输入
    WeaponPickup nearestPickup;
    public void SetNearestPickup(WeaponPickup pickup) { nearestPickup = pickup; }

    ShopNPC nearestShopNPC;
    public void SetNearestShopNPC(ShopNPC npc){nearestShopNPC = npc;}

    bool isMainMenuOpen;


    int postrueHash;
    int moveSpeedHash;
    int turnSpeedHash;
    int jumpSpeedHash;
    int feetTweensHash;

    Vector3 playerMovement = Vector3.zero;//玩家移动向量为(0,0,0)

    public float gravity = -9.8f;//重力

    float VerticalVelocity;//垂直速度

    //public float jumpedVelocity = 5f;//跳跃速度

    //最大的跳跃高度
    public float maxHeight = 1.5f;

    static readonly int CACHE_SIZE = 3;//缓存三帧
    Vector3[] velCache = new Vector3[CACHE_SIZE];
    int currentChaCheIndex = 0;
    Vector3 averageVel = Vector3.zero;

    //下落加速度的倍数
    float fallMultiplier = 1.5f;

    //玩家是否着地
    bool isGround;

    //是否处于CD中
    bool isLanding;

    //玩家是否跌落
    bool couldFall;

    //跌落的最小数值 如果低于此高度 就不会跌落
    float fallHeight = 0.5f;

    //地面检测射线的偏移量
    float groundCheckOffset = 0.5f;

    //滞空左右脚状态
    float feetTween;

    //跳跃CD
    float jumpCD = 0.15f;

    // Start is called before the first frame update

    public void Awake()
    {
        meleeFighter = GetComponent<MeleeFighter>();
        rangedFighter = GetComponent<RangedFighter>();
    }
    void Start()
    {
        PlayerTransform = transform;//获得玩家位置
        Animator = GetComponent<Animator>();//获取动画组件
        characterController = GetComponent<CharacterController>();//获得角色组件

        postrueHash = Animator.StringToHash("PlayerState");//用哈希值存贮 资源占用更少
        moveSpeedHash = Animator.StringToHash("MoveSpeed");
        turnSpeedHash = Animator.StringToHash("TurnSpeed");
        jumpSpeedHash = Animator.StringToHash("JumpSpeed");
        feetTweensHash = Animator.StringToHash("FeetTween");

        Cursor.lockState = CursorLockMode.Locked;//隐藏玩家鼠标

        Animator.SetFloat(postrueHash, standThreshold);
        Animator.SetFloat(moveSpeedHash, 0f);
        Animator.SetFloat(turnSpeedHash, 0f);

        Debug.Log("当前金币" + CurrencyManager.Instance.Gold);

    }
    // Update is called once per frame
    void Update()
    {
        if (meleeFighter.Health <= 0 || !acceptInput)
        {
            moveInput = Vector2.zero; // 清空移动输入
            isRunning = false;
            isCrouch = false;
            isAiming = false;
            isJumping = false;
            return;
        }

        if (UIManager.Instance.panelDict.ContainsKey(UIconst.ShopPanel))
        {
            moveInput = Vector2.zero; // 清空移动输入
            isRunning = false;
            isCrouch = false;
            isAiming = false;
            isJumping = false;
            return;
        }

        CheckGround();
        SwitchPlayerState();
        CaculateGravity();
        Jump();
        CaculateInputDirection();
        SetupAnimator();
        AnimatorMove();

        if (isLocking && lockedEnemy != null)
        {
            float dist = Vector3.Distance(transform.position, lockedEnemy.transform.position);
            if (dist > lockDistance)
            {
                UnlockEnemy();
            }
        }
    }

    #region 输入相关
    public void GetPickupInput(InputAction.CallbackContext context)
    {
        if (!context.performed) return;

        if(nearestShopNPC != null)
        {
            nearestShopNPC.OpenShop();
            return;
        }

        if (nearestPickup != null)
        {
            nearestPickup.TryEquip();
        }
    }

    public void GetMoveInput(InputAction.CallbackContext context)
    {
        moveInput = context.ReadValue<Vector2>();//将输入原始数据转为二维向量 方便后续调取
    }
    public void GetRunInput(InputAction.CallbackContext context)
    {
        isRunning = context.ReadValueAsButton();
    }

    public void GetCrouchInput(InputAction.CallbackContext context)
    {
        isCrouch = context.ReadValueAsButton();
    }

    public void GetAimInput(InputAction.CallbackContext context)
    {
        if(rangedFighter == null) return;
        if (context.performed)
        {
            //按下右键 开始瞄准
            isAiming = true;
            rangedFighter.SetAiming(true);
        }
        else if (context.canceled)
        {
            isAiming = false;
            rangedFighter.SetAiming(false);
        }
    }

    public void GetFireInput(InputAction.CallbackContext context)
    {
        if(rangedFighter == null) return;
        if(!context.performed) return;

        //获取瞄准方向(从摄像机中心发射射线)
        Vector3 aimDirection  = GetAimDirection();

        //尝试射击
        rangedFighter.TryFire(aimDirection);
    }

    public void GetJumpInInput(InputAction.CallbackContext context)
    {
        isJumping = context.ReadValueAsButton();
    }

    public void GetBackpackInput(InputAction.CallbackContext context)
    {
        if(!context.performed) return;

        isMainMenuOpen = !isMainMenuOpen;
        if(isMainMenuOpen)
        {
            Time.timeScale = 0f;//打开背包时暂停游戏
            Cursor.lockState = CursorLockMode.None;
            UIManager.Instance.OpenPanel(UIconst.MainPanel);
        }
        else
        {
            Time.timeScale = 1f;//关闭背包时恢复游戏
            Cursor.lockState = CursorLockMode.Locked;
            UIManager.Instance.ClosePanel(UIconst.MainPanel);
        }
    }

    public void GetLightAttack(InputAction.CallbackContext context)
    {
        if (!context.performed) return;

        //如果正在使用远程武器，不触发近战攻击
        WeaponSwitcher switcher = GetComponent<WeaponSwitcher>();
        if (switcher != null && switcher.IsUsingRanged) return;

        //检查是否装备了近战武器，无武器的时候拦截攻击
        WeaponManager weapManager = GetComponent<WeaponManager>();
        if(weapManager == null || weapManager.GetSlotConfig(0) == null) return;

        MeleeFighter targetFighter = null;
        if (isLocking && lockedEnemy != null)
        {
            targetFighter = lockedEnemy.Fighter;
        }

        var enemy = EnemyManager.i.GetAttackingEnemy();

        if (enemy != null && enemy.Fighter.IsCounterable && !meleeFighter.inAction && !meleeFighter.IsAttackingHit)
        {
            StartCoroutine(meleeFighter.PerformCounterAttack(enemy));
        }
        else
        {
            meleeFighter.ToTryAttack(targetFighter ?? targetEnemy?.Fighter);
        }
    }

    public void GetLockInput(InputAction.CallbackContext context)
    {
        if (!context.performed) return;

        if (isLocking)
        {
            UnlockEnemy();
        }
        else
        {
            var enemy = EnemyManager.i.GetClosesEnemyToPlayerDir();
            if (enemy != null)
            {
                LockEnemy(enemy);
            }
        }
    }

    void LockEnemy(EnemyController enemy)
    {
        isLocking = true;
        lockedEnemy = enemy;
        targetEnemy = enemy;
        ArmState = E_ArmState.Lock;

        CameraManager.Instance.LockFreeLookXAxis();

        enemy.MeshHightlighter?.HighlightMesh(true);
    }

    void UnlockEnemy()
    {
        isLocking = false;

        if (lockedEnemy != null)
            lockedEnemy.MeshHightlighter?.HighlightMesh(false);

        CameraManager.Instance.UnlockFreeLookAxes();

        lockedEnemy = null;
        targetEnemy = null;
        ArmState = E_ArmState.Normal;
    }

    public void ForceUnlock()
    {
        if (isLocking)
        {
            UnlockEnemy();
        }
    }
    #endregion

    void CheckGround()//地面检测
    {

        if (Physics.SphereCast(PlayerTransform.position + (Vector3.up * groundCheckOffset), //球形检测射线从人物向上0.5米开始
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
            couldFall = !Physics.Raycast(PlayerTransform.position, Vector3.down, fallHeight);
        }
    }

    void SwitchPlayerState()//状态切换
    {
        //如果不在地面则切换成滞空状态
        if (!isGround)
        {
            if(isLanding) return;

            //垂直速度大于0
            if (VerticalVelocity > 0)
            {
                //在跳跃中
                PlayerPosture = E_PlayerPosture.Jumping;
            }
            //如果不是处于坠落
            else if (PlayerPosture != E_PlayerPosture.Falling)
            {
                //并且是跌落
                if (couldFall)
                {
                    //在坠落中
                    PlayerPosture = E_PlayerPosture.Falling;
                }
            }

        }
        //如果是处于跳跃
        else if (PlayerPosture == E_PlayerPosture.Jumping || PlayerPosture == E_PlayerPosture.Falling)
        {
            if(VerticalVelocity <= 0)
            {
                StartCoroutine(CoolDownJump());
            }
        }
        else if(PlayerPosture == E_PlayerPosture.Landing)
        {
            
        }

        else if (isLanding)
        {
            PlayerPosture = E_PlayerPosture.Landing;
        }
        else if (isCrouch)
        {
            PlayerPosture = E_PlayerPosture.Crouch;
        }
        else
        {
            PlayerPosture = E_PlayerPosture.Stand;
        }


        if (moveInput.magnitude == 0)
        {
            LocomotionState = E_LocomotionState.Idle;
        }
        else if (isRunning)
        {
            LocomotionState = E_LocomotionState.Run;
        }
        else
        {
            LocomotionState = E_LocomotionState.Walk;
        }

        if (isLocking)
        {
            ArmState = E_ArmState.Lock;
        }
        else if (isAiming)
        {
            ArmState = E_ArmState.Aim;
        }
        else
        {
            ArmState = E_ArmState.Normal;
        }
    }

    IEnumerator CoolDownJump()//计算落地动画的混合值
    {
        LandingThreshold = Mathf.Clamp(VerticalVelocity, -10, 0);
        LandingThreshold /= 20f;//[-0.5,0]
        LandingThreshold += 1f;//[0.5,1.0]
        isLanding = true;
        PlayerPosture = E_PlayerPosture.Landing;
        yield return new WaitForSeconds(jumpCD);
        isLanding = false;
        PlayerPosture = E_PlayerPosture.Stand;
    }


    void CaculateGravity()//重力
    {
        if (PlayerPosture != E_PlayerPosture.Jumping && PlayerPosture != E_PlayerPosture.Falling)
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

    void Jump()//跳跃
    {
        //当角色在地面并且 按下跳跃 则获得一个瞬时向上的力
        if (PlayerPosture == E_PlayerPosture.Stand && isJumping)
        {
            VerticalVelocity = MathF.Sqrt(-2 * gravity * maxHeight);

            PlayerPosture = E_PlayerPosture.Jumping;
            //计算动画脚本混合值
            feetTween = Mathf.Repeat(Animator.GetCurrentAnimatorStateInfo(0).normalizedTime, 1);
            feetTween = feetTween < 0.5 ? 1 : -1; // 0-0.5 前半步 左脚在前 1 ; 0.5-1 后半部 右脚在前 -1
            if (LocomotionState == E_LocomotionState.Run)
            {
                feetTween *= 3;
            }
            else if (LocomotionState == E_LocomotionState.Walk)
            {
                feetTween *= 2;
            }
            else
            {
                feetTween = UnityEngine.Random.Range(0.5f, 1f) * feetTween;
            }
        }
    }


    void CaculateInputDirection()//输入方向的计算
    {
        if (isLocking && lockedEnemy != null)
        {
            // 索敌模式：以玩家到敌人的方向为前方向
            Vector3 toEnemy = lockedEnemy.transform.position - PlayerTransform.position;
            toEnemy.y = 0;
            Vector3 forward = toEnemy.normalized;
            Vector3 right = Vector3.Cross(Vector3.up, forward);

            playerMovement = forward * moveInput.y + right * moveInput.x;
            playerMovement = PlayerTransform.InverseTransformVector(playerMovement);
        }
        else
        {
            // 正常模式：跟随摄像机方向
            Transform camTf = CameraManager.Instance.MainCameraTransform;
            Vector3 caneraForward = new Vector3(camTf.forward.x,0,camTf.forward.z).normalized;
            playerMovement = caneraForward * moveInput.y + camTf.right * moveInput.x;
            playerMovement = PlayerTransform.InverseTransformVector(playerMovement);
        }
    }

    void SetupAnimator()//动画状态更新
    {
        if (PlayerPosture == E_PlayerPosture.Stand)
        {
            //0.1f(dampTime)表示:从当前值 过渡到standThreshold 需要0.1f 使得动画过渡更加自然
            Animator.SetFloat(postrueHash, standThreshold, 0.1f, Time.deltaTime);

            switch (LocomotionState)//切换行动状态
            {
                case E_LocomotionState.Idle:
                    Animator.SetFloat(moveSpeedHash, 0f, 0.1f, Time.deltaTime);
                    break;
                case E_LocomotionState.Walk:
                    Animator.SetFloat(moveSpeedHash, playerMovement.magnitude * walkSpeed, 0.1f, Time.deltaTime);
                    break;
                case E_LocomotionState.Run:
                    Animator.SetFloat(moveSpeedHash, playerMovement.magnitude * runSpeed, 0.1f, Time.deltaTime);
                    break;
            }
        }
        else if (PlayerPosture == E_PlayerPosture.Crouch)
        {
            Animator.SetFloat(postrueHash, crouchThreshold, 0.1f, Time.deltaTime);

            switch (LocomotionState)
            {
                case E_LocomotionState.Idle:
                    Animator.SetFloat(moveSpeedHash, 0f, 0.1f, Time.deltaTime);
                    break;
                default:
                    Animator.SetFloat(moveSpeedHash, playerMovement.magnitude * crouchSpeed, 0.1f, Time.deltaTime);
                    break;
            }

        }
        else if (PlayerPosture == E_PlayerPosture.Jumping)
        {
            Animator.SetFloat(postrueHash, midAirThreshold, 0.1f, Time.deltaTime);
            Animator.SetFloat(jumpSpeedHash, VerticalVelocity, 0.1f, Time.deltaTime);
            Animator.SetFloat("FeetTween", feetTween);
        }
        else if (PlayerPosture == E_PlayerPosture.Landing)
        {
            Animator.SetFloat(postrueHash, LandingThreshold, 0.08f, Time.deltaTime);

            switch (LocomotionState)
            {
                case E_LocomotionState.Idle:
                    Animator.SetFloat(moveSpeedHash, 0f, 0.1f, Time.deltaTime);
                    break;
                case E_LocomotionState.Walk:
                    Animator.SetFloat(moveSpeedHash, playerMovement.magnitude * walkSpeed, 0.1f, Time.deltaTime);
                    break;
                case E_LocomotionState.Run:
                    Animator.SetFloat(moveSpeedHash, playerMovement.magnitude * runSpeed, 0.1f, Time.deltaTime);
                    break;
            }
        }
        else if (PlayerPosture == E_PlayerPosture.Falling)
        {
            Animator.SetFloat(postrueHash, midAirThreshold, 0.1f, Time.deltaTime);
            Animator.SetFloat(jumpSpeedHash, VerticalVelocity, 0.1f, Time.deltaTime);

        }

        if (ArmState == E_ArmState.Lock && lockedEnemy != null)
        {
            Vector3 dirToEnemy = lockedEnemy.transform.position - PlayerTransform.position;
            dirToEnemy.y = 0;
            if (dirToEnemy.sqrMagnitude > 0.01f)
            {
                Quaternion targetRot = Quaternion.LookRotation(dirToEnemy);
                PlayerTransform.rotation = Quaternion.Slerp(PlayerTransform.rotation, targetRot, lockRotateSpeed * Time.deltaTime);
            }
            // turnSpeed 设为移动方向角度，供 Blend Tree 混合 strafe 动画
            float rad = Mathf.Atan2(playerMovement.x, playerMovement.z);
            Animator.SetFloat(turnSpeedHash, rad, 0.1f, Time.deltaTime);
        }
        else if (ArmState == E_ArmState.Normal)
        {
            float rad = Mathf.Atan2(playerMovement.x, playerMovement.z);
            Animator.SetFloat(turnSpeedHash, rad, 0.1f, Time.deltaTime);
            if (!meleeFighter.inAction)
            {
                PlayerTransform.Rotate(0, rad * 200 * Time.deltaTime, 0f);
            }
        }
    }

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

    private void AnimatorMove()//动画驱动移动
    {
        if (PlayerPosture != E_PlayerPosture.Jumping && PlayerPosture != E_PlayerPosture.Falling)
        {
            if (isLocking)
            {
                // 索敌模式：禁用 root motion 水平移动，用代码控制 strafe 方向
                Vector3 worldMove = PlayerTransform.TransformVector(playerMovement);
                worldMove.y = 0;
                float speed = (isRunning ? runSpeed : walkSpeed);
                characterController.Move(worldMove * speed * Time.deltaTime);
                characterController.Move(Vector3.up * VerticalVelocity * Time.deltaTime);
            }
            else
            {
                Vector3 playerDelataMovement = Animator.deltaPosition;
                if (meleeFighter.inAction)
                {
                    playerDelataMovement.x = 0;
                    playerDelataMovement.y = 0;
                }
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

    private void OnAnimatorMove()
    {
        if (!meleeFighter.inCounter && !isLocking)
        {
            
        }

        if (!isLocking && !meleeFighter.inAction)
        {
            transform.rotation *= Animator.deltaRotation;
        }
    }

    public Vector3 GetTargetingDir()//获取目标方向
    {
        if (isLocking && lockedEnemy != null)
        {
            Vector3 dir = lockedEnemy.transform.position - transform.position;
            dir.y = 0;
            return dir.normalized;
        }  

        Transform lookAt = CameraManager.Instance.freeLook.m_LookAt;
        if (targetEnemy != null && lookAt != null)
        {
            Vector3 VecFromCam = lookAt.position - transform.position;
            VecFromCam.y = 0;
            return VecFromCam.normalized;
        }
        else
        {
            return transform.forward;
        }
    }

    public Vector3 GetAimDirection()
    {
        Transform camTf = CameraManager.Instance.MainCameraTransform;

        Ray ray = new Ray(camTf.position,camTf.forward);

        // 如果有锁定目标，指向目标
    if (isAiming && lockedEnemy != null)
    {
        return (lockedEnemy.transform.position - rangedFighter.transform.position).normalized;
    }
    
    return ray.direction;
    }
}

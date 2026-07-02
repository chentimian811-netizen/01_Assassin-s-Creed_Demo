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
    public  Transform PlayerTransform;
    
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




    
    bool isAiming = false;
   
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
    
    

    public float gravity = -9.8f;//重力

    

    //public float jumpedVelocity = 5f;//跳跃速度

    //最大的跳跃高度
    public float maxHeight = 1.5f;

    //下落加速度的倍数
    float fallMultiplier = 1.5f;

    //玩家是否着地
   

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

   

    public void GetBackpackInput(InputAction.CallbackContext context)
    {
        if(!context.performed) return;

        isMainMenuOpen = !isMainMenuOpen;
        if(isMainMenuOpen)
        {
            Time.timeScale = 0f;//打开背包时暂停游戏
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            UIManager.Instance.OpenPanel(UIconst.MainPanel);
        }
        else
        {
            Time.timeScale = 1f;//关闭背包时恢复游戏
            Cursor.lockState = CursorLockMode.Locked;
            UIManager.Instance.ClosePanel(UIconst.MainPanel);
        }
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

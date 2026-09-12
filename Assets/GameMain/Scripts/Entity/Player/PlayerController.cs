using System;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem;
using Cinemachine;
using UnityEngine.Rendering;

/// <summary>
/// 玩家控制器协调奇
/// 职责：持有子组件引用 管理acceptInput 协调Update顺序 转发输入
/// 所有枚举和状态集中定义 子组件通过引用访问
/// </summary>
public class PlayerController : MonoBehaviour
{

    [HideInInspector] public PlayerMovement playerMovement;
    [HideInInspector] public PlayerCombat playerCombat;
    [HideInInspector] public PlayerLockOn playerLockOn;
    [HideInInspector] public PlayerAnimator playerAnimator;
    [HideInInspector] public PlayerDodge playerDodge;       // 翻滚组件（P2）
    [HideInInspector] public PlayerStamina playerStamina;   // 耐力组件（P1）

    public Transform PlayerTransform { get; private set; }
    public Animator Animator { get; private set; }
    public CharacterController CharacterController { get; private set; }
    public MeleeFighter MeleeFighter { get; private set; }
    [Header("远程武器")]
    public RangedFighter RangedFighter;

    /// <summary>是否正在锁定敌人</summary>
    public bool IsLocking => playerLockOn.IsLocking;

    /// <summary>当前锁定的敌人</summary>
    public EnemyController LockedEnemy => playerLockOn.LockedEnemy;

    /// <summary>目标敌人（读写）</summary>
    public EnemyController TargetEnemy
    {
         get => playerLockOn.TargetEnemy; 
         set => playerLockOn.TargetEnemy = value;
    }
          
    /// <summary>强制解锁</summary>
    public void ForceUnlock() => playerLockOn.ForceUnlock();

    /// <summary>获取目标方向</summary>
    public Vector3 GetTargetingDir() => playerLockOn.GetTargetingDir();

    /// <summary>重置垂直速度（翻滚时调用）</summary>
    public void ResetVerticalVelocity() => playerMovement.ResetVerticalVelocity();

    /// <summary>
    /// 玩家姿态枚举
    /// </summary>
    public enum E_PlayerPosture
    {
        Crouch,//蹲下
        Falling,//下落
        Stand,//站立
        Jumping,//滞空
        Landing//着陆
    }
    public E_PlayerPosture PlayerPosture = E_PlayerPosture.Stand;

    /// <summary>
    /// 玩家行动状态枚举
    /// </summary>
    public enum E_LocomotionState
    {
        Idle,
        Walk,
        Run
    }
    public E_LocomotionState LocomotionState = E_LocomotionState.Idle;

    /// <summary>
    /// 玩家手臂状态枚举
    /// </summary>
    public enum E_ArmState
    {
        Normal,
        Aim,
        Lock,
    }
    public E_ArmState ArmState = E_ArmState.Normal;

    [HideInInspector] public bool acceptInput = true;
    bool  isMainMenuOpen;

    // Shift 双功能：短按翻滚 / 长按疾跑
    const float SprintHoldThreshold = 0.2f;
    float shiftPressedRealtime;
    bool shiftHeld;
    bool sprintEngaged;

    WeaponPickup nearestPickup;
    ShopNPC nearestShopNPC;
    public void SetNearestPickup(WeaponPickup pickup)
    {
        nearestPickup = pickup;
    }

    public void SetNearestShopNPC(ShopNPC npc)
    {
        nearestShopNPC = npc;
    }
    public void Awake()
    {
        MeleeFighter = GetComponent<MeleeFighter>();
        RangedFighter = GetComponent<RangedFighter>();
    }

    /// <summary>
    /// 获得组件
    /// </summary>
    void Start()
    {
        PlayerTransform = transform;
        Animator = GetComponent<Animator>();
        CharacterController = GetComponent<CharacterController>();

        
        playerMovement = GetComponent<PlayerMovement>();
        playerCombat = GetComponent<PlayerCombat>();
        playerLockOn = GetComponent<PlayerLockOn>();
        playerAnimator = GetComponent<PlayerAnimator>();
        playerDodge = GetComponent<PlayerDodge>();
        playerStamina = GetComponent<PlayerStamina>();
        if (playerStamina == null)
            Debug.LogWarning("[PlayerController] 玩家未挂 PlayerStamina，攻击将不扣耐力", this);
        if (playerDodge == null)
            Debug.LogWarning("[PlayerController] 玩家未挂 PlayerDodge，翻滚不可用", this);

        playerMovement.Init(CharacterController , Animator ,MeleeFighter , this);
        playerCombat.Init(MeleeFighter, playerLockOn, this, playerStamina);
        playerLockOn.Init(this);
        playerAnimator.Init(Animator, this, playerMovement);
        playerDodge?.Init(Animator, this, playerMovement, playerStamina,
            MeleeFighter != null ? MeleeFighter.HealthComponent : GetComponent<Health>(),
            CharacterController);

        
        Debug.Log("当前金币" + CurrencyManager.Instance.Gold);
    }
  
    void Update()
    {
        // Shift 长按 → 疾跑（在短按翻滚判定之前就咬合）
        if (shiftHeld && !sprintEngaged
            && Time.unscaledTime - shiftPressedRealtime >= SprintHoldThreshold)
        {
            sprintEngaged = true;
            playerMovement.SetRunning(true);
        }

        if ((MeleeFighter != null && MeleeFighter.HealthComponent != null && MeleeFighter.HealthComponent.IsDead)
            || !acceptInput)
        {
            ClearInput();
        }

        if (UIManager.Instance.panelDict.ContainsKey(UIconst.ShopPanel))
        {
            ClearInput();
            return;
        }

        //按顺序执行子组件逻辑
        playerLockOn.Tick();        //先检查锁定距离
        playerMovement.Tick();      //物理/重力/移动
        playerAnimator.Tick();      //同步动画参数
    }

    #region 输入相关
    /// <summary>
    /// 拾取/商店交互输入
    /// </summary>
    /// <param name="context"></param>
    public void GetPickup_ShopInput(InputAction.CallbackContext context)
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

    /// <summary>
    /// 拾取输入。Prefab 上 PlayerInput 事件绑的是这个方法名。
    /// </summary>
    public void GetPickupInput(InputAction.CallbackContext context)
    {
        GetPickup_ShopInput(context);
    }
    
    /// <summary>
    /// 移动输入
    /// </summary>
    /// <param name="context"></param>
    public void GetMoveInput(InputAction.CallbackContext context)
    {
        playerMovement.HandleMoveInput(context);
    }

    /// <summary>
    /// Shift 双功能：按下计时；超过阈值进入疾跑；松开时若未疾跑则翻滚。
    /// 不再走独立的 Dodge 按键，避免与 Run 抢同一个键。
    /// </summary>
    public void GetRunInput(InputAction.CallbackContext context)
    {
        if (context.started)
        {
            shiftHeld = true;
            shiftPressedRealtime = Time.unscaledTime;
            sprintEngaged = false;
            playerMovement.SetRunning(false);
            return;
        }

        if (context.canceled)
        {
            shiftHeld = false;
            bool wasSprint = sprintEngaged;
            sprintEngaged = false;
            playerMovement.SetRunning(false);

            // 短按 → 翻滚（长按疾跑松开不翻滚）
            if (!wasSprint && CursorManager.Instance != null && CursorManager.Instance.IsGameplayFocused)
            {
                playerDodge?.TryDodge();
            }
        }
    }
    
    /// <summary>
    /// 蹲下输入
    /// </summary>
    /// <param name="context"></param>
    public void GetCrouchInput(InputAction.CallbackContext context)
    {
        playerMovement.HandleCrouchInput(context);
    }
    
    /// <summary>
    /// 瞄准输入
    /// </summary>
    /// <param name="context"></param>
    public void GetAimInput(InputAction.CallbackContext context)
    {   
        if(!CursorManager.Instance.IsGameplayFocused) return;
        if(RangedFighter == null) return;
        if (context.performed)
        {
            RangedFighter.SetAiming(true);
        }
        else if (context.canceled)
        {
            RangedFighter.SetAiming(false);
        }
    }

    /// <summary>
    /// 射击输入
    /// </summary>
    /// <param name="context"></param>
    public void GetFireInput(InputAction.CallbackContext context)
    {
        if(!CursorManager.Instance.IsGameplayFocused) return;
        if(RangedFighter == null) return;
        if(!context.performed) return;

        //获取瞄准方向(从摄像机中心发射射线)
        Vector3 aimDirection  = playerLockOn.GetAimDirection();

        //尝试射击
        RangedFighter.TryFire(aimDirection);
    }

   /// <summary>
   /// 背包输入
   /// </summary>
   /// <param name="context"></param>
    public void GetBackpackInput(InputAction.CallbackContext context)
    {
        if(!context.performed) return;

        if (isMainMenuOpen)
        {
            CloseSubPanels();
        }

        isMainMenuOpen = !isMainMenuOpen;
        if(isMainMenuOpen)
        {
           
            CursorManager.Instance.AddLock("Backpack");
            UIManager.Instance.OpenPanel(UIconst.MainPanel);
        }
        else
        {
            
            CursorManager.Instance.RemoveLock("Backpack");
            UIManager.Instance.ClosePanel(UIconst.MainPanel);
        }
    }
    
    /// <summary>
    /// 轻攻击输入
    /// </summary>
    /// <param name="context"></param>
    public void GetLightAttack(InputAction.CallbackContext context)
    {
        if(!CursorManager.Instance.IsGameplayFocused) return;
        playerCombat.HandleLightAttack(context);
    }


    /// <summary>
    /// 锁定输入
    /// </summary>
    /// <param name="context"></param>
    public void GetLockInput(InputAction.CallbackContext context)
    {
        if(!CursorManager.Instance.IsGameplayFocused) return;
        playerLockOn.HandleLockInput(context);
    }

    /// <summary>
    /// 跳跃输入。Prefab 上 PlayerInput 事件绑的是这个方法名，转发给 PlayerMovement。
    /// </summary>
    public void GetJumpInInput(InputAction.CallbackContext context)
    {
        if (!CursorManager.Instance.IsGameplayFocused) return;
        playerMovement.HandleJumpInput(context);
    }

    /// <summary>
    /// 翻滚输入。Shift 双功能走 GetRunInput；此方法保留给调试/其他调用。
    /// </summary>
    public void GetDodgeInput(InputAction.CallbackContext context)
    {
        if (!context.performed) return;
        if (!CursorManager.Instance.IsGameplayFocused) return;
        playerDodge?.TryDodge();
    }

    /// <summary>
    /// 格挡/弹反输入（阶段3实现）。按下=开始格挡，松开=结束。
    /// </summary>
    public void GetBlockInput(InputAction.CallbackContext context)
    {
        // P3：parrySystem?.SetBlocking(context.performed);
        // 当前仅占位，保证 PlayerInput 事件能绑上且不抛异常
        _ = context;
    }

    public void GetShowCursorInput(InputAction.CallbackContext context)
    {

        Debug.Log($"GetShowCursorInput: {context.phase}"); 
        if(context.performed)
        {
            CursorManager.Instance.HoldCursor();
        }
        else if (context.canceled)
        {
            CursorManager.Instance.ReleaseCursor();
        }
    }

    /// <summary>
    /// 清空所有输入状态
    /// </summary>
    void ClearInput()
    {
        shiftHeld = false;
        sprintEngaged = false;
        playerMovement?.ClearInput();
    }
     #endregion
     
    private void CloseSubPanels()
    {
        string[] subPanels = { UIconst.LotteryPanel, UIconst.PackagePanel, UIconst.ShopPanel };
        foreach (var panelName in subPanels)
        {
            if (UIManager.Instance.GetPanel(panelName) != null)
                UIManager.Instance.ClosePanel(panelName);
        }

    }
}
 
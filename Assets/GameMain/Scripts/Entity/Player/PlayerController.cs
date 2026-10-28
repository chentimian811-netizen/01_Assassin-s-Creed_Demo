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
    public void ForceUnlock() => playerLockOn.ForceUnlock();    /// <summary>获取目标方向</summary>
    public Vector3 GetTargetingDir() => playerLockOn.GetTargetingDir();

    /// <summary>重置垂直速度（翻滚时调用）</summary>
    public void ResetVerticalVelocity() => playerMovement.ResetVerticalVelocity();

    /// <summary>
    /// 复位到「新开一局」的状态：满血、回出生点、清锁定与输入残留。
    ///
    /// 【为什么需要】
    ///   单场景阶段「返回主菜单 → 再开始」不重载场景 —— GF 的 LoadScene 是 additive 加载，
    ///   对当前场景再加载一次会多出一整套框架/玩家/敌人实例。所以复位只能在代码里做。
    ///
    /// 【不负责】
    ///   敌人血量/位置、掉落物、篝火状态。真正的整关重置等 GF-4 拆出关卡场景后由重载完成。
    /// </summary>
    public void ResetForNewRun()
    {
        // Start 还没跑（刚加载完场景）时组件引用都是空的，这时也不需要复位
        if (playerMovement == null || PlayerTransform == null) return;

        // 1. 复活：isDead 只有 Health 能清，绕过它会出现「满血但打不了」的僵尸态
        Health health = GetComponent<Health>();
        if (health != null) health.ResetToFull();

        // 2. 回出生点：CharacterController 开着时直接改 transform 会被它顶回去
        if (m_SpawnCaptured)
        {
            bool controllerWasEnabled = CharacterController != null && CharacterController.enabled;
            if (controllerWasEnabled) CharacterController.enabled = false;
            PlayerTransform.SetPositionAndRotation(m_SpawnPosition, m_SpawnRotation);
            if (controllerWasEnabled) CharacterController.enabled = true;
        }

        // 3. 清锁定 / 瞄准 / 输入残留，避免带着死亡瞬间的状态进新一局
        ForceUnlock();
        CameraManager.Instance?.ClearLockTarget();
        CancelAim();
        ClearInput();
        playerMovement.ResetVerticalVelocity();
    }

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

    /// <summary>出生点（Start 时记录），供「返回主菜单 → 再开始」复位用</summary>
    private Vector3 m_SpawnPosition;
    private Quaternion m_SpawnRotation;
    private bool m_SpawnCaptured;

    // Shift 双功能：短按翻滚 / 长按疾跑。
    // 阈值来自 Player 表的 run_hold_threshold（Awake 解析一次，运行中不查表）
    private Player m_configRow;
    private float SprintHoldThreshold => m_configRow != null ? m_configRow.RunHoldThreshold : 0f;
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
        m_configRow = CharacterConfigIdentity.GetPlayerRow(this);
    }

    /// <summary>
    /// 获得组件
    /// </summary>
    void Start()
    {
        PlayerTransform = transform;
        Animator = GetComponent<Animator>();
        CharacterController = GetComponent<CharacterController>();

        // 记录出生点：单场景阶段「返回主菜单 → 再开始」不重载场景，只能把玩家拉回这里
        m_SpawnPosition = transform.position;
        m_SpawnRotation = transform.rotation;
        m_SpawnCaptured = true;

        
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

        // 失去 gameplay 焦点（Alt 松光标 / 背包 / 商店）时，右键的 canceled 回调会被
        // GetAimInput 开头的焦点判断挡掉，这里幂等取消，避免 UI 关掉后仍挂着右肩机位。
        if (!CursorManager.Instance.IsGameplayFocused)
            CancelAim();

        // 商店这类模态界面打开时冻结角色输入（HUD 仍然显示）
        if (UIRouter.IsOpen(UIPaths.ShopForm))
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
    /// 瞄准输入。
    /// 射击状态与相机机位必须同一事务切换，否则会出现「有准星没有右肩机位」或反过来的假瞄准。
    /// </summary>
    /// <param name="context"></param>
    public void GetAimInput(InputAction.CallbackContext context)
    {   
        if(!CursorManager.Instance.IsGameplayFocused) return;
        if(RangedFighter == null) return;
        if (context.performed)
        {
            // 参考行为：进入瞄准会退出近战锁定
            playerLockOn.ForceUnlock();
            ArmState = E_ArmState.Aim;
            RangedFighter.SetAiming(true);
            if (CameraManager.Instance != null)
                CameraManager.Instance.SetAiming(true);
        }
        else if (context.canceled)
        {
            RangedFighter.SetAiming(false);
            ArmState = playerLockOn.IsLocking
                ? E_ArmState.Lock
                : E_ArmState.Normal;
            if (CameraManager.Instance != null)
                CameraManager.Instance.SetAiming(false);
        }
    }

    /// <summary>
    /// 幂等取消瞄准。
    /// 失去 gameplay 焦点时 GetAimInput 开头的焦点判断会把 canceled 挡掉，
    /// 不补这一手会在 UI 后面留下 isAiming = true 与右肩机位。
    /// </summary>
    private void CancelAim()
    {
        if (RangedFighter == null || !RangedFighter.IsAiming)
            return;

        RangedFighter.SetAiming(false);
        ArmState = playerLockOn.IsLocking
            ? E_ArmState.Lock
            : E_ArmState.Normal;
        if (CameraManager.Instance != null)
            CameraManager.Instance.SetAiming(false);
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
   /// 背包输入（反引号）。GF 接管 UI 后直接开关 PackageForm：
   /// 暂停与光标由 PackageForm 继承的 GameUIForm 统一处理，这里不再手写 AddLock。
   /// </summary>
   /// <param name="context"></param>
    public void GetBackpackInput(InputAction.CallbackContext context)
    {
        if(!context.performed) return;

        // 已经开着 → 关掉（反引号即开关，与旧版行为一致）
        if (UIRouter.IsOpen(UIPaths.PackageForm))
        {
            UIRouter.Close(UIPaths.PackageForm);
            return;
        }

        // 打开背包前先关掉其它弹窗，避免两个模态叠在一起
        CloseSubPanels();

        UIRouter.Open(UIPaths.PackageForm, "Popup");
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

        // 瞄准中忽略锁定键：否则 LockEnemy 会把相机切回普通锁定、并把 ArmState 改成 Lock，
        // 而 RangedFighter 仍处于瞄准 → 出现「角色还在瞄、相机已经不在右肩」的假状态。
        // 想改成「按 Q 先取消瞄准、再进入锁定」，把下面这行替换成 CancelAim(); 后继续往下走。
        if (ArmState == E_ArmState.Aim) return;

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
    /// 暂停/恢复输入（Esc）。绑定在 PlayerActions 的 Pause action 上。
    ///
    /// 【为什么不走 CursorManager.IsGameplayFocused 闸门】暂停时鼠标锁已经解除，
    /// 若沿用「只有游戏聚焦才响应」，按 Esc 就永远恢复不了。
    /// 【为什么不在这里直接开关界面】界面与流程的开关只经 ProcedureGame 一处；
    /// 输入层只发菜单命令（GF Event），不持有 UI 状态。
    /// </summary>
    public void GetPauseInput(InputAction.CallbackContext context)
    {
        if (!context.performed) return;

        // 已经暂停 → Esc 恢复。真正解除暂停在 PauseForm.OnClose（UIPauseService 计数归零）
        if (UIRouter.IsOpen(UIPaths.PauseForm))
        {
            GameEntry.Event.Fire(this, MenuCommandEventArgs.Create(ProcedureGame.CommandResume));
            return;
        }

        // 其它模态（背包/商店/死亡/抽卡）开着时不抢 Esc：
        // 这些界面有自己的关闭路径，不要再叠一层暂停上去。
        if (UIPauseService.IsPausedByUI) return;

        GameEntry.Event.Fire(this, MenuCommandEventArgs.Create(ProcedureGame.CommandPause));
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
     
    /// <summary>
    /// 关闭所有弹窗。Package / Lottery / Shop 都已迁到 GF，这里只剩 UIRouter。
    /// </summary>
    private void CloseSubPanels()
    {
        UIRouter.Close(UIPaths.PackageForm);
        UIRouter.Close(UIPaths.LotteryForm);
        UIRouter.Close(UIPaths.ShopForm);
    }
}
 
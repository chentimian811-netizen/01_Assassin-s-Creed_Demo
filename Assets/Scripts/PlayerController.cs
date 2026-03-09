using System;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviour
{
    Transform PlayerTransform;
    Animator Animator;
    Transform cameraTransform;
    CharacterController characterController;

    public enum E_PlayerPostrue//玩家姿态
    {
        Crouch,//蹲下
        Stand,//站立
        MidAir,//滞空
        Landing//着陆
    }
    public E_PlayerPostrue PlayerPostrue = E_PlayerPostrue.Stand;//规定玩家的初始姿态

    float crouchThreshold = 0f;//姿态阈值设定
    float standThreshold = 1f;
    float midAirThreshold = 2.2f; //出现抖动可以调高阈值
    float LandingThreshold = 1f;
     
    public enum E_LocomotionState//玩家行动状态
    {
        Idle,
        Walk,
        Run
    }
    public E_LocomotionState LocomotionState = E_LocomotionState.Idle;//规定玩家的初始动作

    public enum E_ArmState//玩家瞄准状态
    {
        Norml,
        Aim,
    }
    public E_ArmState ArmState = E_ArmState.Norml;//初始攻击

   public float crouchSpeed = 1.5f;
   public float walkSpeed = 3f;
   public float runSpeed = 6f;

    Vector2 moveInput;//用二维值 存贮玩家输入的前后左右的值

    bool isRunning;
    bool isCrouch;
    bool isAiming;
    bool isJumping;

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

    static readonly int CACHE_SIZE = 3;

    Vector3[] velCache = new Vector3[CACHE_SIZE];

    int currentChaCheIndex = 0;

    Vector3 averageVel = Vector3 .zero;

    //下落加速度的倍数
    float fallMultiplier = 1.5f;

    //玩家是否着地
    bool isGround;

    //是否处于CD中
    bool isLanding;

    //地面检测射线的偏移量
    float groundCheckOffset = 0.5f;

    //滞空左右脚状态
    float feetTween;

    //跳跃CD
    float jumpCD = 0.15f;

    // Start is called before the first frame update
    void Start()
    {
        PlayerTransform = transform;
        Animator = GetComponent<Animator>();
        cameraTransform = Camera.main.transform;
        characterController = GetComponent<CharacterController>();

        postrueHash = Animator.StringToHash("PlayerState");//用哈希值存贮 资源占用更少
        moveSpeedHash = Animator.StringToHash("MoveSpeed");
        turnSpeedHash = Animator.StringToHash("TurnSpeed");
        jumpSpeedHash = Animator.StringToHash("JumpSpeed");
        feetTweensHash = Animator.StringToHash("FeetTween");

        Cursor.lockState = CursorLockMode.Locked;//隐藏玩家鼠标
    }
    // Update is called once per frame
    void Update()
    {
        CheckGround();
        SwitchPlayerState();
        CaculateGravity();
        Jump(); 
        CaculateInputDirection();
        SetupAnimator();
        AnimatorMove();
    }
    
    #region 输入相关
    public void GetMoveInput(InputAction.CallbackContext context)
    {
        moveInput = context.ReadValue<Vector2>();
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
        isAiming = context.ReadValueAsButton();
    }

    public void GetJumpInInput(InputAction.CallbackContext context)
    {
        isJumping = context.ReadValueAsButton();
    }
    #endregion

    void CheckGround()
    {
        if(Physics.SphereCast(PlayerTransform.position + (Vector3.up * groundCheckOffset), characterController.radius,Vector3.down,out RaycastHit hit,groundCheckOffset - characterController.radius + 2 * characterController.skinWidth))
        {
            isGround = true;
        }
        else
        {
            isGround = false;
        }
    }

    void SwitchPlayerState()
    {
        //如果不在地面则切换成滞空状态
        if(!isGround)
        {
            PlayerPostrue = E_PlayerPostrue.MidAir;
        }
        else if (PlayerPostrue == E_PlayerPostrue.MidAir)
        {
            StartCoroutine(CoolDownJump()); 
        }
        else if (isLanding)
        {
            PlayerPostrue = E_PlayerPostrue.Landing;
        }
       else if (isCrouch)
        {
            PlayerPostrue = E_PlayerPostrue.Crouch;
        }
        else
        {
            PlayerPostrue = E_PlayerPostrue.Stand;
        }


        if (moveInput.magnitude ==0)
        {
            LocomotionState = E_LocomotionState.Idle;
        }
        else if (isRunning)
        {
            LocomotionState = E_LocomotionState.Walk;
        }
        else
        {
            LocomotionState = E_LocomotionState.Run;
        }

        if (isAiming)
        {
            ArmState = E_ArmState.Aim;
        }
        else
        {
            ArmState = E_ArmState.Norml;
        }
    }

    IEnumerator CoolDownJump()
    {
        LandingThreshold = Mathf.Clamp(VerticalVelocity, -10, 0);
        LandingThreshold /= 20f;
        LandingThreshold += 1f;
        isLanding = true;
        PlayerPostrue = E_PlayerPostrue.Landing;
        yield return new WaitForSeconds(jumpCD);
        isLanding  = false;
    }

    //重力
    void CaculateGravity()
    {
        if(PlayerPostrue != E_PlayerPostrue.MidAir)
        {
            //当在地面上时 给予一个向下的力 使得贴地面
            VerticalVelocity = gravity * Time.deltaTime;
            return;  
        }
        else
        {
            if(VerticalVelocity <= 0)
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
   
    void Jump()
    {
        //当角色在地面并且 按下跳跃 则获得一个瞬时向上的力
        if (PlayerPostrue == E_PlayerPostrue.Stand && isJumping)
        {
            VerticalVelocity = MathF.Sqrt(-2 * gravity * maxHeight);
            feetTween = Mathf.Repeat(Animator.GetCurrentAnimatorStateInfo(0).normalizedTime, 1);
            feetTween = feetTween < 0.5 ? 1 : -1;
            if(LocomotionState == E_LocomotionState.Run)
            {
                feetTween *= 3;
            }
            else if(LocomotionState == E_LocomotionState.Walk)
            {
                feetTween *= 2;
            }
            else
            {
                feetTween = UnityEngine.Random.Range(0.5f,1f) * feetTween;
            }
        }
    }


    void CaculateInputDirection()//输入方向的计算
    {
       Vector3 cameraForward = new Vector3(cameraTransform.forward.x,0, cameraTransform.forward.z).normalized;
       playerMovement = cameraForward * moveInput.y + cameraTransform.right * moveInput.x;//获得玩家输入值
       playerMovement = PlayerTransform.InverseTransformVector(playerMovement);//将世界坐标转换为玩家的当前坐标
    }

    void SetupAnimator()//动画状态更新
    {
        if(PlayerPostrue == E_PlayerPostrue.Stand)
        {
            //0.1f(dampTime)表示:从当前值过渡到standThreshold需要0.1f,使得动画过渡更加自然
            Animator.SetFloat(postrueHash, standThreshold,0.1f,Time.deltaTime);

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
        else if (PlayerPostrue == E_PlayerPostrue.Crouch)
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
        else if(PlayerPostrue == E_PlayerPostrue.MidAir)
        {
            Animator.SetFloat(postrueHash,midAirThreshold, 0.1f, Time.deltaTime);
            Animator.SetFloat(jumpSpeedHash,VerticalVelocity,0.1f,Time.deltaTime);
            Animator.SetFloat("FeetTween", feetTween);
        }
        else if(PlayerPostrue == E_PlayerPostrue.Landing)
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
        

        if (ArmState == E_ArmState.Norml)
        {
            float rad = Mathf.Atan2(playerMovement.x, playerMovement.z);
            Animator.SetFloat(turnSpeedHash, rad, 0.1f, Time.deltaTime);
            PlayerTransform.Rotate(0, rad * 200 * Time.deltaTime, 0f);
        }
    }

    Vector3 AverageVel(Vector3 newVel)
    {
        velCache[currentChaCheIndex] = newVel;
        currentChaCheIndex++;
        currentChaCheIndex %= CACHE_SIZE;
        Vector3 average = Vector3.zero ;
        foreach (Vector3 vel in velCache)
        {
            average += vel;
        }
        return average / CACHE_SIZE;

    }

    private void AnimatorMove()//动画驱动移动
    {
        if(PlayerPostrue != E_PlayerPostrue.MidAir)
        {
            Vector3 playerDelataMovement = Animator.deltaPosition;
            playerDelataMovement.y = VerticalVelocity * Time.deltaTime;//叠加垂直移动 实现跳跃
            characterController.Move(playerDelataMovement);//实现玩家移动
            averageVel = AverageVel(Animator.velocity);
        }
        else
        {
            averageVel.y = VerticalVelocity;
            Vector3 playerDelataMovement = averageVel * Time.deltaTime;
            characterController.Move(playerDelataMovement); 
        }
       
    }
}

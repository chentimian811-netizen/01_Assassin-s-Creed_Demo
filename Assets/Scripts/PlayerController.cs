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
    public enum E_PlayerPostrue
    {
        Crouch,
        Stand,
        MidAir
    }
    public E_PlayerPostrue PlayerPostrue = E_PlayerPostrue.Stand;
    float crouchThreshold = 0f;
    float standThreshold = 1f;
    float midAirThreshold = 2f; 

    public enum E_LocomotionState
    {
        Idle,
        Walk,
        Run
    }
    public E_LocomotionState LocomotionState = E_LocomotionState.Idle;

    public enum E_ArmState
    {
        Norml,
        Aim,
    }
    public E_ArmState ArmState = E_ArmState.Norml;

   public float crouchSpeed = 1.5f;
   public float walkSpeed = 3f;
   public float runSpeed = 6f;

    Vector2 moveInput;
    bool isRunning;
    bool isCrouch;
    bool isAiming;
    bool isJumping;

    int postrueHash;
    int moveSpeedHash;
    int turnSpeedHash;
    int jumpSpeedHash;

    Vector3 playerMovement = Vector3.zero;


    public float gravity = -9.8f;

    float VerticalVelocity;

    public float jumpedVelocity = 5f;
    // Start is called before the first frame update
    void Start()
    {
        PlayerTransform = transform;
        Animator = GetComponent<Animator>();
        cameraTransform = Camera.main.transform;
        characterController = GetComponent<CharacterController>();

        postrueHash = Animator.StringToHash("PlayerState");
        moveSpeedHash = Animator.StringToHash("MoveSpeed");
        turnSpeedHash = Animator.StringToHash("TurnSpeed");
        jumpSpeedHash = Animator.StringToHash("JumpSpeed"); 

        Cursor.lockState = CursorLockMode.Locked;
    }
    // Update is called once per frame
    void Update()
    {
        CaculateGravity();
        Jump();
        CaculateInputDirection();
        SetupAnimator();
        SwitchPlayerState();
        AnimatorMove();
    }
    
    #region  ‰»Îœ‡πÿ
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

    void SwitchPlayerState()
    {
        if(!characterController.isGrounded)
        {
            PlayerPostrue = E_PlayerPostrue.MidAir;
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

    void CaculateGravity()
    {
        if(characterController.isGrounded)
        {
            VerticalVelocity = gravity * Time.deltaTime;
            return;  
        }
        else
        {
            VerticalVelocity += gravity * Time.deltaTime;
        }
    }
   
    void Jump()
    {
        if (characterController.isGrounded && isJumping)
        { 
            VerticalVelocity = jumpedVelocity;
        }
    }


    void CaculateInputDirection()
    {
       Vector3 cameraForward = new Vector3(cameraTransform.forward.x,0, cameraTransform.forward.z).normalized;
       playerMovement = cameraForward * moveInput.y + cameraTransform.right * moveInput.x;
       playerMovement = PlayerTransform.InverseTransformVector(playerMovement);
    }

    void SetupAnimator()
    {
        if(PlayerPostrue == E_PlayerPostrue.Stand)
        {
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
        }

        if (ArmState == E_ArmState.Norml)
        {
            float rad = Mathf.Atan2(playerMovement.x, playerMovement.z);
            Animator.SetFloat(turnSpeedHash, rad, 0.1f, Time.deltaTime);
            PlayerTransform.Rotate(0, rad * 200 * Time.deltaTime, 0f);
        }
    }
    private void AnimatorMove()
    {
        Vector3 playerDelataMovement = Animator.deltaPosition;  
        playerDelataMovement.y = VerticalVelocity * Time.deltaTime;
        characterController.Move(playerDelataMovement);
    }
}

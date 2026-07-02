using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;


/// <summary>
/// 玩家锁定系统组件
/// 职责：锁定/解锁敌人、锁定旋转、距离检查、目标方向计算
/// </summary>
public class PlayerLockOn : MonoBehaviour
{
    PlayerController playerController;

    //锁定参数
    float lockRotateSpeed = 8f;     //锁定时转向敌人的速度
    float lockDistanc = 10f;        //锁定最大距离 超出范围自动解锁

    //是否正在锁定敌人
    public bool IsLocking {get;private set;}

    //当前锁定敌人
    public EnemyController LockedEnemy {get;private set;}

    //目标敌人
    public EnemyController TargetEnemy {get;set;}

    public void Init(PlayerController pc)
    {
        playerController = pc;
    }

    /// <summary>
    /// 每帧调用
    /// 检查锁定距离 超出范围自动解锁
    /// </summary>
    public void Tick()
    {
        if(IsLocking && LockedEnemy != null)
        {
            float dist = Vector3.Distance(transform.position,LockedEnemy.transform.position);
            if(dist > lockDistanc)
            {
                UnlockEnemy();
            }
        }
    }

    public void HandleLockInput(InputAction.CallbackContext context)
    {
        if (!context.performed) return;

        if (IsLocking)
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

    //锁定指定敌人
    void LockEnemy(EnemyController enemy)
    {
        IsLocking = true;
        LockedEnemy = enemy;
        TargetEnemy = enemy;
        playerController.ArmState = PlayerController.E_ArmState.Lock;

        // 禁用自由相机的X轴旋转，由锁定系统控制
        CameraManager.Instance.LockFreeLookXAxis();

        // 高亮敌人网格
        enemy.MeshHightlighter?.HighlightMesh(true);
    }

    //解锁当前敌人
    void UnlockEnemy()
    {
        IsLocking = false;

        if(LockedEnemy != null)
        {
            LockedEnemy.MeshHightlighter?.HighlightMesh(false);
        }

        //恢复自由相机控制
        CameraManager.Instance.UnlockFreeLookAxes();

        LockedEnemy = null;
        TargetEnemy = null;
        playerController.ArmState = PlayerController.E_ArmState.Normal;
    }

    //强制解锁 
    public void ForceUnlock()
    {
        if (IsLocking)
        {
            UnlockEnemy();
        }
    }

    //获得目标方向
    public Vector3 GetTargetingDir()
    {
        //锁定敌人 朝向锁定敌人
        if(IsLocking && LockedEnemy != null)
        {
            Vector3 dir = LockedEnemy.transform.position - transform.position;
            dir.y = 0;
            return dir.normalized;
        }


        //非锁定模式 朝向摄像机LookAt目标
        Transform lookAt = CameraManager.Instance.freeLook.m_LookAt;
        if(TargetEnemy != null && lookAt != null)
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

    public Vector3 GetAimDir()
    {
        Transform camTf = CameraManager.Instance.MainCameraTransform;
        Ray ray = new Ray(camTf.position,camTf.forward);

        //如果有锁定目标 指向目标
        if(playerController.ArmState == PlayerController.E_ArmState.Aim && LockedEnemy != null)
        {
            RangedFighter rf = GetComponent<RangedFighter>();
            if(rf != null)
            {
                return (LockedEnemy.transform.position - transform.position).normalized;
            }
        }
        return ray.direction;
    }
    
}

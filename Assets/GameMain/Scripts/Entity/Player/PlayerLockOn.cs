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
    // 【数值来源】Player 表（Gameplay.xlsx），Init 时解析一次。
    //   原 lockRotateSpeed 字段在工程里没有任何消费方（真正生效的是 PlayerAnimator 的同名字段），已删除。
    private Player m_configRow;

    /// <summary>锁定最大距离（米），超出即自动解锁</summary>
    float lockDistance => m_configRow != null ? m_configRow.LockOnDistance : 0f;

    //是否正在锁定敌人
    public bool IsLocking {get;private set;}

    //当前锁定敌人
    public EnemyController LockedEnemy {get;private set;}

    //目标敌人
    public EnemyController TargetEnemy {get;set;}

    public void Init(PlayerController pc)
    {
        playerController = pc;
        m_configRow = CharacterConfigIdentity.GetPlayerRow(this);
    }

    /// <summary>
    /// 每帧调用
    /// 检查锁定距离 超出范围自动解锁
    /// </summary>
    public void Tick()
    {
        // 敌人被销毁后 LockedEnemy 会按 Unity 假 null 规则变成 null，下面的距离检查会整体跳过。
        // 这里补一条兜底：还在锁定态但目标已失效时走正式解锁，避免相机一直停在旧目标上。
        if (IsLocking && LockedEnemy == null)
        {
            UnlockEnemy();
            return;
        }

        if(IsLocking && LockedEnemy != null)
        {
            float dist = Vector3.Distance(transform.position,LockedEnemy.transform.position);
            if(dist > lockDistance)
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
            else
            {
                // 复刻参考行为：按锁定键但附近没有目标时，只把镜头转回角色背后（Pitch 不变）
                CameraManager.Instance.ResetFreeLookCamera();
            }
        }
    }

    /// <summary>
    /// 锁定指定敌人。
    /// 设为 public 是为了让 EnemyManager 的自动换目标也走同一套正式入口，
    /// 否则会出现「角色/UI 看新敌人、相机仍看旧敌人」。
    /// </summary>
    public void LockEnemy(EnemyController enemy)
    {
        IsLocking = true;
        LockedEnemy = enemy;
        TargetEnemy = enemy;
        playerController.ArmState = PlayerController.E_ArmState.Lock;

        // 机位对准敌人胸口观察点（CameraAimPoint），由相机控制器逐帧追踪
        CameraManager.Instance.SetLockTarget(enemy.CameraAimPoint);

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
        CameraManager.Instance.ClearLockTarget();

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


        //非锁定模式 朝向摄像机跟随锚点
        //（新机位下 FollowTarget 是玩家身上的 CameraWatchPoint；旧 FreeLook 时退回它的 LookAt）
        Transform lookAt = CameraManager.Instance.FollowTarget;
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

    public Vector3 GetAimDirection()
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

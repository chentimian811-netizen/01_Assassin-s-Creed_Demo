using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 玩家战斗输入组件
/// 职责：轻攻击输入处理 反击判断；P1 起负责攻击耐力消耗
/// </summary>
public class PlayerCombat : MonoBehaviour
{
    MeleeFighter meleeFighter;
    PlayerController playerController;
    WeaponSwitcher switcher;
    WeaponManager weapManager;
    PlayerStamina stamina;
    StaminaConfig staminaConfig;

    public void Init(MeleeFighter mf,PlayerLockOn lo,PlayerController pc, PlayerStamina staminaRef = null)
    {
        meleeFighter = mf;
        playerController = pc;
        switcher = GetComponent<WeaponSwitcher>();
        weapManager = GetComponent<WeaponManager>();
        stamina = staminaRef != null ? staminaRef : GetComponent<PlayerStamina>();
        // 从耐力组件取配置，避免战斗侧再挂一份 SO 引用
        if (stamina != null)
        {
            staminaConfig = stamina.Config;
        }
    }

    public void HandleLightAttack(InputAction.CallbackContext context)
    {
        if (!context.performed) return;
        
        //如果正在使用远程武器 不触发近战攻击
        if(switcher != null && switcher.IsUsingRanged) return;

        if(weapManager == null || !weapManager.HasMeeleWeaponEquipped()) return;

        //获取锁定目标
        MeleeFighter targetFighter = null;
        if(playerController.IsLocking && playerController.LockedEnemy != null)
        {
            targetFighter = playerController.LockedEnemy.Fighter;
        }

        //攻击前平滑转向摄像机方向
        if (!playerController.IsLocking)
        {
            Transform camTf = CameraManager.Instance.MainCameraTransform;
            Vector3 camForward = new Vector3(camTf.forward.x,0,camTf.forward.z).normalized;
            if(camForward.sqrMagnitude > 0.01f)
            {
                Quaternion targetRot = Quaternion.LookRotation(camForward);
                //用一个较大的Larp 系数实现"快速但不瞬间"的转向
                playerController.PlayerTransform.rotation = Quaternion.Slerp(playerController.PlayerTransform.rotation,targetRot,10f * Time.deltaTime);
            }
        }

        //检查是否可以反击（目标必须活着，否则会把 Death 姿态切成 Melee_CounterVictim，看着像复活）
        var enemy = EnemyManager.i.GetAttackingEnemy();

        if (enemy != null
            && (enemy.Health == null || !enemy.Health.IsDead)
            && enemy.Fighter.IsCounterable
            && !meleeFighter.inAction && !meleeFighter.IsAttackingHit)
        {
            // 反击是窗口期惩罚，不扣耐力（魂类惯例：处决/反击免费）
            StartCoroutine(meleeFighter.PerformCounterAttack(enemy));
        }
        else
        {
            // 轻攻击消耗：不足则整段攻击不发出
            float cost = staminaConfig != null ? staminaConfig.lightAttackCost : 12f;
            if (stamina != null && !stamina.TryConsume(cost)) return;

            meleeFighter.ToTryAttack(targetFighter ?? playerController.TargetEnemy?.Fighter);
        }
    }
}

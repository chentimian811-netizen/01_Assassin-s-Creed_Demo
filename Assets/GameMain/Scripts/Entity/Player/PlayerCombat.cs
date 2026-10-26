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

    [Header("处决/反击")]
    [Tooltip("关闭后轻攻击不再触发处决演出，一律走普通挥砍（照常扣耐力）。演出逻辑仍保留在 MeleeFighter.PerformCounterAttack，勾回来即可恢复")]
    [SerializeField] private bool enableCounterAttack = false;

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

        // 死亡后不再接受战斗输入。
        // 原因见 MeleeFighter.ToTryAttack：死亡动作在 Override Layer 上是一次性的，
        // 被攻击动画顶掉之后角色会"站起来"但移动逻辑仍然是死的（不能动、也不能再被打死）
        if (meleeFighter != null && meleeFighter.HealthComponent != null
            && meleeFighter.HealthComponent.IsDead) return;

        //如果正在使用远程武器 不触发近战攻击
        if(switcher != null && switcher.IsUsingRanged) return;

        if (weapManager == null)
            weapManager = GetComponent<WeaponManager>();
        if (weapManager == null || !weapManager.HasMeeleWeaponEquipped())
        {
            Debug.Log("[PlayerCombat] 攻击被拦：未装备近战武器（盾/弓不算）");
            return;
        }

        //获取锁定目标
        MeleeFighter targetFighter = null;
        if(playerController.IsLocking && playerController.LockedEnemy != null)
        {
            targetFighter = playerController.LockedEnemy.Fighter;
        }

        //攻击前平滑转向摄像机方向
        if (!playerController.IsLocking && CameraManager.Instance != null)
        {
            Transform camTf = CameraManager.Instance.MainCameraTransform;
            if (camTf != null)
            {
                Vector3 camForward = new Vector3(camTf.forward.x,0,camTf.forward.z).normalized;
                if(camForward.sqrMagnitude > 0.01f)
                {
                    Quaternion targetRot = Quaternion.LookRotation(camForward);
                    playerController.PlayerTransform.rotation = Quaternion.Slerp(playerController.PlayerTransform.rotation,targetRot,10f * Time.deltaTime);
                }
            }
        }

        //检查是否可以反击（目标必须活着，否则会把 Death 姿态切成 Melee_CounterVictim，看着像复活）
        // 处决开关：关掉后不查询「正在出手的敌人」，整段反击分支自然失效，
        // 轻攻击一律走 else 里的普通挥砍（照常扣耐力、照常转向）
        var enemy = enableCounterAttack && EnemyManager.i != null
            ? EnemyManager.i.GetAttackingEnemy()
            : null;

        if (enemy != null
            && (enemy.Health == null || !enemy.Health.IsDead)
            && enemy.Fighter != null
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
            if (stamina != null && !stamina.TryConsume(cost))
            {
                Debug.Log("[PlayerCombat] 攻击被拦：耐力不足");
                return;
            }

            meleeFighter.ToTryAttack(targetFighter ?? playerController.TargetEnemy?.Fighter);
        }
    }
}

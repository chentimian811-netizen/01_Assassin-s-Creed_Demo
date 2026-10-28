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

    [Header("处决/反击")]
    [Tooltip("处决总开关。关闭后轻攻击一律走普通挥砍（照常扣耐力）。演出逻辑保留在 MeleeFighter.PerformCounterAttack")]
    [SerializeField] private bool enableCounterAttack = true;

    [Tooltip("【旧路径，默认关】不必弹反：敌人正在起手时按左键也能触发处决（AC 风格反击）。\n" +
             "开着会变成\"无脑按左键就处决\"，所以和\"弹反后处决\"拆成两个开关")]
    [SerializeField] private bool counterOnEnemyWindup = false;

    [Tooltip("处决的最大触发距离（米）。超出就只打普通攻击，避免隔空瞬移过去演出")]
    [SerializeField] private float executionRange = 3f;

    public void Init(MeleeFighter mf,PlayerLockOn lo,PlayerController pc, PlayerStamina staminaRef = null)
    {
        meleeFighter = mf;
        playerController = pc;
        switcher = GetComponent<WeaponSwitcher>();
        weapManager = GetComponent<WeaponManager>();
        stamina = staminaRef != null ? staminaRef : GetComponent<PlayerStamina>();
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

        // 处决判定（T5）。两条路径，默认只开第一条：
        //   ① 弹反奖励：刚弹反成功的敌人 + Parry.riposte_window 内、贴身按左键 → 处决；
        //   ② 旧路径（默认关）：敌人正在起手时按左键 → 处决（不需要弹反）。
        var enemy = ResolveExecutionTarget();

        if (enemy != null && !meleeFighter.inAction && !meleeFighter.IsAttackingHit)
        {
            // 处决是窗口期惩罚，不扣耐力（魂类惯例：处决/反击免费）
            if (playerController != null && playerController.parrySystem != null)
                playerController.parrySystem.ClearRiposteTarget();   // 窗口用完，不能连点两次

            StartCoroutine(meleeFighter.PerformCounterAttack(enemy));
        }
        else
        {
            // 轻攻击消耗：不足则整段攻击不发出。
            // 消耗值来自 Stamina 表（与翻滚同源）；缺配置时直接拦下，不按默认值放行
            if (stamina == null || stamina.Row == null)
            {
                Debug.LogError("[PlayerCombat] 缺少 Stamina 配置行，轻攻击被拒绝");
                return;
            }
            if (!stamina.TryConsume(stamina.Row.LightAttackCost))
            {
                Debug.Log("[PlayerCombat] 攻击被拦：耐力不足");
                return;
            }

            meleeFighter.ToTryAttack(targetFighter ?? playerController.TargetEnemy?.Fighter);
        }
    }

    /// <summary>
    /// 解析本次左键要不要走处决演出。返回 null = 按普通攻击处理。
    ///
    /// ① 弹反后处决（主路径）：`ParrySystem` 在弹反成功时记下敌人 + `Parry.riposte_window`(2s) 窗口，
    ///    这里只读不消费 —— 真正消费在发起演出时（`ClearRiposteTarget`），
    ///    否则"按了一下但距离不够"会把窗口白白吃掉。
    /// ② 敌人起手时处决（旧路径，默认关）：不需要弹反，容易变成无脑刷处决。
    /// </summary>
    private EnemyController ResolveExecutionTarget()
    {
        if (!enableCounterAttack) return null;

        // ① 弹反奖励
        var riposte = playerController != null && playerController.parrySystem != null
            ? playerController.parrySystem.GetRiposteTarget()
            : null;
        if (riposte != null && IsExecutable(riposte)) return riposte;

        // ② 旧路径
        if (counterOnEnemyWindup && EnemyManager.i != null)
        {
            var enemy = EnemyManager.i.GetAttackingEnemy();
            if (enemy != null && IsExecutable(enemy) && enemy.Fighter != null && enemy.Fighter.IsCounterable)
                return enemy;
        }

        return null;
    }

    /// <summary>处决目标的通用前置：活着、还没被别的演出锁住、贴身</summary>
    private bool IsExecutable(EnemyController enemy)
    {
        if (enemy == null || enemy.Fighter == null) return false;
        if (enemy.Health == null || enemy.Health.IsDead) return false;
        if (playerController == null || playerController.PlayerTransform == null) return false;

        Vector3 a = enemy.transform.position;
        Vector3 b = playerController.PlayerTransform.position;
        a.y = 0f;
        b.y = 0f;
        return Vector3.Distance(a, b) <= executionRange;
    }
}

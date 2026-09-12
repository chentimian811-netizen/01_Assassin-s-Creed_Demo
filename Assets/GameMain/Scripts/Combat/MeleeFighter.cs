using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

//枚举 攻击的四个阶段
public enum E_AttackState
{
    idle,
    Windup,//前摇
    Impact,//生效
    Cooldown,//后摇
}

/// <summary>
/// 近战攻击方 + 动画机。"被打"整体交给 Health 组件。
/// 伤害只通过 DamageRouter → Health；受击表现订阅 GameEvents.OnUnitDamaged。
/// </summary>
public class MeleeFighter : MonoBehaviour, IAttackSource, IParryTarget
{
    [SerializeField] private int weaponID = -1;
    private int upgradeLevel = 1;
    [SerializeField] List<AttackData> attacks;

    WeaponConfig currentWeapConfig;

    [SerializeField] private Health health;

    [Header("攻击设置")]
    [Tooltip("命中时暂停时间（秒）")]
    public float hitStopDuration = 0.1f;

    [Tooltip("命中的时间缩放,0 = 完全暂停,0.1=慢动作")]
    [Range(0f,1f)]
    public float hitStopTimeScale = 0f;

    private GameObject currentWeapon;

    SphereCollider leftHandeConllider, rightHandeConllider, leftFootConllider, rightFootConllider;

    public E_AttackState AttackState { get; private set; }

    BoxCollider WeaponCollider;
    Animator animator;
    RuntimeAnimatorController originalController;
    public bool IsAttackingHit { get; private set; } = false; //是否处于被攻击状态
    public bool inAction { get; private set; } = false;//是否处于攻击中
    public bool inCounter { get; set; } = false;//反击演出阶段
    bool doCombo;//连击标志
    int combocount = 0;//连技计数

    MeleeFighter currentTarget;

    /// <summary>兼容旧读取方：内部无存储，转发到 Health</summary>
    public float Health => health != null ? health.CurrentHealth : 0f;
    public float MaxHealth => health != null ? health.MaxHealth : 0f;

    /// <summary>血量组件转发，外部零成本取 Health</summary>
    public Health HealthComponent => health;

    public MeleeFighter CurrentTarget => currentTarget;

    // IAttackSource
    public AttackData CurrentAttack
    {
        get
        {
            if (AttackState != E_AttackState.Impact) return null;
            var list = ActiveAttacks;
            if (list == null || list.Count == 0) return null;
            int idx = Mathf.Clamp(combocount, 0, list.Count - 1);
            return list[idx];
        }
    }

    public int WeaponID => weaponID;
    public int UpgradeLevel => upgradeLevel;

    // IParryTarget
    public bool IsInParryWindow
    {
        get
        {
            if (AttackState != E_AttackState.Windup) return false;
            var list = ActiveAttacks;
            if (list == null || list.Count == 0) return true;
            int idx = Mathf.Clamp(combocount, 0, list.Count - 1);
            return list[idx] == null || list[idx].Parryable;
        }
    }

    public void OnParried(GameObject parrier)
    {
        StopAllCoroutines();
        AttackState = E_AttackState.idle;
        inAction = false;
        DisableAllHitxboxes();
        // P3：进入硬直状态
    }

    List<AttackData> ActiveAttacks =>
        (currentWeapConfig != null && currentWeapConfig.attacks != null && currentWeapConfig.attacks.Count > 0)
            ? currentWeapConfig.attacks
            : attacks;

    void Awake()
    {
        animator = GetComponent<Animator>();
        originalController = animator.runtimeAnimatorController;
        if (health == null) health = GetComponent<Health>();
        InitBoneCollider();
    }

    private void OnEnable()
    {
        GameEvents.OnUnitDamaged += HandleUnitDamaged;
        GameEvents.OnPlayerDeath += HandlePlayerDeath;
        GameEvents.OnEnemyKilled += HandleEnemyKilled;
    }

    private void OnDisable()
    {
        GameEvents.OnUnitDamaged -= HandleUnitDamaged;
        GameEvents.OnPlayerDeath -= HandlePlayerDeath;
        GameEvents.OnEnemyKilled -= HandleEnemyKilled;
    }

    private void Start()
    {
        // 敌人等未走 SetWeapon 的单位：自动接上预置武器
        if (currentWeapon == null)
        {
            TryBindPreplacedWeapon();
        }

        if (currentWeapon != null && WeaponCollider == null)
        {
            WeaponCollider = currentWeapon.GetComponent<BoxCollider>();
        }
        DisableAllHitxboxes();
    }

    void TryBindPreplacedWeapon()
    {
        var transforms = GetComponentsInChildren<Transform>(true);
        foreach (var t in transforms)
        {
            if (t == null || t.name != "Sword") continue;
            var box = t.GetComponent<BoxCollider>();
            if (box == null) continue;
            currentWeapon = t.gameObject;
            WeaponCollider = box;
            var rb = t.GetComponent<Rigidbody>();
            if (rb != null) Destroy(rb);
            return;
        }
    }

    public void SetWeapon(GameObject newWeapon)
    {
        currentWeapon = newWeapon;
        if(newWeapon != null)
        {
            WeaponCollider = newWeapon.GetComponent<BoxCollider>();
            DisableAllHitxboxes();
        }
        else
        {
            currentWeapon = null;
            WeaponCollider = null;
        }
    }

    public void SetWeaponConfig(WeaponConfig config)
    {
        currentWeapConfig = config;
        if(config != null && config.animOverride != null)
        {
            animator.runtimeAnimatorController = config.animOverride;
        }
        else
        {
            animator.runtimeAnimatorController = originalController;
        }
    }

    private void InitBoneCollider()
    {
        if(animator == null)return;
        leftHandeConllider = animator.GetBoneTransform(HumanBodyBones.LeftHand)?.GetComponent<SphereCollider>();
        rightHandeConllider = animator.GetBoneTransform(HumanBodyBones.RightHand)?.GetComponent<SphereCollider>();
        leftFootConllider = animator.GetBoneTransform(HumanBodyBones.LeftFoot)?.GetComponent<SphereCollider>();
        rightFootConllider = animator.GetBoneTransform(HumanBodyBones.RightFoot)?.GetComponent<SphereCollider>();
    }

    public void ToTryAttack(MeleeFighter target = null)
    {
        if (!inAction)
        {
            StartCoroutine(Attack(target));
        }
        else if (AttackState == E_AttackState.Impact || AttackState == E_AttackState.Cooldown)
        {
            doCombo = true;
        }
    }

    IEnumerator Attack(MeleeFighter target = null)
    {
        inAction = true;

        currentTarget = target;
        AttackState = E_AttackState.Windup;

        var activeAttacks = ActiveAttacks;
        if (activeAttacks == null || activeAttacks.Count == 0)
        {
            inAction = false;
            yield break;
        }

        animator.CrossFade(activeAttacks[combocount].AnimName, 0.2f);

        yield return null;

        var animState = animator.GetNextAnimatorStateInfo(1);

        float timer = 0f;
        float length = Mathf.Max(animState.length, 0.0001f);
        while (timer <= length)
        {
            if (IsAttackingHit)
            {
                break;
            }

            timer += Time.deltaTime;

            float normalizedTime = timer / length;

            if (AttackState == E_AttackState.Windup)
            {
                if (inCounter)
                    break;

                if (normalizedTime >= activeAttacks[combocount].ImpactStartTime)
                {
                    AttackState = E_AttackState.Impact;
                    EnableHitbox(activeAttacks[combocount]);
                }
            }
            else if (AttackState == E_AttackState.Impact)
            {
                if (normalizedTime >= activeAttacks[combocount].ImpactEndTime)
                {
                    AttackState = E_AttackState.Cooldown;
                    DisableAllHitxboxes();
                }
            }
            else if (AttackState == E_AttackState.Cooldown)
            {
                if (doCombo)
                {
                    doCombo = false;
                    combocount = (combocount + 1) % activeAttacks.Count;

                    StartCoroutine(Attack(target));
                    yield break;
                }
            }

            yield return null;
        }

        AttackState = E_AttackState.idle;
        DisableAllHitxboxes();
        combocount = 0;
        inAction = false;
        currentTarget = null;
    }

    /// <summary>
    /// 本回调在【受击方】身上触发，other 是【攻击方】的 hitbox 碰撞体。
    /// </summary>
    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Hitbox")) return;
        if (IsAttackingHit || inCounter) return;

        var attackerFighter = other.GetComponentInParent<MeleeFighter>();
        if (attackerFighter == null || attackerFighter == this) return;

        // 攻击方已锁定别的目标时不误伤
        if (attackerFighter.currentTarget != null && attackerFighter.currentTarget != this) return;

        // ⚠️ target 必须是"我自己"的 Health —— 绝不能把 other（攻击方 hitbox）当 target
        if (health == null) health = GetComponent<Health>();
        if (health == null) return;

        // 命中点取"我身体上离攻击方 hitbox 最近的点"
        var myCollider = GetComponent<Collider>();
        Vector3 hitPoint = myCollider != null
            ? myCollider.ClosestPoint(other.transform.position)
            : transform.position;

        var atk = attackerFighter.CurrentAttack;
        bool applied = DamageRouter.Apply(health, attackerFighter.gameObject, hitPoint,
            atk == null || atk.Parryable);
        if (!applied) return;

        // 攻击方驱动的反馈：卡肉参数取自招式配置
        if (HitStopManager.Instance != null)
        {
            if (atk != null) HitStopManager.Instance.Play(atk.HitStopDuration, atk.HitStopTimeScale);
            else HitStopManager.Instance.Play(hitStopDuration, hitStopTimeScale);
        }

        // 保持原有语义：【玩家被打】时震屏
        if (CompareTag("Player")) CameraManager.Instance?.ShakeScreen();
    }

    /// <summary>
    /// 受击表现（玩家与敌人共用）。替代被删除的 PlayerHitReaction 直调。
    /// 敌人的 FSM 状态切换由 EnemyController.HandleUnitDamaged 负责。
    /// </summary>
    private void HandleUnitDamaged(string victimUnitId, DamageInfo info)
    {
        if (health == null || victimUnitId != health.UnitId) return;
        if (health.IsDead) return;
        if (inCounter) return;
        StartCoroutine(HitReaction(info.Source != null ? info.Source.transform : null));
    }

    private IEnumerator HitReaction(Transform attacker)
    {
        inAction = true;
        IsAttackingHit = true;

        if (attacker != null)
        {
            var dispVec = attacker.position - transform.position;
            dispVec.y = 0f;
            if (dispVec != Vector3.zero) transform.rotation = Quaternion.LookRotation(dispVec);
        }

        animator.CrossFade("Melee_Impact", 0.2f);
        yield return null;

        var animState = animator.GetNextAnimatorStateInfo(1);
        yield return new WaitForSeconds(animState.length * 0.60f);

        GameEvents.RaiseHitReactionComplete(health.UnitId);
        inAction = false;
        IsAttackingHit = false;
    }

    private void HandleEnemyKilled(string killedUnitId)
    {
        if (health == null || killedUnitId != health.UnitId) return;
        StopAllCoroutines();
        animator.CrossFade("Melee_FallBackDeath", 0.2f);
        DisableAllHitxboxes();
    }

    private void HandlePlayerDeath()
    {
        if (!CompareTag("Player")) return;
        StopAllCoroutines();
        animator.CrossFade("Melee_FallBackDeath", 0.2f);
    }

    public IEnumerator PerformCounterAttack(EnemyController opponet)
    {
        inAction = true;
        inCounter = true;
        opponet.Fighter.inCounter = true;

        // 处决无敌，避免演出中被二次命中
        health?.SetInvulnerable(InvulnReasons.Counter, true);
        opponet.Fighter.HealthComponent?.SetInvulnerable(InvulnReasons.Counter, true);

        var disVec = opponet.transform.position - transform.position;
        disVec.y = 0;

        transform.rotation = Quaternion.LookRotation(disVec);
        opponet.transform.rotation = Quaternion.LookRotation(-disVec);

        var targetPos = opponet.transform.position - disVec.normalized * 2f;

        animator.CrossFade("Melee_CounterAttack", 0.2f);
        opponet.Animator.CrossFade("Melee_CounterVictim", 0.2f);

        yield return null;

        var animState = animator.GetNextAnimatorStateInfo(1);

        float timer = 0f;
        float length = Mathf.Max(animState.length, 0.0001f);
        while (timer <= length)
        {
            transform.position = Vector3.MoveTowards(transform.position, targetPos, 2 * Time.deltaTime);
            yield return null;
            timer += Time.deltaTime;
        }

        // 致死只走伤害唯一路径，不再直接 ChangeState(Dead)
        var enemyCol = opponet.GetComponent<Collider>();
        if (enemyCol != null)
        {
            DamageRouter.ApplyAmount(
                enemyCol, gameObject, opponet.transform.position,
                9999f, E_DamageSource.Player, parryable: false, attackId: "counter_execute");
        }
        else
        {
            opponet.ChangeState(E_EnemyState.Dead);
        }

        inCounter = false;
        opponet.Fighter.inCounter = false;
        health?.SetInvulnerable(InvulnReasons.Counter, false);

        inAction = false;
    }

    void EnableHitbox(AttackData attack)
    {
        if (WeaponCollider == null && currentWeapon != null)
            WeaponCollider = currentWeapon.GetComponent<BoxCollider>();

        switch (attack.HitboxToUse)
        {
            case E_AttackHitbox.LeftHande:
                if(leftHandeConllider != null) leftHandeConllider.enabled = true;
                break;
            case E_AttackHitbox.RightHande:
                if(rightHandeConllider !=null) rightHandeConllider.enabled = true;
                break;
            case E_AttackHitbox.LeftFoot:
                if(leftFootConllider != null)leftFootConllider.enabled = true;
                break;
            case E_AttackHitbox.RightFoot:
                if(rightFootConllider != null) rightFootConllider.enabled = true;
                break;
            case E_AttackHitbox.Weapon:
                if (rightHandeConllider != null) rightHandeConllider.enabled = true;
                if (WeaponCollider != null) WeaponCollider.enabled = true;
                break;
            default:
                break;
        }
    }

    void DisableAllHitxboxes()
    {
        if (leftHandeConllider != null) leftHandeConllider.enabled = false;
        if (rightHandeConllider != null) rightHandeConllider.enabled = false;
        if (leftFootConllider != null) leftFootConllider.enabled = false;
        if (rightFootConllider != null) rightFootConllider.enabled = false;
        if (WeaponCollider != null) WeaponCollider.enabled = false;
    }

    public List<AttackData> Attacks => attacks;

    public bool IsCounterable => AttackState == E_AttackState.Windup && combocount == 0;

    public void SetUpgradeLevel(int level) => upgradeLevel = level;

    public void SetWeaponID(int id) => weaponID = id;
}

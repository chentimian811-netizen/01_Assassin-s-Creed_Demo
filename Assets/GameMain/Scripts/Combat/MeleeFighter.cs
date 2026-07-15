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
public class MeleeFighter : MonoBehaviour
{
    [field: SerializeField] public float Health { get; private set; } = 25f;

    [SerializeField] private int weaponID = -1;
    private int upgradeLevel = 1;
    [SerializeField] List<AttackData> attacks;

    WeaponConfig currentWeapConfig;
    
    [Header("攻击设置")]
    [Tooltip("命中时暂停时间（秒）")]
    public float hitStopDuration = 0.1f;

    [Tooltip("命中的时间缩放,0 = 完全暂停,0.1=慢动作")]
    [Range(0f,1f)]
    public float hitStopTimeScale = 0f;

    static bool isHitStopActive = false;

    private GameObject currentWeapon;

    SphereCollider leftHandeConllider, rightHandeConllider, leftFootConllider, rightFootConllider;

    public E_AttackState AttackState { get; private set; }

    public event Action<MeleeFighter> OnGotHit;
    public event Action OnHitComplete;

    BoxCollider WeaponCollider;
    Animator animator;
    RuntimeAnimatorController originalController;
    public bool IsAttackingHit { get; private set; } = false; //是否处于被攻击状态
    public bool inAction { get; private set; } = false;//是否处于攻击中
    public bool inCounter { get; set; } = false;//反击演出阶段
    bool doCombo;//连击标志
    int combocount = 0;//连技计数

    void Awake()
    {
        animator = GetComponent<Animator>();
        originalController = animator.runtimeAnimatorController;
        InitBoneCollider();

    }
    private void Start()
    {   

        if (currentWeapon != null)
        {
            WeaponCollider = currentWeapon.GetComponent<BoxCollider>();
            DisableAllHitxboxes();
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
        if (!inAction)//没有攻击——>启动
        {
            StartCoroutine(Attack(target));
        }
        else if (AttackState == E_AttackState.Impact || AttackState == E_AttackState.Cooldown)//处于生效/后摇——>排队
        {
            doCombo = true;
        }
    }

    MeleeFighter currentTarget;

    IEnumerator Attack(MeleeFighter target = null)
    {
        inAction = true;

        currentTarget = target;
        AttackState = E_AttackState.Windup;

        var activeAttacks =(currentWeapConfig != null && currentWeapConfig.attacks.Count > 0)
            ?currentWeapConfig.attacks : attacks;

        animator.CrossFade(activeAttacks[combocount].AnimName, 0.2f);

        yield return null;

        var animState = animator.GetNextAnimatorStateInfo(1);

        float timer = 0f;
        while (timer <= animState.length)
        {
            if (IsAttackingHit)
            {
                break;
            }

            timer += Time.deltaTime;

            float normalizedTime = timer / animState.length;

            if (AttackState == E_AttackState.Windup)
            {
                if (inCounter)
                    break;

                if (normalizedTime >= activeAttacks[combocount].ImpactStartTime)
                {
                    AttackState = E_AttackState.Impact;
                    EnableHitbox(activeAttacks[combocount]);

                    //SwordCollider.enabled = true;
                }
            }
            else if (AttackState == E_AttackState.Impact)
            {
                if (normalizedTime >= activeAttacks[combocount].ImpactEndTime)
                {
                    AttackState = E_AttackState.Cooldown;
                    DisableAllHitxboxes();

                    //SwordCollider.enabled = false;
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

        //yield return new WaitForSeconds(animState.length);
        combocount = 0;
        inAction = false;
        currentTarget = null;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.tag == "Hitbox" && !IsAttackingHit && !inCounter)
        {
            var attacker = other.GetComponentInParent<MeleeFighter>();
            if (attacker == null || attacker == this) return;

            if(attacker.currentTarget != null && attacker.currentTarget != this)
            {
                return;
            }

            float damage = 5f;
            if(attacker.weaponID >0 && DataRepository.ItemTable.TryGetValue(attacker.weaponID,out var weaponItem))
            {
                damage = WeaponUpgradeSystem.CalculateDamage(weaponItem.BaseDamage,attacker.upgradeLevel);
            }
            TakeDamage(damage);

            OnGotHit?.Invoke(attacker);

            
            //触发卡肉效果
            attacker.HitStop();

            //触发屏幕震动效果
            if (CompareTag("Player"))
            {
                CameraManager.Instance.ShakeScreen();
            }

            if (Health > 0)
            {
                StartCoroutine(PlayerHitReaction(other.GetComponentInParent<MeleeFighter>().transform));
            }
            else
            {
                PlayDeathAnimation(attacker);
            }
                
        }
    }

    public void HitStop()
    {
        if(isHitStopActive) return;
        StartCoroutine(HitStopCoroution());
    }

    IEnumerator HitStopCoroution()
    {
        isHitStopActive = true;
        //保持原始时间缩放
        float originalTimeScale = Time.timeScale;

        //设置为卡肉时间缩放
        Time.timeScale = hitStopTimeScale;

        yield return new WaitForSecondsRealtime(hitStopDuration);

        Time.timeScale = originalTimeScale;
        isHitStopActive = false;
    }

    public void TakeDamage(float damage)
    {
        Health = Mathf.Clamp(Health - damage, 0, Health);
    }

    /// <summary>
    /// 受到伤害（带攻击者信息，触发受击/死亡动画）
    /// </summary>
    public void TakeDamage(float damage, MeleeFighter attacker)
    {
        TakeDamage(damage);

        //触发受击事件
        OnGotHit?.Invoke(attacker);

        //根据血量播放动画
        if (Health > 0)
        {
            if (attacker != null)
            {
                StartCoroutine(PlayerHitReaction(attacker.transform));
            }
        }
        else
        {
            PlayDeathAnimation(attacker);
        }
    }

    IEnumerator PlayerHitReaction(Transform attacker)
    {
        inAction = true;
        IsAttackingHit = true;
        var dispVec = attacker.position - transform.position;
        dispVec.y = 0;
        //防止零向量报错
        if (dispVec != Vector3.zero)
        {
            transform.rotation = Quaternion.LookRotation(dispVec);
        }

        animator.CrossFade("Melee_Impact", 0.2f);

        yield return null;

        var animState = animator.GetNextAnimatorStateInfo(1);

        yield return new WaitForSeconds(animState.length * 0.60f);


        OnHitComplete?.Invoke();
        inAction = false;
        IsAttackingHit = false;
    }
    public IEnumerator PerformCounterAttack(EnemyController opponet)//实现反击动画
    {
        inAction = true;

        inCounter = true;
        opponet.Fighter.inCounter = true;
        opponet.ChangeState(E_EnemyState.Dead);

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
        while (timer <= animState.length)
        {
            transform.position = Vector3.MoveTowards(transform.position, targetPos, 2 * Time.deltaTime);

            yield return null;
            timer += Time.deltaTime;
        }

        inCounter = false;
        opponet.Fighter.inCounter = false;

        inAction = false;
    }

    void PlayDeathAnimation(MeleeFighter attacker)
    {
        animator.CrossFade("Melee_FallBackDeath", 0.2f);
    }

    void EnableHitbox(AttackData attack)
    {
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
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
    [SerializeField] List<AttackData> attacks;
    
    [Header("攻击设置")]
    [Tooltip("命中时暂停时间（秒）")]
    public float hitStopDuration = 0.1f;

    [Tooltip("命中的时间缩放，0 = 完全赞同，0.1=慢动作")]
    [Range(0f,1f)]
    public float hitStopTimeScale = 0f;

    static bool isHitStopActive = false;

    [SerializeField] GameObject Sword;

    SphereCollider leftHandeConllider, rightHandeConllider, leftFootConllider, rightFootConllider;

    public E_AttackState AttackState { get; private set; }

    public event Action<MeleeFighter> OnGotHit;
    public event Action OnHitComplete;

    BoxCollider SwordCollider;

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

    }
    private void Start()
    {
        if (Sword != null)
        {
            SwordCollider = Sword.GetComponent<BoxCollider>();

            leftHandeConllider = animator.GetBoneTransform(HumanBodyBones.LeftHand).GetComponent<SphereCollider>();
            leftFootConllider = animator.GetBoneTransform(HumanBodyBones.LeftFoot).GetComponent<SphereCollider>();
            rightHandeConllider = animator.GetBoneTransform(HumanBodyBones.RightHand).GetComponent<SphereCollider>();
            rightFootConllider = animator.GetBoneTransform(HumanBodyBones.RightFoot).GetComponent<SphereCollider>();


            DisableAllHitxboxes();
        }
    }

    public void SetWeapon(GameObject newSword)
    {
        Sword = newSword;
        if(newSword != null)
        {
            SwordCollider = newSword.GetComponent<BoxCollider>();

            if (leftHandeConllider == null && animator != null)
            {
                leftHandeConllider = animator.GetBoneTransform(HumanBodyBones.LeftHand).GetComponent<SphereCollider>();
                leftFootConllider = animator.GetBoneTransform(HumanBodyBones.LeftFoot).GetComponent<SphereCollider>();
                rightHandeConllider = animator.GetBoneTransform(HumanBodyBones.RightHand).GetComponent<SphereCollider>();
                rightFootConllider = animator.GetBoneTransform(HumanBodyBones.RightFoot).GetComponent<SphereCollider>();
            }
            DisableAllHitxboxes();
        }
        else
        {
            Sword = null;
            SwordCollider = null;
        }
    }

    //应用武器动画覆盖，替代Animator中的动画片段
    public void SetAnimatorOverride(RuntimeAnimatorController overrdeController)
    {
        if(animator == null || overrdeController == null)return;
        animator.runtimeAnimatorController = overrdeController;
    }

    //恢复原始动画控制器(卸装武器时调用)
    public void ClearAnimatorOverride()
    {
        if(animator == null || originalController == null)return;
        animator.runtimeAnimatorController = originalController;
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

        animator.CrossFade(attacks[combocount].AnimName, 0.2f);

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

                if (normalizedTime >= attacks[combocount].ImpactStartTime)
                {
                    AttackState = E_AttackState.Impact;
                    EnableHitbox(attacks[combocount]);

                    //SwordCollider.enabled = true;
                }
            }
            else if (AttackState == E_AttackState.Impact)
            {
                if (normalizedTime >= attacks[combocount].ImpactEndTime)
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
                    combocount = (combocount + 1) % attacks.Count;

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

            TakeDamage(5f);
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

        animator.CrossFade("SwordImpact", 0.2f);

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

        animator.CrossFade("CounterAttack", 0.2f);
        opponet.Animator.CrossFade("CounterAttackVictim", 0.2f);

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
        animator.CrossFade("FallBackDeath", 0.2f);
    }

    void EnableHitbox(AttackData attack)
    {
        switch (attack.HitboxToUse)
        {
            case E_AttackHitbox.LeftHande:
                leftHandeConllider.enabled = true;
                break;
            case E_AttackHitbox.RightHande:
                rightHandeConllider.enabled = true;
                break;
            case E_AttackHitbox.LeftFoot:
                leftFootConllider.enabled = true;
                break;
            case E_AttackHitbox.RightFoot:
                rightFootConllider.enabled = true;
                break;
            case E_AttackHitbox.Sword:
                if (SwordCollider != null) SwordCollider.enabled = true;
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
        if (SwordCollider != null) SwordCollider.enabled = false;
    }

    public List<AttackData> Attacks => attacks;

    public bool IsCounterable => AttackState == E_AttackState.Windup && combocount == 0;
}
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor.Search;
using UnityEngine;


public enum E_AttackState
{
    idle,
    Windup,
    Impact,
    Cooldown,
}


public class MeeleFighter : MonoBehaviour
{
    [field: SerializeField] public float Health { get; private set; } = 25f;



    [SerializeField] List<AttackData> attacks;
    [SerializeField] GameObject Sword;

    SphereCollider leftHandeConllider, rightHandeConllider, leftFootConllider, rightFootConllider;

    public E_AttackState AttackState { get; private set; }

    public event Action<MeeleFighter> OnGotHit;
    public event Action OnHitComplete;

    BoxCollider SwordCollider;

    Animator animator;

    public bool IsAttackingHit { get; private set; } = false;

    public bool inAction { get; private set; } = false;

    public bool inCounter { get; set; } = false;

    bool doCombo;
    int combocount = 0;

    void Awake()
    {
        animator = GetComponent<Animator>();

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
        }
        else
        {
            SwordCollider = null;
        }
    }

    public void ToTryAttack(MeeleFighter target = null)
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

    MeeleFighter currentTarget;

    IEnumerator Attack(MeeleFighter target = null)
    {
        inAction = true;

        currentTarget = target;
        AttackState = E_AttackState.Windup;

        animator.CrossFade(attacks[combocount].AnimName, 0.2f);//使用交叉变化 从上一个动画慢慢过渡到下一个动画（slash）

        yield return null;//等待一帧 

        var animState = animator.GetNextAnimatorStateInfo(1);//获取下一个动画的状态信息

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
                    //开启碰撞
                }
            }
            else if (AttackState == E_AttackState.Impact)
            {
                if (normalizedTime >= attacks[combocount].ImpactEndTime)
                {
                    AttackState = E_AttackState.Cooldown;
                    DisableAllHitxboxes();

                    //SwordCollider.enabled = false ;
                    //关闭碰撞
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

            yield return null;//等待一帧
        }

        AttackState = E_AttackState.idle;

        //yield return new WaitForSeconds(animState.length);//根据动画的长度进行等待
        combocount = 0;
        inAction = false; //结束动画
        currentTarget = null;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.tag == "Hitbox" && !IsAttackingHit && !inCounter)
        {
            Debug.Log("打中了！");
            var attacker = other.GetComponentInParent<MeeleFighter>();
            if(attacker.currentTarget != null && attacker.currentTarget != this)
            {
                return;
            }

            TakeDamage(5f);
            OnGotHit?.Invoke(attacker);

            if (Health > 0)
            {
                StartCoroutine(PlayerHitReaction(other.GetComponentInParent<MeeleFighter>().transform));
            }
            else
            {
                PlayDeathAnimation(attacker);
            }
                
        }
    }

    void TakeDamage(float damage)
    {
        Health = Mathf.Clamp(Health - damage, 0, Health); 
    }

    IEnumerator PlayerHitReaction(Transform attacker)
    {
        inAction = true;
        IsAttackingHit = true;
        var dispVec = attacker.position - transform.position;
        dispVec.y = 0;
        transform.rotation = Quaternion.LookRotation(dispVec);

        

        animator.CrossFade("SwordImpact", 0.2f);//使用交叉变化 从上一个动画慢慢过渡到下一个动画（slash）

        yield return null;//等待一帧 

        var animState = animator.GetNextAnimatorStateInfo(1);//获取下一个动画的状态信息

        yield return new WaitForSeconds(animState.length * 0.60f);//根据动画的长度进行等待


        OnHitComplete?.Invoke();
        inAction = false; //结束动画
        IsAttackingHit = false;
    }
    public IEnumerator PerformCounterAttack(EnemyController opponet)
    {
        inAction = true;

        inCounter = true;
        opponet.Fighter.inCounter = true;
        opponet.ChangeState(E_EnemyState.Dead);

        var disVec = opponet.transform.position - transform.position;
        disVec.y = 0;

        transform.rotation = Quaternion.LookRotation(disVec);
        opponet.transform.rotation = Quaternion.LookRotation(-disVec);

        var targetPos = opponet.transform.position - disVec.normalized * 2.7f;

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

        inAction = false; //结束动画
    }

    void PlayDeathAnimation(MeeleFighter attacker)
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
                SwordCollider.enabled = true;
                break;
            default:
                break;
        }
    }

    void DisableAllHitxboxes()
    {
        if (leftFootConllider != null)
        {
            leftHandeConllider.enabled = false;
        }

        if (leftHandeConllider != null)
        {
            leftFootConllider.enabled = false;
        }

        if (rightFootConllider != null)
        {
            rightHandeConllider.enabled = false;
        }
        if (leftHandeConllider != null)
        {
            rightFootConllider.enabled = false;
        }
        if (SwordCollider != null)
        {
            SwordCollider.enabled = false;
        }

    }

    public List<AttackData> Attacks => attacks;

    public bool IsCounterable => AttackState == E_AttackState.Windup && combocount == 0;
}
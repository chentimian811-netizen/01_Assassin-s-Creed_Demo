using System;
using System.Collections;
using System.Collections.Generic;
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
    [SerializeField] List<AttackData> attacks;
    [SerializeField] GameObject Sword;

    E_AttackState attackState;

    BoxCollider SwordCollider;

    Animator animator;

    bool inAction = false;

    bool docombo;
    int combocount = 0;

    void Awake()
    {
        animator = GetComponent<Animator>();
        
    }
    private void Start()
    {
        if (Sword != null)
        {
            SwordCollider =Sword.GetComponent<BoxCollider>();
            SwordCollider.enabled = false;
        }
    }

    public void ToTryAttack()
    {  
        if (!inAction)
        {
            StartCoroutine(Attack());
        }
        else if (attackState == E_AttackState.Impact || attackState == E_AttackState.Cooldown)
        {
            docombo = true;

        }
    }

    IEnumerator Attack()
    {
        inAction = true;
        attackState = E_AttackState.Windup;

        animator.CrossFade(attacks[combocount].AnimName,0.2f);//使用交叉变化 从上一个动画慢慢过渡到下一个动画（slash）

        yield return null;//等待一帧 

        var animState = animator.GetNextAnimatorStateInfo(1);//获取下一个动画的状态信息

        float timer = 0f;
        while (timer <= animState.length)
        {
            timer += Time.deltaTime;

            float normalizedTime = timer / animState.length;

            if(attackState == E_AttackState.Windup)
            {
                if (normalizedTime >= attacks[combocount].ImpactStartTime)
                {
                    attackState = E_AttackState.Impact;
                    SwordCollider.enabled = true;
                    //开启碰撞
                }
            }
            else if (attackState == E_AttackState.Impact)
            {
                if(normalizedTime >= attacks[combocount].ImpactEndTime)
                {
                    attackState = E_AttackState.Cooldown;
                    SwordCollider.enabled = false ;
                    //关闭碰撞
                }
            }
            else if (attackState == E_AttackState.Cooldown)
            {
                if( docombo )
                {
                    docombo = false;
                    combocount = (combocount + 1) % attacks.Count;

                    StartCoroutine(Attack());
                    yield break;
                }
            }

                yield return null;//等待一帧
        }

        attackState = E_AttackState.idle;

        //yield return new WaitForSeconds(animState.length);//根据动画的长度进行等待
        combocount = 0 ;

        inAction = false; //结束动画
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.tag == "Hitbox" && !inAction)
        {
            Debug.Log("打中了");
            StartCoroutine(PlayerHitReaction());
        }
    }
    IEnumerator PlayerHitReaction()
    {
        inAction = true;
        animator.CrossFade("SwordImpact", 0.2f);//使用交叉变化 从上一个动画慢慢过渡到下一个动画（slash）

        yield return null;//等待一帧 

        var animState = animator.GetNextAnimatorStateInfo(1);//获取下一个动画的状态信息

        yield return new WaitForSeconds(animState.length * 0.8f);//根据动画的长度进行等待

        inAction = false; //结束动画
    }

}

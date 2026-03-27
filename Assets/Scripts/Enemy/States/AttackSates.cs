using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AttackSates : State<EnemyController>
{
    [SerializeField] float attackDistance = 2.0f;

    EnemyController enemy;


    bool isAttacking;
    public override void Enter(EnemyController owner)
    {
        enemy = owner;
        enemy.NavAgent.stoppingDistance  = attackDistance;
    }

    public override void Execute()
    {
        if (isAttacking) return;

        enemy.NavAgent.SetDestination(enemy.Target.transform.position);

        if (Vector3.Distance(enemy.Target.transform.position, enemy.transform.position) <= attackDistance + 0.5f)
        {
            StartCoroutine(Attack());
        }

    }

    IEnumerator Attack()
    {
        isAttacking = true;
        enemy.Animator.applyRootMotion = true;

        enemy.Fighter.ToTryAttack();
        yield return new WaitUntil(() => enemy.Fighter.AttackState == E_AttackState.idle); 

        enemy.Animator.applyRootMotion = false;
        isAttacking = false;
    }
}

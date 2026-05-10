using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AttackSates : State<EnemyController>
{
    [SerializeField] float attackDistance = 3.0f;

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

        if (Vector3.Distance(enemy.Target.transform.position, enemy.transform.position) <= attackDistance + 0.3f) 
        {
            StartCoroutine(Attack(Random.Range(0,enemy.Fighter.Attacks.Count + 1)));
        }

    }

    IEnumerator Attack( int comboCount = 1)
    {
        isAttacking = true;
        enemy.Animator.applyRootMotion = true;

        enemy.Fighter.ToTryAttack(enemy.Target);


        for(int i = 1; i < comboCount; i++)
        {
            yield return new WaitUntil(() => enemy.Fighter.AttackState == E_AttackState.Cooldown);
            enemy.Fighter.ToTryAttack(enemy.Target);
        }

        yield return new WaitUntil(() => enemy.Fighter.AttackState == E_AttackState.idle); 

        enemy.Animator.applyRootMotion = false;
        isAttacking = false;


        if (enemy.IsInState(E_EnemyState.Attack))
        {
            enemy.ChangeState(E_EnemyState.ReteatAfterAttack);
        }
        
    }

    public override void Exit()
    {
        enemy.NavAgent.ResetPath();
    }
}

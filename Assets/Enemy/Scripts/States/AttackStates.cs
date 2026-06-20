using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AttackStates : State<EnemyController>
{
    [SerializeField] float attackDistance = 2.5f;

    EnemyController enemy;

    bool isAttacking;
    Coroutine attackCoroutine; // 保存协程引用，用于退出时停止

    public override void Enter(EnemyController owner)
    {
        enemy = owner;
        enemy.NavAgent.stoppingDistance  = attackDistance;
    }

    public override void Execute()
    {
        if (isAttacking) return;

        // 防御性检查：Target为空或已死亡时退回CombatMovement
        if (enemy.Target == null || enemy.Target.Health <= 0)
        {
            enemy.ChangeState(E_EnemyState.CombatMovement);
            return;
        }

        enemy.NavAgent.SetDestination(enemy.Target.transform.position);

        if (Vector3.Distance(enemy.Target.transform.position, enemy.transform.position) <= attackDistance + 0.3f)
        {
            attackCoroutine = StartCoroutine(Attack(Random.Range(0,enemy.Fighter.Attacks.Count + 1)));
        }

    }

    IEnumerator Attack( int comboCount = 1)
    {
        isAttacking = true;
        enemy.NavAgent.updatePosition = false;
        enemy.Animator.applyRootMotion = true;

        // 防御：如果此时inAction为true（可能残留），先等待它变为false再发起攻击
        if (enemy.Fighter.inAction)
        {
            yield return new WaitUntil(() => !enemy.Fighter.inAction);
            // 等待期间状态可能已被切换，再次确认仍在Attack状态
            if (!enemy.IsInState(E_EnemyState.Attack))
            {
                enemy.Animator.applyRootMotion = false;
                enemy.NavAgent.updatePosition = true;
                isAttacking = false;
                yield break;
            }
        }

        enemy.Fighter.ToTryAttack(enemy.Target);


        for(int i = 1; i < comboCount; i++)
        {
            yield return new WaitUntil(() => enemy.Fighter.AttackState == E_AttackState.Cooldown);
            // 每次连击前检查状态是否仍然有效
            if (!enemy.IsInState(E_EnemyState.Attack))
            {
                enemy.Animator.applyRootMotion = false;
                enemy.NavAgent.updatePosition = true;
                isAttacking = false;
                yield break;
            }
            enemy.Fighter.ToTryAttack(enemy.Target);
        }

        yield return new WaitUntil(() => enemy.Fighter.AttackState == E_AttackState.idle);

        enemy.Animator.applyRootMotion = false;
        isAttacking = false;
        attackCoroutine = null;

        if (enemy.IsInState(E_EnemyState.Attack))
        {   
            enemy.ChangeState(E_EnemyState.RetreatAfterAttack);
        }

    }

    public override void Exit()
    {
        // 停止正在运行的攻击协程，防止协程脱离状态生命周期
        if (attackCoroutine != null)
        {
            enemy.StopCoroutine(attackCoroutine);
            attackCoroutine = null;
        }
        // 重置标志位，确保下次进入时不会被阻塞
        isAttacking = false;
        // 确保RootMotion被关闭
        enemy.Animator.applyRootMotion = false;
        enemy.NavAgent.updatePosition = true;
        enemy.NavAgent.ResetPath();
    }
}

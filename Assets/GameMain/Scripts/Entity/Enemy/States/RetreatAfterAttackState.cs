using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting.Dependencies.NCalc;
using UnityEngine;

public class RetreatAfterAttackState : State<EnemyController>
{
    // 后撤速度/距离/转身速度都来自 Enemy 表（转身速度原来硬编码 500）
    EnemyController enemy;

    Vector3 targetPos;  

    public override void Enter(EnemyController owner)
    {
        enemy = owner;
        targetPos = enemy.Target.transform.position;

        // 将NavAgent的内部位置同步到当前实际位置
        // 防止updatePosition重新开启时NavAgent产生向前的位移修正
        enemy.NavAgent.Warp(enemy.transform.position);
    }

    public override void Execute()
    {
        if(Vector3.Distance(enemy.transform.position,targetPos )>= (enemy.Config?.DistanceToRetreat ?? 0f))
        {
            enemy.ChangeState(E_EnemyState.CombatMovement);
            return;
        }

        var vecToTarget = enemy.Target.transform.position - enemy.transform.position;
        vecToTarget.y = 0f; 
        enemy.NavAgent.Move(-vecToTarget.normalized * (enemy.Config?.BackwardWalkSpeed ?? 0f) * Time.deltaTime);
        transform.rotation=Quaternion.RotateTowards(transform.rotation,Quaternion.LookRotation(vecToTarget),
            (enemy.Config?.RetreatTurnSpeed ?? 0f) * Time.deltaTime);
    }

    public override void Exit()
    {
        base.Exit();
    }
}

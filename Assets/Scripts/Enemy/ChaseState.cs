using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ChaseState : State<EnemyController>
{
    EnemyController enemy;
    [SerializeField] float distanceToSand = 3f;

    public override void Enter(EnemyController owner)
    {
         enemy = owner;

        enemy.NavAgent.stoppingDistance = distanceToSand;
    }

    public override void Execute()
    {
        enemy.NavAgent.SetDestination(enemy.Target.transform.position);
        enemy.animator.SetFloat("MoveAmount",enemy.NavAgent.velocity.magnitude / enemy.NavAgent.speed );
    }

    public override void Exit()
    {
        
    }
}

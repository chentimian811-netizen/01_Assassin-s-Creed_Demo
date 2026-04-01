using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public class IdleState : State<EnemyController> 
{
    EnemyController Enemy;

    public override void Enter(EnemyController owner)
    {
        Enemy = owner;

        Enemy.Animator.SetBool("combatMode", false);

    }

    public override void Execute() 
    {
        foreach(var target in Enemy.TargetsInRange)
        {
            var vecToTarget =  target.transform.position - transform.position;

            float angle = Vector3.Angle(transform.forward , vecToTarget);  

            if(angle <= Enemy.Fov / 2)
            {
                Enemy.Target = target;
                Enemy.ChangeState(E_EnemyState.CombatMovement);
                break; 
            }
        }
    }

    public override void Exit()
    {
       
    }
}

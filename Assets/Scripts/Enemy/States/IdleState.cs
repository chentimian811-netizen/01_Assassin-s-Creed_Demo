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
        Enemy.Target = Enemy.FindTarget();
        if(Enemy.Target != null)
        {
            Enemy.ChangeState(E_EnemyState.CombatMovement);
        }
    }

    public override void Exit()
    {
       
    }
}

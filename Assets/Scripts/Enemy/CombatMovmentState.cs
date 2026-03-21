using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum E_AICombatStates
{
    idle,
    Chase,
    Circling,
}

public class CombatMovmentState : State<EnemyController>
{
    EnemyController enemy;
    E_AICombatStates state;
    [SerializeField] float distanceToSand = 3f;
    [SerializeField] float adjustDistanceThreshold = 1f;

    public override void Enter(EnemyController owner)
    {
         enemy = owner;

        enemy.NavAgent.stoppingDistance = distanceToSand;
    }

    public override void Execute()
    {
        if(Vector3.Distance(enemy.Target.transform.position,enemy.transform.position) < distanceToSand + adjustDistanceThreshold)
        {
            StartChase();
        }


        if(state == E_AICombatStates.idle)
        {

        }
        else if(state == E_AICombatStates.Chase)
        {
            if (Vector3.Distance(enemy.Target.transform.position, enemy.transform.position) <= distanceToSand + 0.03f)
            {
                StartIdle();
            }
                enemy.NavAgent.SetDestination(enemy.Target.transform.position);
        }
        else if(state == E_AICombatStates.Circling)
        {

        }

        
    }

    void StartChase()
    {
        state = E_AICombatStates.Chase;
        enemy.animator.SetBool("combatMode", false);
    }
    void StartIdle()
    {
        state = E_AICombatStates.idle;
        enemy.animator.SetBool("combatMode", true);
    }

    public override void Exit()
    {
        
    }
}

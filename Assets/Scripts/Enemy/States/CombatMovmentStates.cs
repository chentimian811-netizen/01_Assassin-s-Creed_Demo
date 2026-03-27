using System.Collections;
using UnityEngine;
using UnityEngine.SocialPlatforms;
using System.Collections.Generic;
using UnityEngine.AI;

public enum E_AICombatStates
{
    idle,
    Chase,
    Circling,
}

public class CombatMovmentStates : State<EnemyController>
{
    EnemyController enemy;
    E_AICombatStates state;
    [SerializeField] float circlingSpeed = 20f;
    [SerializeField] float distanceToSand = 3f;
    [SerializeField] float adjustDistanceThreshold = 1f;
    [SerializeField] Vector2 idleTimeRange = new Vector2(2, 5);
    [SerializeField] Vector2 circlingTimeRange = new Vector2(4, 6);

    float timer = 0f;
    int circlingDir = 1;

    public override void Enter(EnemyController owner)
    {
        enemy = owner;

        enemy.NavAgent.stoppingDistance = distanceToSand;
        enemy.CombatMovementTimer = 0f;
    }

    public override void Execute()
    {
        
        if (Vector3.Distance(enemy.Target.transform.position, enemy.transform.position) > distanceToSand + adjustDistanceThreshold)
        {
            StartChase();
        }
        if (state == E_AICombatStates.idle)
        {
            if (timer <= 0)
            {
                if (Random.Range(0, 2) == 0)
                {
                    StartIdle();
                }
                else 
                {
                    StartCircling();
                }
            }

        }
        else if (state == E_AICombatStates.Chase)
        {
            if (Vector3.Distance(enemy.Target.transform.position, enemy.transform.position) <= distanceToSand + 0.03f)
            {
                StartIdle();
                return;
            }

            enemy.NavAgent.SetDestination(enemy.Target.transform.position);
        }
        else if (state == E_AICombatStates.Circling)
        {
            if (timer <= 0)
            {
                StartIdle(); 
                
                return;
            }
            
            var vecToTarget = enemy.transform.position - enemy.Target.transform.position;

            var rotatePos = Quaternion.Euler(0, circlingSpeed * circlingDir * Time.deltaTime, 0) * vecToTarget;

            enemy.NavAgent.Move(rotatePos - vecToTarget);

            enemy.transform.rotation = Quaternion.LookRotation(-rotatePos);



        }

        if (timer > 0)
            timer -= Time.deltaTime;

        enemy.CombatMovementTimer += Time.deltaTime;

    }

    void StartChase()
    {
        state = E_AICombatStates.Chase;
        enemy.Animator.SetBool("combatMode", false);
     
    }
    void StartIdle()
    {
        state = E_AICombatStates.idle; 

        timer = Random.Range(idleTimeRange.x, idleTimeRange.y);

        enemy.Animator.SetBool("combatMode", true);
    
    }

    void StartCircling()
    {
        state = E_AICombatStates.Circling;

        timer = Random.Range(circlingTimeRange.x, circlingTimeRange.y);

        circlingDir = Random.Range(0, 2) == 0 ? 1 : -1;

    }

    public override void Exit()
    {
        enemy.CombatMovementTimer = 0f;
    }
}

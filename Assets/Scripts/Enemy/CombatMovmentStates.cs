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
    [SerializeField] float distanceToSand = 3f;
    [SerializeField] float adjustDistanceThreshold = 1f;

    [SerializeField] Vector2 idleTimeRange = new Vector2(2, 5);
    [SerializeField] Vector2 circlingTimeRange = new Vector2(3, 6);

    float circlingSpeed = 20f;
    int circlingDir = 1;

    float timer = 0f;


    public override void Enter(EnemyController owner)
    {
        enemy = owner;

        enemy.NavAgent.stoppingDistance = distanceToSand;
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
                Debug.Log("执行idle");
                return;
            }
            Debug.Log("执行circling");
            transform.RotateAround(enemy.Target.transform.position, Vector3.up, circlingSpeed * circlingDir * Time.deltaTime);
        }

        if (timer > 0)
            timer -= Time.deltaTime;
        
    }

    void StartChase()
    {
        state = E_AICombatStates.Chase;
        enemy.animator.SetBool("combatMode", false);
        enemy.animator.SetBool("C", false);
    }
    void StartIdle()
    {
        state = E_AICombatStates.idle;

        timer = Random.Range(idleTimeRange.x, idleTimeRange.y);//随机值

        enemy.animator.SetBool("combatMode", true);
        enemy.animator.SetBool("C", false);
    }

    void StartCircling()
    {
        state = E_AICombatStates.Circling;
        timer = Random.Range(circlingTimeRange.x, circlingTimeRange.y);//随机值
        Debug.Log($"StartCircling: timer = {timer}");
        circlingDir = Random.Range(0, 2) == 0 ? 1 : -1;

        enemy.animator.SetBool("C", true); 
        enemy.animator.SetFloat("circlingDir", circlingDir);
    }

    public override void Exit()
    {

    }
}

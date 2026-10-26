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

public class CombatMovementStates : State<EnemyController>
{
    EnemyController enemy;
    E_AICombatStates state;
    [SerializeField] float circlingSpeed = 20f;
    [SerializeField] float distanceToSand = 3f;
    [SerializeField] float adjustDistanceThreshold = 1f;
    [SerializeField] Vector2 idleTimeRange = new Vector2(2, 5);
    [SerializeField] Vector2 circlingTimeRange = new Vector2(4, 6);

    [Header("目标倒地后")]
    [Tooltip("目标被自己打死后再继续攻击/追击多少秒（补刀），之后才放弃目标回 Idle。\n" +
             "设很大（如 9999）就等于一直打到玩家复活")]
    [SerializeField] float keepAttackingDeadTargetSeconds = 4f;

    /// <summary>目标已倒地后的计时（保证只补刀一小会儿，不会永远围着尸体转）</summary>
    float deadTargetTimer;

    float timer = 0f;
    int circlingDir = 1;

    public override void Enter(EnemyController owner)
    {
        enemy = owner;

        enemy.NavAgent.stoppingDistance = distanceToSand;
        enemy.CombatMovementTimer = 0f;

        enemy.Animator.SetBool("combatMode", true);

        // 重置内部子状态，确保每次进入时从干净状态开始
        state = E_AICombatStates.idle;
        timer = 0f;
        deadTargetTimer = 0f;
    }

    public override void Execute()
    {
        if(enemy.Target == null)
        {
            enemy.Target = enemy.FindTarget();
            if(enemy.Target == null)
            {
                enemy.ChangeState(E_EnemyState.Idle);
                return;
            }
        }

        if(enemy.Target.Health <= 0)
        {
            // 目标已倒地：不要立刻丢目标发呆（原来这里直接 Target=null → Idle，
            // 表现就是"敌人把玩家打死后杵在原地"）。先继续追着补刀一小会儿。
            deadTargetTimer += Time.deltaTime;
            if (deadTargetTimer >= keepAttackingDeadTargetSeconds)
            {
                enemy.Target = null;
                enemy.ChangeState(E_EnemyState.Idle);
                return;
            }
        }
        else
        {
            deadTargetTimer = 0f;
        }
        
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
            vecToTarget.y = 0f;

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
        
     
    }
    void StartIdle()
    {
        state = E_AICombatStates.idle; 

        timer = Random.Range(idleTimeRange.x, idleTimeRange.y);

    
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

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
    // 环绕速度、站位距离、修正阈值、待机/环绕时间区间、补刀时长都来自 Enemy 表，
    // 由 EnemyController 统一解析后供本状态读取（原 idleTimeRange/circlingTimeRange 已拆成 min/max 两列）。

    /// <summary>目标已倒地后的计时（保证只补刀一小会儿，不会永远围着尸体转）</summary>
    float deadTargetTimer;

    float timer = 0f;
    int circlingDir = 1;

    public override void Enter(EnemyController owner)
    {
        enemy = owner;

        enemy.NavAgent.stoppingDistance = enemy?.Config?.DistanceToSand ?? 0f;
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
            if (deadTargetTimer >= (enemy.Config?.KeepAttackingDeadTargetSeconds ?? 0f))
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
        
        float standDistance = enemy.Config?.DistanceToSand ?? 0f;
        float adjustThreshold = enemy.Config?.AdjustDistanceThreshold ?? 0f;

        if (Vector3.Distance(enemy.Target.transform.position, enemy.transform.position) > standDistance + adjustThreshold)
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
            if (Vector3.Distance(enemy.Target.transform.position, enemy.transform.position) <= standDistance + 0.03f)
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

            var rotatePos = Quaternion.Euler(0, (enemy.Config?.CirclingSpeed ?? 0f) * circlingDir * Time.deltaTime, 0) * vecToTarget;

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

        timer = Random.Range(enemy?.Config?.IdleTimeMin ?? 0f, enemy?.Config?.IdleTimeMax ?? 0f);

    
    }

    void StartCircling()
    {
        state = E_AICombatStates.Circling;

        timer = Random.Range(enemy?.Config?.CirclingTimeMin ?? 0f, enemy?.Config?.CirclingTimeMax ?? 0f);

        circlingDir = Random.Range(0, 2) == 0 ? 1 : -1;

    }

    public override void Exit()
    {
        enemy.CombatMovementTimer = 0f;
    }
}

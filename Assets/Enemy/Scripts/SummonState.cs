using System.Collections;
using UnityEngine.AI;
using UnityEngine;

/// <summary>
/// Boss 召唤小怪状态
/// 播放召唤动画 → 在指定位置生成小怪 → 返回战斗
/// 挂载到 Boss 的 GameObject 上，和其它 State 组件并列
/// </summary>
public class SummonState : State<EnemyController>
{
   private BossController boss;
   private Coroutine summonCoroutine;

    //进入召唤状态 停止导航 播放动画 启动协程
    public override void Enter(EnemyController owner)
    {
        boss = owner as BossController;
        if(boss == null || boss.bossConfig == null || boss.bossConfig.minionPrefab == null)
        {
            //安全回退：配置缺失时直接返回战斗
            owner.ChangeState(E_EnemyState.CombatMovement);
            return;
        }

        //设置战斗模式动画
        boss.Animator.SetBool("combatMode",true);

        // //播放召唤动画
        // if (!string.IsNullOrEmpty(boss.bossConfig.summonAnimName))
        // {
        //     boss.Animator.CrossFadeInFixedTime(boss.bossConfig.summonAnimName,0.2f);
        // }

        //启动召唤携程
        summonCoroutine = boss.StartCoroutine(SummonRoutine());
    }

    //召唤协程 等待动画节奏 生成小怪 回血 返回战斗
    private IEnumerator SummonRoutine()
    {
        BossConfig config = boss.bossConfig;

        // //等待小怪生成延迟
        // yield return new WaitForSeconds(config.spawnDelay);
        //等待一帧让状态稳定
        yield return null;

        //生成小怪
        SpawnMinions(config);

        //召唤时回血
        if(config.healOnSummon > 0f)
        {
            float newHealth = Mathf.Min(boss.Fighter.Health + config.healOnSummon,config.maxHealth);
            boss.Fighter.SetHealth(newHealth);
        }

        // //等待动画剩余时间
        // float remainingTIme = config.summonAnimDuration - config.spawnDelay;
        // if(remainingTIme > 0)
        // {
        //     yield return new WaitForSeconds(remainingTIme);
        // }

        //召唤完成 
        boss.OnSummonComplete();
        boss.ChangeState(E_EnemyState.CombatMovement);
    }

    //在boss周围生成小怪 小怪自动继承boss的目标
    private void SpawnMinions(BossConfig config)
    {
        for(int i = 0 ; i < config.minionCount; i++)
        {
            Vector3 spawnPos = boss.GetSummonPostion(i);

            //确保生成位置在NavMesh上
            NavMeshHit hit;
            if(NavMesh.SamplePosition(spawnPos,out hit, 3f, NavMesh.AllAreas))
            {
                spawnPos = hit.position;
            }

            //实例化小怪
            GameObject minimon = Object.Instantiate(config.minionPrefab,spawnPos,Quaternion.identity);

            //设置小怪的目标为Boss当前的目标(玩家)，直接进入战斗
            EnemyController minionCtrl = minimon.GetComponent<EnemyController>();
            if(minionCtrl != null && boss.Target != null)
            {
                minionCtrl.Target = boss.Target;
                // minionCtrl.ChangeState(E_EnemyState.CombatMovement);
                // EnemyManager.i.AddEnemyInRange(minionCtrl);
                if (!minionCtrl.TargetsInRange.Contains(boss.Target))
                {
                    minionCtrl.TargetsInRange.Add(boss.Target);
                }
    
            }
        }
    }

    //每帧执行 召唤的过程中保持Boss面朝玩家
    public override void Execute()
    {
        if(boss != null && boss.Target != null)
        {
            Vector3 dirToTarget =(boss.Target.transform.position - boss.transform.position).normalized;
            dirToTarget.y = 0;
            if(dirToTarget.sqrMagnitude > 0.01f)
            {
                Quaternion target = Quaternion.LookRotation(dirToTarget);
                boss.transform.rotation = Quaternion.RotateTowards(
                    boss.transform.rotation , target,360f* Time.deltaTime
                );
            }
        }
    }

    //退出召唤状态 清除协程 重置引用
    public override void Exit()
    {
        if(summonCoroutine != null)
        {
            boss.StopCoroutine(summonCoroutine);
            summonCoroutine = null;
        }
        boss = null;
    }
}

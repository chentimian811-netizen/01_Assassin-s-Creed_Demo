using System.Collections;
using UnityEngine;

/// <summary>
/// 受击硬直。自计时，不依赖 OnHitComplete 事件时序。
/// </summary>
public class GettingHitState : State<EnemyController>
{
    // 硬直时长来自 Enemy 表 stun_time
    EnemyController enemy;

    public override void Enter(EnemyController owner)
    {
        enemy = owner;
        StartCoroutine(GoToCombatMovement());
    }

    private IEnumerator GoToCombatMovement()
    {
        // 卡肉期间该等待会被略微拉长，属可接受误差
        yield return new WaitForSeconds(enemy != null && enemy.Config != null ? enemy.Config.StunTime : 0f);
        // 协程可能在切到 Dead 后才恢复（Exit 已 StopAllCoroutines，这里是兜底）
        if (enemy == null || (enemy.Health != null && enemy.Health.IsDead))
            yield break;
        if (!enemy.IsInState(E_EnemyState.Dead))
        {
            enemy.ChangeState(E_EnemyState.CombatMovement);
        }
    }

    public override void Exit()
    {
        // 切走时必须停掉本组件上的协程，否则受击计时会在死亡后仍触发 CombatMovement
        StopAllCoroutines();
    }
}

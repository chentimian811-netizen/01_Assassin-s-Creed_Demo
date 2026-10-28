using System.Collections;
using UnityEngine;

/// <summary>
/// 受击硬直。自计时，不依赖 OnHitComplete 事件时序。
///
/// 时长有两个来源：
///   - 普通受击：Enemy 表 stun_time（本状态默认）；
///   - 弹反硬直：由弹反方按 Parry.enemy_stagger_duration 指定（T2，1.5s）。
/// 两者共用同一个状态与同一套"到点回 CombatMovement"的收尾，只是时长不同，
/// 所以用 RefreshStagger(0 = 用表值) 一个入口同时覆盖。
/// </summary>
public class GettingHitState : State<EnemyController>
{
    EnemyController enemy;

    /// <summary>本次硬直时长（秒）。0 = 用 Enemy 表 stun_time（普通受击）</summary>
    private float m_stunDuration;

    public override void Enter(EnemyController owner)
    {
        enemy = owner;
        // 弹反方可能已经排好了时长（EnemyController.Stagger），取走并消费一次
        RefreshStagger(owner != null ? owner.ConsumePendingStunDuration() : 0f);
    }

    /// <summary>
    /// 重新计时（0 = 用 Enemy 表 stun_time）。
    /// 也从 Enter 里调用：所有"什么时候结束硬直"的逻辑只在这一处。
    /// </summary>
    public void RefreshStagger(float duration)
    {
        m_stunDuration = duration;
        StopAllCoroutines();
        StartCoroutine(GoToCombatMovement());
    }

    private IEnumerator GoToCombatMovement()
    {
        // 卡肉期间该等待会被略微拉长，属可接受误差（硬直是玩法时长，暂停时应一起停）
        yield return new WaitForSeconds(ResolveDuration());
        // 协程可能在切到 Dead 后才恢复（Exit 已 StopAllCoroutines，这里是兜底）
        if (enemy == null || (enemy.Health != null && enemy.Health.IsDead))
            yield break;
        if (!enemy.IsInState(E_EnemyState.Dead))
        {
            enemy.ChangeState(E_EnemyState.CombatMovement);
        }
    }

    /// <summary>本次硬直实际时长：弹反指定值优先，否则回落到 Enemy 表</summary>
    private float ResolveDuration()
    {
        if (m_stunDuration > 0f) return m_stunDuration;
        return enemy != null && enemy.Config != null ? enemy.Config.StunTime : 0f;
    }

    public override void Exit()
    {
        // 切走时必须停掉本组件上的协程，否则受击计时会在死亡后仍触发 CombatMovement
        StopAllCoroutines();
    }
}

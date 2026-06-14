using System.Buffers;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GettingHitState : State<EnemyController>
{
    [SerializeField] float stunnTime = 0.5f;

    EnemyController enemy;

    // 保存委托引用，用于退出时取消订阅，防止多次进入导致回调累积
    private System.Action onHitCompleteHandler;

    public override void Enter(EnemyController owner)
    {
        enemy = owner;
        onHitCompleteHandler = () => StartCoroutine(GoToCombatMovement());
        enemy.Fighter.OnHitComplete += onHitCompleteHandler;
    }

    IEnumerator GoToCombatMovement()
    {
        yield return new WaitForSeconds(stunnTime);
        if (!enemy.IsInState(E_EnemyState.Dead))
        {
            enemy.ChangeState(E_EnemyState.CombatMovement);
        }

    }

    /// <summary>
    /// 退出时取消订阅OnHitComplete事件，防止多次进入GettingHit后回调累积
    /// </summary>
    public override void Exit()
    {
        if (onHitCompleteHandler != null)
        {
            enemy.Fighter.OnHitComplete -= onHitCompleteHandler;
            onHitCompleteHandler = null;
        }
    }
}

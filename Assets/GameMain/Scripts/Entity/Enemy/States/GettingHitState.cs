using System.Collections;
using UnityEngine;

/// <summary>
/// 受击硬直。自计时，不依赖 OnHitComplete 事件时序。
/// </summary>
public class GettingHitState : State<EnemyController>
{
    [SerializeField] float stunnTime = 0.5f;

    EnemyController enemy;

    public override void Enter(EnemyController owner)
    {
        enemy = owner;
        StartCoroutine(GoToCombatMovement());
    }

    private IEnumerator GoToCombatMovement()
    {
        // 卡肉期间该等待会被略微拉长，属可接受误差
        yield return new WaitForSeconds(stunnTime);
        if (!enemy.IsInState(E_EnemyState.Dead))
        {
            enemy.ChangeState(E_EnemyState.CombatMovement);
        }
    }

    public override void Exit() { }
}

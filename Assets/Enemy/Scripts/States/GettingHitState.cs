using System.Buffers;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GettingHitState : State<EnemyController>
{
    [SerializeField] float stunnTime = 0.5f;

    EnemyController enemy;

    public override void Enter(EnemyController owner)
    {
        enemy = owner;
        enemy.Fighter.OnHitComplete += () => StartCoroutine(GoToCombatMovement());
    }

    IEnumerator GoToCombatMovement()
    {
        yield return new WaitForSeconds(stunnTime);
        if (!enemy.IsInState(E_EnemyState.Dead))
        {
            enemy.ChangeState(E_EnemyState.CombatMovement);
        }
        
    }
}

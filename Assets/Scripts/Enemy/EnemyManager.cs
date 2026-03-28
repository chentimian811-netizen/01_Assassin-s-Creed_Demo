using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class EnemyManager : MonoBehaviour
{
    [SerializeField] Vector2 timeRangeBetWeenAttacks = new Vector2(1, 4);
    public static EnemyManager i { get; private set; }

    private void Awake()
    {
        i = this;
    }

    List<EnemyController> enemiesInRange = new List<EnemyController>();

    float notAttackingTimer = 2f;

    public void AddEnemyInRange(EnemyController enemy)
    {
        if (!enemiesInRange.Contains(enemy))
        {
            enemiesInRange.Add(enemy);
        }

    }

    public void RemoveEnemyInRange(EnemyController enemy)
    {
        enemiesInRange.Remove(enemy);
    }

    private void Update()
    {
        if (enemiesInRange.Count == 0) return;

        if (!enemiesInRange.Any(e => e.IsInState(E_EnemyState.Attack)))
        {
            if (notAttackingTimer > 0)
            {
                notAttackingTimer -= Time.deltaTime;
            }


            if (notAttackingTimer <= 0)
            {
                var attackingEnemy = SelectEnemyForAttack();
                attackingEnemy.ChangeState(E_EnemyState.Attack);
                notAttackingTimer = Random.Range(timeRangeBetWeenAttacks.x, timeRangeBetWeenAttacks.y);
            }
        }
    }


    EnemyController SelectEnemyForAttack()
    {
        return enemiesInRange.OrderByDescending(e => e.CombatMovementTimer).FirstOrDefault();
    }


    public EnemyController GetAttackingEnemy()
    {
        return enemiesInRange.FirstOrDefault(e => e.IsInState(E_EnemyState.Attack));
    }
}
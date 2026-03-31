using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class EnemyManager : MonoBehaviour
{
    [SerializeField] Vector2 timeRangeBetWeenAttacks = new Vector2(1, 4);

    [SerializeField] PlayerController Player;
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

    float timer = 0f;

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
                if (attackingEnemy != null)
                {
                    attackingEnemy.ChangeState(E_EnemyState.Attack);
                    notAttackingTimer = Random.Range(timeRangeBetWeenAttacks.x, timeRangeBetWeenAttacks.y);
                }
            }
        }

        if(timer >= 0.1f)
        {
            timer = 0f;
            var closestEnemy = GetClosesEnemyToPlayerDir();

            if (closestEnemy != null && closestEnemy != Player.tatgetEnemy)
            {
                var prevEnemy = Player.tatgetEnemy;
                Player.tatgetEnemy = closestEnemy;

                Player?.tatgetEnemy?.MeshHightlighter.HighlightMesh(true);
                prevEnemy?.MeshHightlighter?.HighlightMesh(false);
            }

        }
        timer += Time.deltaTime;
    }


    EnemyController SelectEnemyForAttack()
    {
        return enemiesInRange.OrderByDescending(e => e.CombatMovementTimer).FirstOrDefault(e => e.Target != null);
    }


    public EnemyController GetAttackingEnemy()
    {
        return enemiesInRange.FirstOrDefault(e => e.IsInState(E_EnemyState.Attack));
    }

    public EnemyController GetClosesEnemyToPlayerDir()
    {
        var targetingDir = Player.GetTargetingDir();
        
        float minDistance = Mathf.Infinity;
        EnemyController closestEnemy = null;

        foreach (var enemy in enemiesInRange)
        {
            var vecToEnemy = enemy.transform.position - Player.transform.position;
            vecToEnemy.y = 0;

            float angle = Vector3.Angle(targetingDir,vecToEnemy);
            float distance = vecToEnemy.magnitude * Mathf.Sin(angle * Mathf.Deg2Rad);

            if(distance < minDistance)
            {
                minDistance = distance;
                closestEnemy = enemy;
            }

        }
        return closestEnemy;
    }
}
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
/// <summary>
/// 敌人管理器（单例）
/// 职责：管理范围内敌人列表、定时调度攻击、最近敌人查找、锁定目标同步
/// </summary>
public class EnemyManager : MonoBehaviour
{
    [SerializeField] Vector2 timeRangeBetWeenAttacks = new Vector2(1, 4);

    [SerializeField] PlayerController Player;

    [field: SerializeField] 
    public LayerMask EnemyLayer { get; private set; }

    public static EnemyManager i { get; private set; }

    private void Awake()
    {
        i = this;
    }

    List<EnemyController> enemiesInRange = new List<EnemyController>();
    float notAttackingTimer = 2f;
    float timer = 0f;

    /// <summary>
    /// 将敌人加入攻击范围列表（去重）
    /// </summary>
    /// <param name="enemy"></param>
    public void AddEnemyInRange(EnemyController enemy)
    {
        if (!enemiesInRange.Contains(enemy))
        {
            enemiesInRange.Add(enemy);
        }

    }
    /// <summary>
    /// 将敌人移出攻击范围列表，并同步锁定目标
    /// </summary>
    /// <param name="enemy"></param>
    public void RemoveEnemyInRange(EnemyController enemy)
    {
        enemiesInRange.Remove(enemy);

        if (enemy == Player.TargetEnemy)
        {
            enemy.MeshHightlighter?.HighlightMesh(false);

            if (Player.IsLocking)
            {
                EnemyController next = GetClosesEnemyToPlayerDir();
                if (next != null)
                {
                    Player.TargetEnemy = next;
                    next.MeshHightlighter?.HighlightMesh(true);
                }
                else
                {
                    Player.ForceUnlock();
                }
            }
            else
            {
                Player.TargetEnemy = GetClosesEnemyToPlayerDir();
            }
        }
    }

    /// <summary>
    /// 每帧更新 调度敌人攻击 刷新最近的锁定目标
    /// </summary>
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

        //非锁定时刷新最近的目标
        if (Player.IsLocking) return;

        if(timer >= 0.1f)
        {
            timer = 0f;
            var closestEnemy = GetClosesEnemyToPlayerDir();

            if (closestEnemy != null && closestEnemy != Player.TargetEnemy)
            {
                Player.TargetEnemy = closestEnemy;
            }

        }
        timer += Time.deltaTime;
    }

    /// <summary>
    /// 选择一个敌人发起攻击
    /// </summary>
    /// <returns></returns>
    EnemyController SelectEnemyForAttack()
    {
        return enemiesInRange.OrderByDescending(e => e.CombatMovementTimer).FirstOrDefault(e => e.Target != null && e.IsInState(E_EnemyState.CombatMovement));
    }

    /// <summary>
    /// 获取当前在攻击的敌人（用于反击判定）
    /// </summary>
    /// <returns></returns>
    public EnemyController GetAttackingEnemy()
    {
        return enemiesInRange.FirstOrDefault(e => e.IsInState(E_EnemyState.Attack));
    }

    /// <summary>
    /// 获取玩家视角方向最近的敌人
    /// </summary>
    /// <returns></returns>
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
            float perpDist = vecToEnemy.magnitude * Mathf.Sin(angle * Mathf.Deg2Rad);
            float paraDist = vecToEnemy.magnitude * Mathf.Cos(angle * Mathf.Deg2Rad);

            float score = perpDist * 10 + paraDist;

            if (score < minDistance)
            {
                minDistance = score;
                closestEnemy = enemy;
            }

        }
        return closestEnemy;
    }
}
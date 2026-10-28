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
    // 出手间隔区间/初始倒计时/目标刷新周期都来自 EnemyGlobal 单行表（Gameplay.xlsx）；
    // 运行时倒计时仍是组件状态，不回写表格。
    private EnemyGlobal m_configRow;

    [SerializeField] PlayerController Player;

    [field: SerializeField] 
    public LayerMask EnemyLayer { get; private set; }

    public static EnemyManager i { get; private set; }

    private void Awake()
    {
        i = this;
        m_configRow = DataRepository.GetEnemyGlobal();
        notAttackingTimer = m_configRow != null ? m_configRow.InitialAttackDelay : 0f;
    }

    List<EnemyController> enemiesInRange = new List<EnemyController>();
    float notAttackingTimer;
    float timer = 0f;

    /// <summary>
    /// 场景里的**全部**敌人（不是交战列表）。敌人重生时按它全量复位。
    /// 由 EnemyController.Start 登记 —— 本项目敌人是场景摆放，没有运行时生成。
    /// </summary>
    readonly List<EnemyController> m_AllEnemies = new List<EnemyController>();

    /// <summary>登记一只敌人（EnemyController.Start 调用，幂等）</summary>
    public void RegisterEnemy(EnemyController enemy)
    {
        if (enemy == null || m_AllEnemies.Contains(enemy)) return;
        m_AllEnemies.Add(enemy);
    }

    /// <summary>注销（敌人被销毁或移出关卡时调用）</summary>
    public void UnregisterEnemy(EnemyController enemy)
    {
        if (enemy == null) return;
        m_AllEnemies.Remove(enemy);
    }

    /// <summary>
    /// 全部敌人复位到初始落点并复活（篝火休息 / 玩家死亡回篝火时调用）。
    ///
    /// 【为什么要清空交战列表】
    ///   被回收的敌人还留在 enemiesInRange 里，复位那一帧的 SelectEnemyForAttack /
    ///   GetClosesEnemyToPlayerDir 会挑到「位置在旧处、血量已归零」的对象。
    ///   清空后由各敌人的 VisionSensor 在玩家重新进入视野时自己登记回来。
    ///
    /// 【为什么要清玩家的目标】
    ///   硬锁/软锁还指着旧目标时，相机与高亮会一直盯着一个刚隐藏的敌人；
    ///   清掉后下一次周期刷新会重新挑最近的一个。
    /// </summary>
    public void RespawnAll()
    {
        enemiesInRange.Clear();

        if (Player != null)
        {
            Player.TargetEnemy = null;
            if (Player.IsLocking) Player.ForceUnlock();
        }

        int respawned = 0;
        for (int i = 0; i < m_AllEnemies.Count; i++)
        {
            EnemyController enemy = m_AllEnemies[i];
            if (enemy == null) continue;
            enemy.ResetToSpawn();
            respawned++;
        }

        // 出手调度重新起算：刚重生就排上一刀会显得很突兀
        notAttackingTimer = m_configRow != null ? m_configRow.InitialAttackDelay : 0f;
        timer = 0f;

        Debug.Log($"[EnemyManager] 敌人已重生：{respawned} 只");
    }

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
                // 先在旧锁定语境下算出候选，再清掉旧目标，最后走完整锁定流程。
                // 只写 Player.TargetEnemy 的话，PlayerLockOn.LockedEnemy 与相机仍会停在旧敌人上。
                EnemyController next = GetClosesEnemyToPlayerDir();
                Player.ForceUnlock();
                if (next != null)
                    Player.playerLockOn.LockEnemy(next);
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
                    notAttackingTimer = Random.Range(m_configRow.AttackIntervalMin, m_configRow.AttackIntervalMax);
                }
            }
        }

        //非锁定时刷新最近的目标
        if (Player.IsLocking) return;

        if(timer >= (m_configRow != null ? m_configRow.TargetRefreshInterval : 0f))
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
        return enemiesInRange
            .Where(e => e != null && e.Health != null && !e.Health.IsDead)
            .OrderByDescending(e => e.CombatMovementTimer)
            .FirstOrDefault(e => e.Target != null && e.IsInState(E_EnemyState.CombatMovement));
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
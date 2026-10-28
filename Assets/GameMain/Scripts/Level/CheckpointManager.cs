using UnityEngine;

/// <summary>
/// 重生点管理：记住「当前该在哪复活」，并负责把玩家真正放回去。
///
/// 【为什么是懒加载 C# 单例，不挂场景】
///   它与 CurrencyManager / PackageLocalData 同类：只持有数据和一个复活入口，
///   不需要 Update，也没有场景引用。挂成 MonoBehaviour 还得 DontDestroyOnLoad，
///   等于和 GameManager 重复一套 —— 而它连 GameObject 都不需要。
///
/// 【为什么复活必须复用 PlayerController.ResetForNewRun】
///   单场景阶段不能重载场景，复活 = Health 复活 + 落点 + 清锁定/输入 +
///   战斗层切回 Empty，这四件事 ResetForNewRun 已经做过一次（返回主菜单 → 再开始）。
///   这里只把「落点」换成篝火落点，绝不另写一套 —— 两条复活路径一旦分叉，
///   迟早会有一边漏掉某一步（最典型的是漏掉 ResetCombatPose，复活后一直保持倒地姿势）。
///
/// 【尚未做（§14.3 C2 / C4 / 第 5 步）】
///   掉魂/拾魂、篝火点亮与捷径的 PlayerPrefs 持久化。当前重生点只存在内存里，
///   退出 Play 即丢失（重新进 Play 相当于新开局，落点回到玩家出生点）。
/// </summary>
public class CheckpointManager
{
    private static CheckpointManager s_Instance;

    public static CheckpointManager Instance => s_Instance ?? (s_Instance = new CheckpointManager());

    private string m_BonfireId;
    private Vector3 m_Position;
    private Quaternion m_Rotation;
    private bool m_HasCheckpoint;

    /// <summary>是否已点亮过篝火（没点亮过就只能回出生点）</summary>
    public bool HasCheckpoint => m_HasCheckpoint;

    /// <summary>当前重生点所属篝火 ID，未登记时为 null</summary>
    public string BonfireId => m_BonfireId;

    public Vector3 RespawnPosition => m_Position;
    public Quaternion RespawnRotation => m_Rotation;

    /// <summary>
    /// 构造时读一次盘（懒加载单例，第一次访问时执行）。
    /// 读不到就保持「没有重生点」——回出生点，与第 2 步之前的行完全一致。
    /// </summary>
    private CheckpointManager()
    {
        if (LevelPersistence.TryLoadCheckpoint(out string bonfireId, out Vector3 position, out Quaternion rotation))
        {
            m_BonfireId = bonfireId;
            m_Position = position;
            m_Rotation = rotation;
            m_HasCheckpoint = true;
            Debug.Log($"[Checkpoint] 已从存档恢复重生点：{bonfireId} @ {position}");
        }
    }

    /// <summary>登记重生点。由 Bonfire.Interact 调用（点燃/休息都算一次登记），并立刻写盘</summary>
    public void SetCheckpoint(string bonfireId, Vector3 position, Quaternion rotation)
    {
        m_BonfireId = bonfireId;
        m_Position = position;
        m_Rotation = rotation;
        m_HasCheckpoint = true;

        LevelPersistence.SaveCheckpoint(bonfireId, position, rotation);

        Debug.Log($"[Checkpoint] 重生点已登记并写盘：{bonfireId} @ {position}");
    }

    /// <summary>
    /// 「新开一局」（主菜单 → 开始游戏）时把玩家放到重生点；没有存档就回出生点。
    ///
    /// 【为什么单独一个方法，而不是复用 RespawnPlayer】
    ///   RespawnPlayer 是死亡出口，它还要复活敌人、发 OnPlayerRevive、关死亡界面；
    ///   开局只需要「落点」这一件事（敌人由 ProcedureGame 自己 ResetAggro）。
    ///   两者共用 PlayerController.ResetForNewRun，落点逻辑只有一份。
    /// </summary>
    public void PlacePlayerAtCheckpoint(PlayerController player)
    {
        if (player == null) return;

        if (m_HasCheckpoint)
        {
            player.ResetForNewRun(m_Position, m_Rotation);
            Debug.Log($"[Checkpoint] 新局从篝火开始：{m_BonfireId} @ {m_Position}");
        }
        else
        {
            player.ResetForNewRun();
        }
    }

    /// <summary>
    /// 在篝火复活玩家（没有重生点时回出生点）。
    /// 返回 false 表示场景里找不到玩家 —— 调用方应保留死亡界面，别把它关掉。
    /// </summary>
    public bool RespawnPlayer()
    {
        PlayerController player = FindPlayer();
        if (player == null)
        {
            Debug.LogError("[Checkpoint] 场景里找不到玩家（Tag=Player），复活失败");
            return false;
        }

        if (m_HasCheckpoint)
        {
            player.ResetForNewRun(m_Position, m_Rotation);
        }
        else
        {
            // 一次篝火都没点就死了：回玩家出生点，行为与「返回主菜单 → 再开始」一致
            Debug.LogWarning("[Checkpoint] 还没有点亮过篝火，回出生点复活");
            player.ResetForNewRun();
        }

        // 耐力不归 Health 管：ResetForNewRun 只回血，这里必须补上耐力，
        // 否则「死亡 → 篝火复活」会带着死亡瞬间的空耐力继续，和篝火休息的体感不一致
        player.playerStamina?.RestoreFull();

        // 敌人重生：死了再回来，关卡里的敌人也一起复位（魂系行为）。
        // 不能只清仇恨 —— 那样跑回原处只剩一地空地（Phase 4 第 3 步）。
        if (EnemyManager.i != null)
        {
            EnemyManager.i.RespawnAll();
        }
        else
        {
            // 没有 EnemyManager 的调试场景：至少清仇恨，别让敌人一路追到篝火
            ResetEnemiesAggro();
        }

        GameEvents.RaisePlayerRevive();

        Debug.Log(m_HasCheckpoint
            ? $"[Checkpoint] 已在 {m_BonfireId} 复活"
            : "[Checkpoint] 已在出生点复活");

        return true;
    }

    /// <summary>
    /// 只清仇恨、不复位（没有 EnemyManager 时的兜底）。
    ///
    /// 正常路径已改成 <see cref="EnemyManager.RespawnAll"/>（连血量/位置一起复位），
    /// 这里保留给「场景里没摆 EnemyManager」的调试场景：杀死玩家的那只敌人 Target 仍指向玩家，
    /// 不清的话会一路追到篝火。
    /// </summary>
    private static void ResetEnemiesAggro()
    {
        EnemyController[] enemies = Object.FindObjectsOfType<EnemyController>(true);
        for (int i = 0; i < enemies.Length; i++)
        {
            if (enemies[i] != null) enemies[i].ResetAggro();
        }
    }

    private static PlayerController FindPlayer()
    {
        GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
        return playerObject != null ? playerObject.GetComponent<PlayerController>() : null;
    }
}

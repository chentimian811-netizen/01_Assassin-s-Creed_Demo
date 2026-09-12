using System;
using UnityEngine;

/// <summary>
/// 玩法事件总线（轻量静态）。只服务玩法/任务/关卡，不替代 GF 流程事件。
/// 订阅方：任务、HUD、篝火、雾门、音效。发布方：战斗与关卡组件。
///
/// 【强制约定】
///   1. 订阅必须在 OnEnable / OnOpen，退订必须在 OnDisable / OnClose —— 一一对应；
///   2. 禁止用 lambda / 匿名方法订阅（无法退订，必泄漏）；
///   3. 静态事件跨 Play 会话存活（关闭 Domain Reload 时尤其明显），
///      故必须有 ResetStatics 在进入 Play 前清空全部订阅者。
/// </summary>
public static class GameEvents
{
    // ----- 玩家 -----
    public static event Action OnPlayerDeath;
    public static event Action OnPlayerRevive;
    public static event Action<float, float> OnPlayerHealthChanged;  // cur, max
    public static event Action<float, float> OnPlayerStaminaChanged; // cur, max

    // ----- 受伤（所有单位） -----
    /// <summary>任意单位受伤的广播（不含"谁被打"），用于受击音效/飘字/镜头</summary>
    public static event Action<DamageInfo> OnDamageTaken;

    /// <summary>
    /// ⚠️ 单个单位受伤的定向通知：(victimUnitId, info)。
    /// 【为什么必须要 victimUnitId】DamageInfo 里只有 Source（攻击者），没有受害者。
    /// 若只广播 OnDamageTaken，所有敌人都会以为"我被打/我要死"，4 只敌人会一起倒下。
    /// 订阅方应先比对 victimUnitId == 自己的 Health.UnitId。
    /// 仇恨目标从 info.Source 取，不再需要 MeleeFighter.OnGotHit。
    /// </summary>
    public static event Action<string, DamageInfo> OnUnitDamaged;

    /// <summary>
    /// 受击硬直表现结束。替代原 MeleeFighter.OnHitComplete
    /// （GettingHitState 依赖它才会回到战斗状态；v2.5 后 FSM 改自计时，事件保留供 UI/音效）。
    /// 参数 unitId 同样用于过滤"是不是我"。
    /// </summary>
    public static event Action<string> OnHitReactionComplete;

    // ----- 敌人 / Boss -----
    /// <summary>敌人死亡。⚠️ 参数必须是**全局唯一 ID**（Health.UnitId），
    /// 不能用物体名 —— 4 只同名的 "Enemy" 会互相误触发死亡演出</summary>
    public static event Action<string> OnEnemyKilled;             // unitId
    public static event Action<string> OnBossKilled;              // bossId
    public static event Action<string, int> OnBossPhaseChanged;   // bossId, phaseIndex

    // ----- 弹反 / 战斗反馈 -----
    public static event Action OnParrySuccess;
    public static event Action OnParryFailed;

    // ----- 关卡 / 死亡惩罚 -----
    public static event Action<string> OnBonfireLit;              // bonfireId
    public static event Action<int> OnSoulsDropped;               // amount
    public static event Action<int> OnSoulsPicked;                // amount
    public static event Action<string> OnShortcutUnlocked;
    public static event Action<string> OnFogWallEntered;
    public static event Action<string> OnFogWallCleared;

    // ----- 任务（P7 再挂 UI） -----
    public static event Action<int> OnQuestCompleted;
    public static event Action<int, int> OnQuestObjectiveUpdated; // questId, objectiveId

    // ==================== 发布辅助 ====================
    public static void RaisePlayerDeath() => OnPlayerDeath?.Invoke();
    public static void RaisePlayerRevive() => OnPlayerRevive?.Invoke();
    public static void RaisePlayerHealthChanged(float c, float m) => OnPlayerHealthChanged?.Invoke(c, m);
    public static void RaisePlayerStaminaChanged(float c, float m) => OnPlayerStaminaChanged?.Invoke(c, m);
    public static void RaiseDamageTaken(in DamageInfo info) => OnDamageTaken?.Invoke(info);
    public static void RaiseUnitDamaged(string unitId, in DamageInfo info) => OnUnitDamaged?.Invoke(unitId, info);
    public static void RaiseHitReactionComplete(string unitId) => OnHitReactionComplete?.Invoke(unitId);
    public static void RaiseEnemyKilled(string id) => OnEnemyKilled?.Invoke(id);
    public static void RaiseBossKilled(string id) => OnBossKilled?.Invoke(id);
    public static void RaiseBossPhaseChanged(string id, int phase) => OnBossPhaseChanged?.Invoke(id, phase);
    public static void RaiseParrySuccess() => OnParrySuccess?.Invoke();
    public static void RaiseParryFailed() => OnParryFailed?.Invoke();
    public static void RaiseBonfireLit(string id) => OnBonfireLit?.Invoke(id);
    public static void RaiseSoulsDropped(int amount) => OnSoulsDropped?.Invoke(amount);
    public static void RaiseSoulsPicked(int amount) => OnSoulsPicked?.Invoke(amount);
    public static void RaiseShortcutUnlocked(string id) => OnShortcutUnlocked?.Invoke(id);
    public static void RaiseFogWallEntered(string id) => OnFogWallEntered?.Invoke(id);
    public static void RaiseFogWallCleared(string id) => OnFogWallCleared?.Invoke(id);
    public static void RaiseQuestCompleted(int questId) => OnQuestCompleted?.Invoke(questId);
    public static void RaiseQuestObjectiveUpdated(int questId, int objectiveId)
        => OnQuestObjectiveUpdated?.Invoke(questId, objectiveId);

    // ==================== 静态状态清理 ====================
    /// <summary>
    /// 进入 Play 前清空全部订阅者。
    /// 关闭 Domain Reload 时，静态事件会带着上一次 Play 的"僵尸订阅者"进入下一次，
    /// 表现为 MissingReferenceException 或同一事件被调用两次。
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        OnPlayerDeath = null;
        OnPlayerRevive = null;
        OnPlayerHealthChanged = null;
        OnPlayerStaminaChanged = null;
        OnDamageTaken = null;
        OnUnitDamaged = null;
        OnHitReactionComplete = null;
        OnEnemyKilled = null;
        OnBossKilled = null;
        OnBossPhaseChanged = null;
        OnParrySuccess = null;
        OnParryFailed = null;
        OnBonfireLit = null;
        OnSoulsDropped = null;
        OnSoulsPicked = null;
        OnShortcutUnlocked = null;
        OnFogWallEntered = null;
        OnFogWallCleared = null;
        OnQuestCompleted = null;
        OnQuestObjectiveUpdated = null;
    }
}

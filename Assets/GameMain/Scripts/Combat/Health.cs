using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 血量与无敌帧的唯一承载组件。实现 IDamageable。
///
/// 为什么单独成组件（而不是继续放在 MeleeFighter 里）：
///   1. MeleeFighter 的职责是"打人"，被打是另一件事；
///   2. 工程里有多个文件在读血量，需要一个稳定的读取出口；
///   3. 无敌帧来源不止一种（翻滚/处决/反击），需要按来源管理。
///
/// 无敌用"来源集合"而非布尔：翻滚 i-Frame 结束时不会误清掉处决演出的无敌。
/// </summary>
[DisallowMultipleComponent]
public class Health : MonoBehaviour, IDamageable
{
    [Header("血量")]
    [Tooltip("最大血量。修复了原 MeleeFighter 没有 MaxHealth、clamp 上限用当前血的 bug")]
    [SerializeField] private float maxHealth = 25f;

    [Tooltip("复活/篝火后是否回满")]
    [SerializeField] private bool refillOnEnable = true;

    [Header("标识")]
    [Tooltip("单位唯一标识。留空则 Awake 时自动用 GetInstanceID() 生成。\n" +
             "⚠️ 不要用物体名当 ID：同名的敌人会互相误触发死亡事件")]
    [SerializeField] private string unitId;

    private float currentHealth;
    private bool isDead;
    private GameObject lastAttacker;

    /// <summary>单位唯一标识。全局唯一，供 OnEnemyKilled / OnBossKilled 使用</summary>
    public string UnitId => unitId;

    // 无敌来源集合：任何一个来源存在即无敌
    private readonly HashSet<string> invulnerableReasons = new HashSet<string>();

    // 已发布的血量快照，用于"只在变化时发事件"
    private float lastPublishedHealth = float.NaN;

    public Transform Transform => transform;
    public float CurrentHealth => currentHealth;
    public float MaxHealth => maxHealth;
    public bool IsInvulnerable => invulnerableReasons.Count > 0;
    public bool IsDead => isDead;

    /// <summary>最后攻击者，供死亡演出（朝向攻击者倒下）使用</summary>
    public GameObject LastAttacker => lastAttacker;

    /// <summary>是否玩家（按 tag 判定一次并缓存，避免每帧 CompareTag）</summary>
    private bool isPlayer;

    private void Awake()
    {
        isPlayer = CompareTag("Player");

        // 自动生成唯一 ID：不用物体名，避免同名敌人互相误触发
        if (string.IsNullOrEmpty(unitId))
        {
            unitId = isPlayer ? "player" : $"unit_{GetInstanceID()}";
        }

        if (refillOnEnable) ResetToFull();
    }

    private void OnEnable()
    {
        // 重置"已发布"快照，确保重新启用后 HUD 会收到一次刷新
        lastPublishedHealth = float.NaN;
    }

    /// <summary>回满并清除死亡/无敌状态（篝火、重生、复活用）</summary>
    public void ResetToFull()
    {
        currentHealth = maxHealth;
        isDead = false;
        invulnerableReasons.Clear();
    }

    public void TakeDamage(in DamageInfo info)
    {
        if (isDead) return;
        if (IsInvulnerable) return;
        if (info.Amount <= 0f) return;

        lastAttacker = info.Source;
        currentHealth = Mathf.Clamp(currentHealth - info.Amount, 0f, maxHealth);

        // ⚠️ 致死必须【先置 isDead，再发事件】（v2.5 修复）：
        // EnemyController.HandleUnitDamaged 靠 IsDead 决定进 Dead 还是 GettingHit。
        // 若先发事件再置死，致死一击会被当成普通受击 —— 敌人 0 血回 CombatMovement，永远不死。
        bool lethal = currentHealth <= 0f;
        if (lethal) isDead = true;

        PublishHealthIfChanged();
        GameEvents.RaiseUnitDamaged(unitId, info);   // 定向：受击表现 / 仇恨 / FSM 转换
        GameEvents.RaiseDamageTaken(info);           // 广播：音效 / 飘字 / 统计

        if (lethal) Die();
    }

    public void Heal(float amount)
    {
        if (isDead || amount <= 0f) return;
        currentHealth = Mathf.Clamp(currentHealth + amount, 0f, maxHealth);
        PublishHealthIfChanged();
    }

    public void SetInvulnerable(string reason, bool value)
    {
        if (string.IsNullOrEmpty(reason))
        {
            Debug.LogWarning("[Health] SetInvulnerable 必须带 reason，否则无法按来源管理", this);
            return;
        }

        if (value) invulnerableReasons.Add(reason);
        else invulnerableReasons.Remove(reason);
    }

    private void Die()
    {
        // isDead 已在 TakeDamage 提前置位；此处幂等保障 + 发死亡事件
        isDead = true;
        currentHealth = 0f;
        PublishHealthIfChanged();

        if (isPlayer) GameEvents.RaisePlayerDeath();
        else GameEvents.RaiseEnemyKilled(unitId);
    }

    /// <summary>只在血量真变化时发事件，避免空转导致 UI 每帧重绘与 GC</summary>
    private void PublishHealthIfChanged()
    {
        if (Mathf.Approximately(lastPublishedHealth, currentHealth)) return;
        lastPublishedHealth = currentHealth;

        if (isPlayer) GameEvents.RaisePlayerHealthChanged(currentHealth, maxHealth);
    }

    /// <summary>调试用：Inspector 右键可直接扣血验证</summary>
    // 工程内有 UI/ContextMenu 类，必须写全名避免与 UnityEngine.ContextMenu 冲突
    [UnityEngine.ContextMenu("Debug/Damage 5")]
    private void DebugDamage5()
    {
        var info = DamageInfo.Create(5f, gameObject, transform.position,
            E_DamageSource.Scripted, false, "debug");
        TakeDamage(info);
    }
}

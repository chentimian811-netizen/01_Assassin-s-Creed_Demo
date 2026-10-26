using UnityEngine;

/// <summary>
/// Boss 阶段控制器。P6 实装：订阅血量事件，在阈值切换招式表并发 OnBossPhaseChanged。
/// </summary>
public class BossPhaseController : MonoBehaviour
{
    [Tooltip("Boss 配置/事件标识，对应 BossPhase 表的 boss_id")]
    [SerializeField] private string bossId = "boss_01";

    private Health health;
    private int currentPhase = -1;

    public string BossId => bossId;
    public int CurrentPhase => currentPhase;

    private void Awake()
    {
        health = GetComponent<Health>();
    }

    private void OnEnable()
    {
        GameEvents.OnUnitDamaged += HandleUnitDamaged;
    }

    private void OnDisable()
    {
        GameEvents.OnUnitDamaged -= HandleUnitDamaged;
    }

    private void HandleUnitDamaged(string victimUnitId, DamageInfo info)
    {
        if (health == null || victimUnitId != health.UnitId) return;
        EvaluatePhase();
    }

    private void EvaluatePhase()
    {
        if (health == null) return;

        // 阶段阈值来自 BossPhase 表（按 boss_id 分组、phase_index 升序）
        var phases = DataRepository.GetBossPhases(bossId);
        if (phases == null || phases.Count == 0) return;

        float ratio = health.MaxHealth > 0f ? health.CurrentHealth / health.MaxHealth : 0f;

        // 反向遍历：先命中序号最高的阶段
        for (int i = phases.Count - 1; i >= 0; i--)
        {
            if (ratio <= phases[i].HealthThreshold)
            {
                if (currentPhase != phases[i].PhaseIndex)
                {
                    currentPhase = phases[i].PhaseIndex;
                    GameEvents.RaiseBossPhaseChanged(bossId, currentPhase);
                }
                return;
            }
        }
    }
}

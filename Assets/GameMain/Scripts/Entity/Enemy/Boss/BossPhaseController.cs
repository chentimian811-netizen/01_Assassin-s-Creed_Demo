using UnityEngine;

/// <summary>
/// Boss 阶段控制器。P6 实装：订阅血量事件，在阈值切换招式表并发 OnBossPhaseChanged。
/// </summary>
public class BossPhaseController : MonoBehaviour
{
    [SerializeField] private string bossId = "boss_01";
    [SerializeField] private BossPhaseConfig[] phases;

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
        if (health == null || phases == null || phases.Length == 0) return;
        float ratio = health.MaxHealth > 0f ? health.CurrentHealth / health.MaxHealth : 0f;

        for (int i = phases.Length - 1; i >= 0; i--)
        {
            if (phases[i] != null && ratio <= phases[i].healthThreshold)
            {
                if (currentPhase != phases[i].phaseIndex)
                {
                    currentPhase = phases[i].phaseIndex;
                    GameEvents.RaiseBossPhaseChanged(bossId, currentPhase);
                }
                return;
            }
        }
    }
}

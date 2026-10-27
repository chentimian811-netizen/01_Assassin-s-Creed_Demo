using UnityEngine;

/// <summary>
/// 玩家资源条面板（左上）：HP / MP / 耐力。
/// 挂在 MainHUDForm 的 PlayerStatusRoot 上，随 HUD 开关自动订阅/退订。
///
/// 设计：UI 只吃事件，不轮询玩法组件；打开时主动拉一次初值 ——
/// Health / PlayerStamina 在数值没变化时不会发事件，光等事件会一直显示默认值。
/// </summary>
public class PlayerStatusPanel : MonoBehaviour
{
    [Header("三条资源条")]
    [SerializeField] private HUDBarView healthBar;
    [SerializeField] private HUDBarView manaBar;
    [SerializeField] private HUDBarView staminaBar;

    [Header("绑定")]
    [Tooltip("找不到玩家时的重试间隔（秒）")]
    [SerializeField] private float bindRetryInterval = 0.5f;

    private Health m_health;
    private PlayerStamina m_stamina;
    private PlayerMana m_mana;

    private bool m_bound;
    private float m_nextBindTime;

    private void OnEnable()
    {
        GameEvents.OnPlayerHealthChanged += HandleHealthChanged;
        GameEvents.OnPlayerStaminaChanged += HandleStaminaChanged;
        GameEvents.OnPlayerManaChanged += HandleManaChanged;

        m_nextBindTime = 0f;
        TryBind();
    }

    private void OnDisable()
    {
        GameEvents.OnPlayerHealthChanged -= HandleHealthChanged;
        GameEvents.OnPlayerStaminaChanged -= HandleStaminaChanged;
        GameEvents.OnPlayerManaChanged -= HandleManaChanged;

        m_bound = false;
    }

    private void Update()
    {
        if (m_bound) return;
        if (Time.unscaledTime < m_nextBindTime) return;

        m_nextBindTime = Time.unscaledTime + Mathf.Max(0.1f, bindRetryInterval);
        TryBind();
    }

    /// <summary>
    /// 找玩家并拉一次初值。HUD 可能比玩家先创建，所以找不到要重试，
    /// 不假设 ProcedureGame 的初始化顺序。
    /// </summary>
    private void TryBind()
    {
        if (m_bound) return;

        PlayerController player = FindObjectOfType<PlayerController>(true);
        if (player == null) return;

        m_health = player.GetComponent<Health>();
        m_stamina = player.GetComponent<PlayerStamina>();
        m_mana = player.GetComponent<PlayerMana>();

        if (m_health == null && m_stamina == null && m_mana == null) return;

        m_bound = true;
        RefreshFromPlayer();
    }

    private void RefreshFromPlayer()
    {
        if (m_health != null && healthBar != null)
        {
            healthBar.SetValue(m_health.CurrentHealth, m_health.MaxHealth, true);
        }

        if (m_stamina != null && staminaBar != null)
        {
            staminaBar.SetValue(m_stamina.Current, m_stamina.Max, true);
        }

        if (manaBar == null) return;

        if (m_mana != null) manaBar.SetValue(m_mana.Current, m_mana.Max, true);
        else manaBar.SetValue(0f, 1f, true);   // 玩家没挂 PlayerMana 时先显示空条，不假装有数值
    }

    private void HandleHealthChanged(float current, float max)
    {
        if (healthBar != null) healthBar.SetValue(current, max);
    }

    private void HandleStaminaChanged(float current, float max)
    {
        if (staminaBar != null) staminaBar.SetValue(current, max);
    }

    private void HandleManaChanged(float current, float max)
    {
        if (manaBar != null) manaBar.SetValue(current, max);
    }
}

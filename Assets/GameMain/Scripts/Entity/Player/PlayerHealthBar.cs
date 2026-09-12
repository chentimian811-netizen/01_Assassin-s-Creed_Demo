using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 玩家血条UI控制器（带平滑颜色过渡）
/// 订阅 GameEvents.OnPlayerHealthChanged，不再依赖 MeleeFighter.OnGotHit。
/// </summary>
public class PlayerHealthBar : MonoBehaviour
{
    [Header("UI引用")]
    [Tooltip("血条Slider组件")]
    [SerializeField] private Slider healthSlider;

    [Tooltip("血条填充图片")]
    [SerializeField] private Image fillImage;

    [Header("颜色设置")]
    [SerializeField] private Color highHealthColor = new Color(0.2f,0.8f,0.2f);
    [SerializeField] private Color midHealthColor = new Color(0.9f,0.9f,0.1f);
    [SerializeField] private Color lowHealthColor = new Color(0.9f,0.1f,0.1f);

    [Range(0f,1f)]
    [SerializeField] private float midThreshold = 0.5f;
    [Range(0f,1f)]
    [SerializeField] private float lowThreshold = 0.25f;

    [Header("动画设置")]
    [SerializeField] private float healthLerpSpeed = 5f;
    [SerializeField] private float colorLerpSpeed = 3f;

    private Health playerHealth;
    private float maxHealth = 1f;
    private float currentDisplayPercent;
    private float targetHealthPercent;
    private Color targetColor;
    private Color currentDisplayColor;

    private void Start()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if(player == null) return;

        playerHealth = player.GetComponent<Health>();
        if(playerHealth == null) return;

        maxHealth = playerHealth.MaxHealth;
        float initialPercet = maxHealth > 0f ? playerHealth.CurrentHealth / maxHealth : 0f;
        targetHealthPercent = initialPercet;
        currentDisplayPercent = initialPercet;
        targetColor = GetHealthColor(initialPercet);
        currentDisplayColor = targetColor;

        if(healthSlider != null) healthSlider.value = initialPercet;
        if(fillImage != null) fillImage.color = targetColor;

        GameEvents.OnPlayerHealthChanged += HandlePlayerHealthChanged;
    }

    private void OnEnable()
    {
        // Start 里可能尚未绑定，这里兜底再订一次由 Start 完成；此处仅保证重复进场景可恢复
        GameEvents.OnPlayerHealthChanged -= HandlePlayerHealthChanged;
        GameEvents.OnPlayerHealthChanged += HandlePlayerHealthChanged;
    }

    private void OnDisable()
    {
        GameEvents.OnPlayerHealthChanged -= HandlePlayerHealthChanged;
    }

    private void HandlePlayerHealthChanged(float current, float max)
    {
        maxHealth = max > 0f ? max : 1f;
        UpdateHealthBar(current);
    }

    private void Update()
    {
        if(Mathf.Abs(currentDisplayPercent - targetHealthPercent)> 0.001f)
        {
            currentDisplayPercent = Mathf.Lerp(
                currentDisplayPercent,targetHealthPercent,Time.deltaTime * healthLerpSpeed
            );
        }

        if(healthSlider != null) healthSlider.value = currentDisplayPercent;

        if(currentDisplayColor != targetColor)
        {
            currentDisplayColor = Color.Lerp(
                currentDisplayColor,
                targetColor,
                Time.deltaTime * colorLerpSpeed
            );
        }

        if(fillImage != null) fillImage.color = currentDisplayColor;
    }

    private void UpdateHealthBar(float currentHealth)
    {
        float healthPercent = maxHealth > 0f ? currentHealth / maxHealth : 0f;
        targetHealthPercent = healthPercent;
        targetColor = GetHealthColor(healthPercent);
    }

    private Color GetHealthColor(float percent)
    {
        if(percent <= lowThreshold)
        {
            return lowHealthColor;
        }
        else if(percent <= midThreshold)
        {
            float t =( percent - lowThreshold) / (midThreshold - lowThreshold);
            return Color.Lerp(lowHealthColor,midHealthColor,t);
        }
        else
        {
            float t = (percent - midThreshold) / (1f - midThreshold);
            return Color.Lerp(midHealthColor,highHealthColor,t);
        }
    }

    public void RefreshHealthBar()
    {
        if(playerHealth != null)
        {
            UpdateHealthBar(playerHealth.CurrentHealth);
        }
    }

    public void SetHealthImmediate(float percent)
    {
        targetHealthPercent = percent;
        currentDisplayPercent = percent;
        targetColor = GetHealthColor(percent);
        currentDisplayColor = targetColor;

        if(healthSlider != null) healthSlider.value = percent;
        if(fillImage != null) fillImage.color = targetColor;
    }
}

using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 敌人头顶血条的单个实例（从模板克隆，由 EnemyHealthBarsForm 驱动）。
/// 只做三件事：画 fill/ghost、淡入淡出、被外部摆位置。
/// </summary>
public class EnemyHealthBarWidget : MonoBehaviour
{
    [Header("引用")]
    [SerializeField] private Image fillImage;
    [SerializeField] private Image ghostImage;
    [SerializeField] private CanvasGroup canvasGroup;

    [Header("动画")]
    [Tooltip("主条追赶速度（归一化/秒）")]
    [SerializeField] private float fillSpeed = 10f;

    [Tooltip("掉血后 ghost 停留多久（秒）")]
    [SerializeField] private float ghostDelay = 0.35f;

    [Tooltip("ghost 回落速度（归一化/秒）")]
    [SerializeField] private float ghostSpeed = 1.5f;

    [Tooltip("淡入淡出速度（alpha/秒）")]
    [SerializeField] private float fadeSpeed = 10f;

    private RectTransform m_rect;
    private float m_target = 1f;
    private float m_displayed = 1f;
    private float m_ghost = 1f;
    private float m_ghostTimer;
    private float m_alphaTarget;

    public RectTransform Rect
    {
        get
        {
            if (m_rect == null) m_rect = transform as RectTransform;
            return m_rect;
        }
    }

    private void Awake()
    {
        m_rect = transform as RectTransform;
        m_target = m_displayed = m_ghost = 1f;
        m_alphaTarget = 0f;
        if (canvasGroup != null) canvasGroup.alpha = 0f;
        Apply();
    }

    /// <summary>设置当前血量百分比。instant=true 时直接跳到目标（初始化用）</summary>
    public void SetValue(float current, float max, bool instant = false)
    {
        float percent = max > 0f ? Mathf.Clamp01(current / max) : 0f;

        if (instant)
        {
            m_target = m_displayed = m_ghost = percent;
            m_ghostTimer = 0f;
            Apply();
            return;
        }

        if (percent < m_target)
        {
            m_ghost = Mathf.Max(m_ghost, m_target);
            m_ghostTimer = ghostDelay;
        }
        else if (percent > m_target)
        {
            m_ghost = percent;
        }

        m_target = percent;
    }

    /// <summary>由 EnemyHealthBarsForm 设置屏幕坐标（Canvas 局部坐标）</summary>
    public void SetLocalPosition(Vector2 position)
    {
        if (Rect != null) Rect.anchoredPosition = position;
    }

    public void SetVisible(bool visible)
    {
        m_alphaTarget = visible ? 1f : 0f;
    }

    /// <summary>受击瞬间立刻可见，不等渐入</summary>
    public void ShowImmediate()
    {
        m_alphaTarget = 1f;
        if (canvasGroup != null) canvasGroup.alpha = 1f;
    }

    private void Update()
    {
        float dt = Time.unscaledDeltaTime;

        m_displayed = Mathf.MoveTowards(m_displayed, m_target, fillSpeed * dt);

        if (m_ghost > m_target)
        {
            if (m_ghostTimer > 0f) m_ghostTimer -= dt;
            else m_ghost = Mathf.MoveTowards(m_ghost, m_target, ghostSpeed * dt);
        }
        else
        {
            m_ghost = m_target;
        }

        Apply();

        if (canvasGroup != null)
        {
            canvasGroup.alpha = Mathf.MoveTowards(canvasGroup.alpha, m_alphaTarget, fadeSpeed * dt);
        }
    }

    private void Apply()
    {
        if (fillImage != null) fillImage.fillAmount = m_displayed;
        if (ghostImage != null) ghostImage.fillAmount = Mathf.Max(m_ghost, m_displayed);
    }
}

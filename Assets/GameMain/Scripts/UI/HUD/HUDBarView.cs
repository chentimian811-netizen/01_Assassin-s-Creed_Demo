using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 单条资源条（HP / MP / 耐力）的通用视图。
/// 只负责把 0~1 的百分比画出来，数值从哪来由订阅方决定。
///
/// 结构约定（HUD 生成器按这个结构建）：
///   Frame（底框）→ Background → Ghost（延迟掉血）→ Fill（主条）
/// Ghost 与 Fill 都是 Image.Type.Filled / Horizontal / Left。
///
/// 为什么单独抽出来：血条、蓝条、耐力条只有颜色不同，
/// 动画与填值逻辑完全一致，复制三份只会让以后改手感要改三处。
/// </summary>
public class HUDBarView : MonoBehaviour
{
    [Header("引用")]
    [Tooltip("主填充条，Image Type 必须是 Filled / Horizontal")]
    [SerializeField] private Image fillImage;

    [Tooltip("延迟掉血条，画在主条后面；掉血时短暂露出")]
    [SerializeField] private Image ghostImage;

    [Header("动画")]
    [Tooltip("主条追赶速度（归一化/秒），越大越干脆")]
    [SerializeField] private float fillSpeed = 6f;

    [Tooltip("掉血后 ghost 停留多久才开始回落（秒）")]
    [SerializeField] private float ghostDelay = 0.45f;

    [Tooltip("ghost 回落速度（归一化/秒）")]
    [SerializeField] private float ghostSpeed = 1.2f;

    [Tooltip("暂停/卡肉时是否继续播放条动画")]
    [SerializeField] private bool useUnscaledTime = true;

    private float m_target = 1f;
    private float m_displayed = 1f;
    private float m_ghost = 1f;
    private float m_ghostTimer;

    private void Awake()
    {
        Apply();
    }

    /// <summary>设置当前值。instant=true 时不做动画（打开界面 / 复活回满时用）</summary>
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
            // 掉血：ghost 卡在旧值，等延迟结束再追
            m_ghost = Mathf.Max(m_ghost, m_target);
            m_ghostTimer = ghostDelay;
        }
        else if (percent > m_target)
        {
            // 回血：ghost 直接跟上，避免绿条后面拖一条橙色
            m_ghost = percent;
        }

        m_target = percent;
    }

    /// <summary>直接改主条颜色（低血量变色等）</summary>
    public void SetColor(Color color)
    {
        if (fillImage != null) fillImage.color = color;
    }

    private void Update()
    {
        float dt = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;

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
    }

    private void Apply()
    {
        if (fillImage != null) fillImage.fillAmount = m_displayed;
        if (ghostImage != null) ghostImage.fillAmount = Mathf.Max(m_ghost, m_displayed);
    }
}

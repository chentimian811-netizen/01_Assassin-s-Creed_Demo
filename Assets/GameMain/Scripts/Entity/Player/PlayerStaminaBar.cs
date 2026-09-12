using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 耐力条 UI 绑定器。挂在你自己的 Canvas 节点上，拖引用即可。
/// 只订阅 GameEvents.OnPlayerStaminaChanged，不轮询 PlayerStamina。
///
/// 接入步骤：
/// 1. Canvas 下建 Slider 或 fill Image
/// 2. 挂本脚本到该节点（或 HUD 根节点）
/// 3. 拖入 staminaSlider / fillImage（至少一个）
/// 4. 场景玩家需挂 PlayerStamina 并指定 StaminaConfig
/// </summary>
public class PlayerStaminaBar : MonoBehaviour
{
    [Header("UI 引用（至少拖一个）")]
    [Tooltip("耐力 Slider（0~1 归一化）")]
    [SerializeField] private Slider staminaSlider;

    [Tooltip("填充 Image（Image Type 需为 Filled 时用 fillAmount）")]
    [SerializeField] private Image fillImage;

    [Header("可选：力竭表现")]
    [Tooltip("力竭时填充图变色；不拖则跳过")]
    [SerializeField] private Image colorTarget;
    [SerializeField] private Color normalColor = new Color(0.85f, 0.75f, 0.25f);
    [SerializeField] private Color exhaustedColor = new Color(0.55f, 0.55f, 0.55f);

    private float displayPercent = 1f;
    private bool bound;
    private PlayerStamina cachedStamina;
    private bool exhaustedState;

    private void OnEnable()
    {
        GameEvents.OnPlayerStaminaChanged -= HandleStaminaChanged;
        GameEvents.OnPlayerStaminaChanged += HandleStaminaChanged;
        bound = true;

        // 玩家可能比 HUD 先 Awake，这里主动拉一次当前值
        RefreshFromPlayer();
    }

    private void OnDisable()
    {
        if (!bound) return;
        GameEvents.OnPlayerStaminaChanged -= HandleStaminaChanged;
        bound = false;
    }

    private void HandleStaminaChanged(float current, float max)
    {
        displayPercent = max > 0f ? Mathf.Clamp01(current / max) : 0f;
        Apply(displayPercent);
    }

    private void Apply(float percent)
    {
        if (staminaSlider != null) staminaSlider.value = percent;
        if (fillImage != null) fillImage.fillAmount = percent;
    }

    private void ApplyExhausted(bool exhausted)
    {
        if (exhaustedState == exhausted) return;
        exhaustedState = exhausted;
        if (colorTarget == null) return;
        colorTarget.color = exhausted ? exhaustedColor : normalColor;
    }

    private PlayerStamina FindStamina()
    {
        if (cachedStamina != null) return cachedStamina;
        var player = GameObject.FindGameObjectWithTag("Player");
        if (player == null) return null;
        cachedStamina = player.GetComponent<PlayerStamina>();
        return cachedStamina;
    }

    private void RefreshFromPlayer()
    {
        var stamina = FindStamina();
        if (stamina == null) return;

        HandleStaminaChanged(stamina.Current, stamina.Max);
        ApplyExhausted(stamina.IsExhausted);
    }

    private void Update()
    {
        // 力竭态不走事件（只在数值变化时发），这里轻量同步颜色
        var stamina = FindStamina();
        if (stamina == null) return;
        ApplyExhausted(stamina.IsExhausted);
    }
}

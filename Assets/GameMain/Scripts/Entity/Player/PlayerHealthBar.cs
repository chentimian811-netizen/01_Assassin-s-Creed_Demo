using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 玩家血条UI控制器（带平滑颜色过渡）
/// 挂载位置：Canvas下的血条UI对象
/// 功能：订阅玩家受击事件，实时更新血条显示，颜色平滑渐变
/// </summary>
public class PlayerHealthBar : MonoBehaviour
{
    [Header("UI引用")]
    [Tooltip("血条Slider组件")]
    [SerializeField] private Slider healthSlider;

    [Tooltip("血条填充图片")]
    [SerializeField] private Image fillImage;

    [Header("颜色设置")]
    [Tooltip("血量充足时的颜色（绿色）")]
    [SerializeField] private Color highHealthColor = new Color(0.2f,0.8f,0.2f);
    
    [Tooltip("血量充足时的颜色（黄色）")]
    [SerializeField] private Color midHealthColor = new Color(0.9f,0.9f,0.1f);

    [Tooltip("血量充足时的颜色（红色）")]
    [SerializeField] private Color lowHealthColor = new Color(0.9f,0.1f,0.1f);

    //中等血量阈值
    [Range(0f,1f)]
    [SerializeField] private float midThreshold = 0.5f;

    //危险血量阈值
    [Range(0f,1f)]
    [SerializeField] private float lowThreshold = 0.25f;

    [Header("动画设置")]
    [Tooltip("血条下降动画速度")]
    [SerializeField] private float healthLerpSpeed = 5f;

    //颜色渐变速度
    [SerializeField] private float colorLerpSpeed = 3f;

    private MeleeFighter plyaerFighter;

    //最大血量
    private float maxHealth;

    //当前血量百分比
    private float currentDisplayPercent;

    //目标血量百分比
    private float targetHealthPercent;
    //目标颜色
    private Color targetColor;

    //当前显示的颜色
    private Color currentDisplayColor;

    private void Start()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if(player != null)
        {
            plyaerFighter = player.GetComponent<MeleeFighter>();
            if(plyaerFighter != null)
            {
                maxHealth = plyaerFighter.Health;

                    //初始化血条
                float initialPercet = plyaerFighter.Health / maxHealth;
                targetHealthPercent = initialPercet;
                currentDisplayPercent = initialPercet;

                //初始化颜色
                targetColor = GetHealthColor(initialPercet);
                currentDisplayColor = targetColor;

                if(healthSlider != null)
                {
                    healthSlider.value = initialPercet;
                }
                if(fillImage != null)
                {
                    fillImage.color = targetColor;
                }

                //订阅受击事件
                plyaerFighter.OnGotHit += OnPlayerGoHit;
            }
        }
    }

    private void Update()
    {
        //平滑过渡血条数值
        if(Mathf.Abs(currentDisplayPercent - targetHealthPercent)> 0.001f)
        {
            //使用Lerp平滑过渡
            currentDisplayPercent = Mathf.Lerp(
                currentDisplayPercent,targetHealthPercent,Time.deltaTime * healthLerpSpeed
            );
        }

        if(healthSlider != null)
        {
            healthSlider.value = currentDisplayPercent;
        }

        //平滑过渡颜色
        if(currentDisplayColor != targetColor)
        {
            currentDisplayColor = Color.Lerp(
                currentDisplayColor,
                targetColor,
                Time.deltaTime * colorLerpSpeed
            );
        }

        if(fillImage != null)
        {
            fillImage.color = currentDisplayColor;
        }
    }

    private void OnDestroy()
    {
        if(plyaerFighter != null)
        {
            plyaerFighter.OnGotHit -= OnPlayerGoHit;
        }
    }

    //玩家受击反馈
    private void OnPlayerGoHit(MeleeFighter attacker)
    {
        UpdateHealthBar(plyaerFighter.Health);
    }

    //更新血条显示
    private void UpdateHealthBar(float currentHealth)
    {
        float healthPercent = currentHealth / maxHealth;
        
        //设置目标血量百分比
        targetHealthPercent = healthPercent;

        //根据血量百分比设置目标颜色
        targetColor = GetHealthColor(healthPercent);
    }

    //根据目标亚瑟百分比获取目标颜色
    private Color GetHealthColor(float percent)
    {
        if(percent <= lowThreshold)
        {
            return lowHealthColor;
        }
        else if(percent <= midThreshold)
        {
            //在中等和危险之前插值
            float t =( percent - lowThreshold) / (midThreshold - lowThreshold);
            return Color.Lerp(lowHealthColor,midHealthColor,t);
        }
        else
        {
            float t = (percent - midThreshold) / (1f - midThreshold);
            return Color.Lerp(midHealthColor,highHealthColor,t);
        }
    }

    //外部调用:强制刷新血条
    public void RefreshHealthBar()
    {
        if(plyaerFighter != null)
        {
            UpdateHealthBar(plyaerFighter.Health);
        }
    }

    //外部调用：立即设置血量
    public void SetHealthImmediate(float percent)
    {
        targetHealthPercent = percent;
        currentDisplayPercent = percent;
        targetColor = GetHealthColor(percent);
        currentDisplayColor = targetColor;

        if(healthSlider != null)
        {
            healthSlider.value = percent;
        }
        if(fillImage != null)
        {
            fillImage.color = targetColor;
        }
    }
}

using UnityEngine;

[CreateAssetMenu(menuName = "ACDemo/Configs/Parry Config", fileName = "ParryConfig")]
public class ParryConfig : ScriptableObject
{
    [Header("弹反")]
    [Tooltip("按下格挡后仍算弹反成功的宽松窗口（秒）")]
    public float parryWindow = 0.15f;

    [Tooltip("弹反成功后敌人硬直时长（秒）")]
    public float enemyStaggerDuration = 1.5f;

    [Tooltip("弹反成功后玩家可反击的窗口（秒），0 = 不限制")]
    public float riposteWindow = 2f;

    [Header("弹反成功后的保护")]
    [Tooltip("弹反成功后的短暂无敌时长（真实时间，秒）。防止被同一招的后续判定二次命中")]
    public float parryInvulnDuration = 0.3f;

    [Header("普通格挡")]
    [Range(0f, 1f)] public float blockDamageReduction = 0.5f;

    [Tooltip("格挡成功是否仍受击退")]
    public bool blockStillKnockback = true;
}

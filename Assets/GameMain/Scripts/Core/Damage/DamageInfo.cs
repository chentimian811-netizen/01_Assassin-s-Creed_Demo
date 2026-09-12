using UnityEngine;

/// <summary>
/// 伤害来源分类。用于区分玩家/敌人/环境伤害，
/// 避免到处 CompareTag("Player") 这种脆弱判断（篝火重生、坠落伤害、陷阱都要用）。
/// </summary>
public enum E_DamageSource
{
    Player,         // 玩家造成
    Enemy,          // 敌人造成
    Environment,    // 环境（陷阱/坠落/持续伤害地形）
    Scripted,       // 剧情/处决演出强制扣血
}

/// <summary>
/// 统一伤害包。所有对 IDamageable 的伤害必须走此结构，
/// 便于扩展弹反/处决/属性/元素。
/// </summary>
public struct DamageInfo
{
    /// <summary>最终伤害值（已算完武器加成与招式倍率，接收方不再做乘法）</summary>
    public float Amount;

    /// <summary>伤害来源根物体（玩家或敌人），用于击退方向与仇恨</summary>
    public GameObject Source;

    /// <summary>命中点世界坐标（特效/击退用）</summary>
    public Vector3 HitPoint;

    /// <summary>命中法线方向（特效朝向）</summary>
    public Vector3 HitDirection;

    /// <summary>来源分类</summary>
    public E_DamageSource SourceType;

    /// <summary>是否可被弹反（处决/背刺/环境伤害传 false）</summary>
    public bool IsParryable;

    /// <summary>攻击配置 ID（AttackData 资源名 / 招式表 ID），用于日志与任务</summary>
    public string AttackId;

    /// <summary>削韧值：用于敌人霸体/硬直判定，0 表示不削韧</summary>
    public float PoiseDamage;

    /// <summary>是否处决/背刺等特殊伤害（播放专属演出）</summary>
    public bool IsCritical;

    /// <summary>构造辅助：避免各处手写对象初始化器漏字段</summary>
    public static DamageInfo Create(float amount, GameObject source, Vector3 hitPoint,
        E_DamageSource sourceType = E_DamageSource.Enemy,
        bool parryable = true, string attackId = null,
        float poiseDamage = 0f, bool critical = false)
    {
        return new DamageInfo
        {
            Amount = amount,
            Source = source,
            HitPoint = hitPoint,
            HitDirection = source != null ? source.transform.forward : Vector3.forward,
            SourceType = sourceType,
            IsParryable = parryable,
            AttackId = attackId,
            PoiseDamage = poiseDamage,
            IsCritical = critical,
        };
    }
}

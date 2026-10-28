using UnityEngine;

/// <summary>
/// 可作为弹反目标的敌人（由 MeleeFighter 实现）。
/// 注意：这是"被弹反方"的能力，"发起弹反方"是 ParrySystem。
/// </summary>
public interface IParryTarget
{
    /// <summary>
    /// 当前这一招是否处于可被弹反的时段。
    /// 口径是"前摇 + 生效窗口"，且该招式 Attack.parryable = true ——
    /// 命中回调只发生在生效窗口，只认前摇会让弹反永远判定失败（详见 MeleeFighter 里注释）。
    /// </summary>
    bool IsInParryWindow { get; }

    /// <summary>
    /// 弹反成功：打断当前招式并进入硬直。
    /// </summary>
    /// <param name="parrier">发起弹反的单位（玩家根对象）</param>
    /// <param name="staggerDuration">硬直时长（秒），由弹反方按 Parry.enemy_stagger_duration 给出</param>
    void OnParried(GameObject parrier, float staggerDuration);
}

/// <summary>
/// 攻击来源：由 MeleeFighter 实现，供 DamageRouter 读取本次攻击的配置。
/// </summary>
public interface IAttackSource
{
    /// <summary>当前正在生效的招式配置；非 Impact 阶段返回 null</summary>
    AttackData CurrentAttack { get; }

    /// <summary>武器 ID（-1 = 未装备），用于查数据表基础伤害</summary>
    int WeaponID { get; }

    /// <summary>武器升级等级</summary>
    int UpgradeLevel { get; }
}

using UnityEngine;

/// <summary>
/// 可作为弹反目标的敌人（由 MeleeFighter 实现）。
/// 注意：这是"被弹反方"的能力，"发起弹反方"是 ParrySystem。
/// </summary>
public interface IParryTarget
{
    /// <summary>当前是否处于可被弹反的攻击窗口（前摇内）</summary>
    bool IsInParryWindow { get; }

    /// <summary>弹反成功：打断当前攻击、进入硬直</summary>
    void OnParried(GameObject parrier);
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

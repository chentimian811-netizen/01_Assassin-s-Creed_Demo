using UnityEngine;

/// <summary>
/// 受击统一接口。由 Health 组件实现，玩家与敌人共用同一个组件。
/// 调用方（DamageRouter）只负责造成伤害，无敌/格挡/弹反一律由实现方判断。
/// </summary>
public interface IDamageable
{
    Transform Transform { get; }

    /// <summary>当前血量（HUD / Boss 阶段阈值读取）</summary>
    float CurrentHealth { get; }

    /// <summary>最大血量（Boss 血条必须靠它，原 MeleeFighter 没有此字段）</summary>
    float MaxHealth { get; }

    /// <summary>是否处于无敌（翻滚 i-Frame、处决演出、复活保护）</summary>
    bool IsInvulnerable { get; }

    /// <summary>是否已死亡</summary>
    bool IsDead { get; }

    /// <summary>扣血。无敌时应在实现内直接 return</summary>
    void TakeDamage(in DamageInfo info);

    /// <summary>治疗（篝火/道具），clamp 到 MaxHealth</summary>
    void Heal(float amount);

    /// <summary>开关无敌。reason 用于区分来源，避免多来源互相覆盖（见 Health 实现）</summary>
    void SetInvulnerable(string reason, bool value);
}

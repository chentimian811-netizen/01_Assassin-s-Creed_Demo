using UnityEngine;

/// <summary>
/// 伤害路由：全工程唯一允许调用 IDamageable.TakeDamage 的地方。
///
/// 为什么需要它（而不是各处直接 TakeDamage）：
///   1. 附魔/增伤/减伤/难度系数将来只在这里加一次；
///   2. 可以统一做空值与自伤过滤；
///   3. 便于集中打日志与埋点（论文性能测试要统计伤害次数）。
/// </summary>
public static class DamageRouter
{
    /// <summary>
    /// 红线 4 的唯一例外：DamageRouter 是静态类无法序列化 SO。
    /// 若未来需要调这个值，迁到 Config SO 并在 ProcedureGame.OnEnter 静态注入。
    /// </summary>
    private const float FallbackBaseDamage = 5f;

    /// <summary>
    /// 施加伤害（已知受击方时直传）。
    /// 近战 OnTriggerEnter 必须用这个重载：trigger 回调里的 other 是【攻击方】的 hitbox，
    /// 若把 other 当 target 传入 Collider 版，GetComponentInParent 会找到攻击者自己的
    /// Health，再被自伤过滤拦下 —— 表现为"近战永远 0 伤害"，编译与 grep 都不会发现。
    /// </summary>
    public static bool Apply(IDamageable target, GameObject attacker, Vector3 hitPoint, bool parryable = true)
    {
        if (target == null) return false;
        if (attacker != null && target.Transform.gameObject == attacker) return false;

        float amount = ResolveAmount(attacker, out AttackData attack, out int weaponId);
        if (amount <= 0f) return false;

        var info = DamageInfo.Create(amount, attacker, hitPoint, ResolveSourceType(attacker),
            parryable, attack != null ? attack.name : null, attack != null ? attack.PoiseDamage : 0f);
        target.TakeDamage(info);
        return true;
    }

    /// <summary>
    /// 施加伤害（受击碰撞体，向上找 IDamageable）。投射物/环境伤害用。
    /// </summary>
    public static bool Apply(Collider target, GameObject attacker, Vector3 hitPoint, bool parryable = true)
    {
        if (target == null) return false;
        return Apply(target.GetComponentInParent<IDamageable>(), attacker, hitPoint, parryable);
    }

    /// <summary>
    /// 直接按数值施加伤害（投射物、环境伤害、剧情伤害用）。
    /// 这是给"没有 AttackData 的伤害源"留的口子，不再需要目标去认识 MeleeFighter。
    /// </summary>
    public static bool ApplyAmount(Collider target, GameObject attacker, Vector3 hitPoint,
        float amount, E_DamageSource sourceType, bool parryable = true, string attackId = null)
    {
        if (target == null || amount <= 0f) return false;

        IDamageable damageable = target.GetComponentInParent<IDamageable>();
        if (damageable == null) return false;
        if (attacker != null && damageable.Transform.gameObject == attacker) return false;

        var info = DamageInfo.Create(amount, attacker, hitPoint, sourceType, parryable, attackId);
        damageable.TakeDamage(info);
        return true;
    }

    /// <summary>
    /// 解析本次攻击的最终伤害：
    ///   基础伤害 = 数据表 BaseDamage（按武器ID查）或兜底值
    ///   最终伤害 = 基础伤害 × 招式倍率（AttackData.DamageMultiplier）
    /// 这样"伤害写进 SO"与"数值仍由数据表配置"两件事不打架。
    /// </summary>
    private static float ResolveAmount(GameObject attacker, out AttackData attack, out int weaponId)
    {
        attack = null;
        weaponId = -1;
        if (attacker == null) return 0f;

        IAttackSource source = attacker.GetComponent<IAttackSource>();
        if (source == null) return 0f;

        attack = source.CurrentAttack;
        weaponId = source.WeaponID;

        // 基础伤害：优先数据表，查不到用兜底
        float baseDamage = FallbackBaseDamage;
        if (weaponId > 0 && DataRepository.ItemTable != null &&
            DataRepository.ItemTable.TryGetValue(weaponId, out var item))
        {
            baseDamage = WeaponUpgradeSystem.CalculateDamage(item.BaseDamage, source.UpgradeLevel);
        }

        // 招式倍率
        float multiplier = attack != null ? attack.DamageMultiplier : 1f;
        return baseDamage * Mathf.Max(0f, multiplier);
    }

    private static E_DamageSource ResolveSourceType(GameObject attacker)
    {
        if (attacker == null) return E_DamageSource.Environment;
        return attacker.CompareTag("Player") ? E_DamageSource.Player : E_DamageSource.Enemy;
    }
}

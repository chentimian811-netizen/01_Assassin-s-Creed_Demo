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
    /// 兜底基础伤害：已迁到 CombatGlobal 表（Gameplay.xlsx），这里不再写死常量。
    /// 只在"武器 ID 解析不出物品"时使用；正常武器伤害走 Item.BaseDamage × 招式倍率。
    /// 取不到表时返回 0：宁可这次不结算，也不用旧常量静默掩盖配置缺失。
    /// </summary>
    private static float FallbackBaseDamage => DataRepository.GetCombatGlobal()?.FallbackBaseDamage ?? 0f;

    /// <summary>
    /// 是否把每次伤害结算打到 Console（调试用）。false = 完全不产生日志开销。
    ///
    /// 【为什么放在这里】DamageRouter 是全工程伤害的唯一出口，在这里打一次就覆盖
    /// 近战/投射物/处决全部来源；不必在每个攻击者身上各写一份，也不会漏掉某条通道。
    /// 排查"到底打中没打中"时先看这条日志：有日志 = 命中盒、Trigger、伤害计算全通，
    /// 那问题就一定在表现层（受击动画/飘字/音效），而不是数值层。
    /// </summary>
    public static bool LogDamage = true;

    /// <summary>
    /// 施加伤害（已知受击方时直传）。
    /// 近战 OnTriggerEnter 必须用这个重载：trigger 回调里的 other 是【攻击方】的 hitbox，
    /// 若把 other 当 target 传入 Collider 版，GetComponentInParent 会找到攻击者自己的
    /// Health，再被自伤过滤拦下 —— 表现为"近战永远 0 伤害"，编译与 grep 都不会发现。
    /// </summary>
    public static bool Apply(IDamageable target, GameObject attacker, Vector3 hitPoint, bool parryable = true)
    {
        if (target == null) return false;

        // 尸体不再结算，也不再产生命中反馈（卡肉 / 震屏）：
        // 敌人对倒地玩家继续补刀时，这些反馈会一直响，看起来像"打中了但没伤害"
        if (target.IsDead) return false;

        if (attacker != null && target.Transform.gameObject == attacker) return false;

        float amount = ResolveAmount(attacker, out AttackData attack, out int weaponId);
        if (amount <= 0f) return false;

        var info = DamageInfo.Create(amount, attacker, hitPoint, ResolveSourceType(attacker),
            parryable, attack != null ? attack.name : null, attack != null ? attack.PoiseDamage : 0f);

        // 拦截层（格挡 / 弹反）：被完全吃掉时不进受击流程，也就不播卡肉与震屏
        if (!RunInterceptors(target, ref info)) return false;

        target.TakeDamage(info);

        // 命中确认日志：攻击方 → 受击方、招式、伤害、结算后血量
        if (LogDamage && target.Transform != null)
        {
            Health victim = target.Transform.GetComponent<Health>();
            string remain = victim != null
                ? $"，剩余 {victim.CurrentHealth:0.#}/{victim.MaxHealth:0.#}" + (victim.IsDead ? "（致死）" : "")
                : "";
            // 打 info.Amount（结算后的真实伤害），不是拦截前的 amount：
            // 否则格挡减伤生效时日志照样显示原伤害，"到底减没减"完全看不出来
            string reduced = info.Amount < amount ? $"（原始 {amount:0.#}，被拦截）" : "";
            Debug.Log(
                $"[Damage] {(attacker != null ? attacker.name : "环境")} 命中 {target.Transform.name}" +
                $"（{(attack != null ? attack.name : "无招式")}）伤害 {info.Amount:0.#}{reduced}{remain}",
                victim != null ? victim.gameObject : null);
        }

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

        // 同 Apply：投射物 / 环境伤害也要能被格挡拦截，否则"举盾却被箭射满伤"
        if (!RunInterceptors(damageable, ref info)) return false;

        damageable.TakeDamage(info);

        // 同上：投射物 / 处决 / 环境伤害也统一留一条命中记录
        if (LogDamage && damageable.Transform != null)
        {
            Health victim = damageable.Transform.GetComponent<Health>();
            // 同样打结算后的 info.Amount（投射物也会被格挡减伤）
            string reduced = info.Amount < amount ? $"（原始 {amount:0.#}，被拦截）" : "";
            Debug.Log(
                $"[Damage] {(attacker != null ? attacker.name : "环境")} 命中 {damageable.Transform.name}" +
                $"（{attackId ?? sourceType.ToString()}）伤害 {info.Amount:0.#}{reduced}" +
                (victim != null ? $"，剩余 {victim.CurrentHealth:0.#}/{victim.MaxHealth:0.#}" : ""),
                victim != null ? victim.gameObject : null);
        }

        return true;
    }

    /// <summary>
    /// 询问受击方的伤害拦截层（格挡 / 弹反）。返回 false = 本次伤害被完全吞掉。
    ///
    /// 【为什么插在这一步】
    ///   位置必须是"伤害已算完、TakeDamage 还没调"。放进 Health.TakeDamage 会出两个问题：
    ///   ① TakeDamage 的第一道闸是无敌判定，格挡写在那里会让"伤害 0"也走进受击流程，
    ///      受击动画、命中事件、卡肉全都会照常播；
    ///   ② 致死伤害与普通伤害共用同一道闸，多一层语义以后排查成本翻倍。
    ///   在路由层拦下 = 这次命中没发生过，攻击方拿到的返回值也是"没打中"。
    ///
    /// 【性能】GetComponent 走 Unity 的组件缓存，不产生 GC；每次命中一次，
    ///   与 Apply 里原有的 GetComponent&lt;Health&gt; 同一量级。
    /// </summary>
    private static bool RunInterceptors(IDamageable target, ref DamageInfo info)
    {
        if (target == null || target.Transform == null) return true;

        var interceptor = target.Transform.GetComponent<IDamageInterceptor>();
        if (interceptor == null) return true;

        // 组件被禁用 = 拦截层关闭（将来"破盾"之类只要禁用组件即可）
        if (interceptor is Behaviour behaviour && !behaviour.enabled) return true;

        if (!interceptor.TryIntercept(ref info)) return true;   // 放行
        return info.Amount > 0f;                               // false = 完全免伤
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

        // 招式倍率。⚠️ 没有招式就不结算伤害：
        // CurrentAttack 只在 Impact 判定窗口内非 null，所以这一条同时堵死了
        // "命中盒常开、人却没出招"的那种白送伤害（以前 attack 为 null 时倍率按 1 算）。
        if (attack == null) return 0f;
        return baseDamage * Mathf.Max(0f, attack.DamageMultiplier);
    }

    private static E_DamageSource ResolveSourceType(GameObject attacker)
    {
        if (attacker == null) return E_DamageSource.Environment;
        return attacker.CompareTag("Player") ? E_DamageSource.Player : E_DamageSource.Enemy;
    }
}

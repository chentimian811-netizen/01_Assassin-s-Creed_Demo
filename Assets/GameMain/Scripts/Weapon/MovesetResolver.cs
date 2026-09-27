using UnityEngine;

/// <summary>
/// 【宪法】动作集 = f(左手, 右手)。
/// 全系统只有这一个“当前是什么动作集”的判定点；任何地方都不得出现第二处。
/// </summary>
public static class MovesetResolver
{
    /// <summary>最近一次解析结果（供调试 / 后续阶段查询，只读）</summary>
    public static E_MovesetType CurrentType { get; private set; } = E_MovesetType.Unarmed;

    /// <summary>最近一次选中的动作集资产（覆盖器留空时为 null 或对应资产）</summary>
    public static MovesetConfig CurrentConfig { get; private set; }

    /// <summary>
    /// 由左右手 WeaponConfig 解析动作集类型。
    /// 右手必须是「单手近战类」（剑/匕/斧）才进入持械动作集；盾本身不算右手武器。
    /// </summary>
    public static E_MovesetType Resolve(WeaponConfig right, WeaponConfig left)
    {
        bool rightMelee = IsOneHandedMelee(right);
        bool leftShield = IsShield(left);

        if (!rightMelee) return E_MovesetType.Unarmed;
        return leftShield ? E_MovesetType.SwordAndShield
                          : E_MovesetType.OneHandedSword;
    }

    /// <summary>
    /// 解析并缓存当前动作集。configs 传入三件套（Unarmed / OneHandedSword / SwordAndShield）。
    /// 覆盖器可以为空；为空时不换 controller，只更新 CurrentType。
    /// </summary>
    public static MovesetConfig ResolveAndCache(
        WeaponConfig right,
        WeaponConfig left,
        MovesetConfig unarmed,
        MovesetConfig oneHandedSword,
        MovesetConfig swordAndShield)
    {
        E_MovesetType type = Resolve(right, left);
        CurrentType = type;

        MovesetConfig cfg;
        switch (type)
        {
            case E_MovesetType.OneHandedSword:
                cfg = oneHandedSword;
                break;
            case E_MovesetType.SwordAndShield:
                cfg = swordAndShield;
                break;
            default:
                cfg = unarmed;
                break;
        }

        CurrentConfig = cfg;
        return cfg;
    }

    /// <summary>是否盾类（isShield 优先；表 WeaponType=Shield 兜底）</summary>
    public static bool IsShield(WeaponConfig cfg)
    {
        if (cfg == null) return false;
        if (cfg.isShield) return true;
        return GetWeaponType(cfg) == E_WeaponType.Shield;
    }

    /// <summary>
    /// 是否「单手近战」：剑/匕/斧。
    /// 弓/杖/盾/空手都不算。同一套 OneHandedSword 动作集覆盖三种近战（本期不拆）。
    /// </summary>
    public static bool IsOneHandedMelee(WeaponConfig cfg)
    {
        if (cfg == null) return false;
        if (cfg.isShield || cfg.isRanged) return false;

        E_WeaponType t = GetWeaponType(cfg);
        return t == E_WeaponType.Sword
            || t == E_WeaponType.Dagger
            || t == E_WeaponType.Axe;
    }

    static E_WeaponType GetWeaponType(WeaponConfig cfg)
    {
        if (cfg == null) return E_WeaponType.Sword;
        if (DataRepository.ItemTable != null
            && DataRepository.ItemTable.TryGetValue(cfg.weaponID, out var item))
            return item.WeaponType;
        if (cfg.isShield) return E_WeaponType.Shield;
        if (cfg.isRanged) return E_WeaponType.Bow;
        return E_WeaponType.Sword;
    }
}

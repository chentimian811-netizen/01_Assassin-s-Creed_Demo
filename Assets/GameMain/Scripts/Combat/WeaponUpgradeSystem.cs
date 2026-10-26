using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 武器升级公式。只把系数外置到 WeaponUpgrade 单行表（Gameplay.xlsx），
/// 公式形态与整数返回类型保持原样：伤害/暴击/费用都还是同一套线性公式。
/// </summary>
public static class WeaponUpgradeSystem
{
    /// <summary>武器最高等级。取不到表时返回 0 并已由 DataRepository 报错</summary>
    public static int GetMaxLevel() => DataRepository.GetWeaponUpgrade()?.MaxLevel ?? 0;

    public static int CalculateDamage(int baseDamage, int level)
    {
        int perLevel = DataRepository.GetWeaponUpgrade()?.DamagePerLevel ?? 0;
        return baseDamage + (level - 1) * perLevel;
    }

    public static float CalculateCritRate(float baseCritRate, int level)
    {
        float perLevel = DataRepository.GetWeaponUpgrade()?.CritRatePerLevel ?? 0f;
        return baseCritRate + (level - 1) * perLevel;
    }

    public static int GetUpgradeCost(int curremtLevel)
    {
        int perLevel = DataRepository.GetWeaponUpgrade()?.UpgradeCostPerLevel ?? 0;
        return (curremtLevel + 1) * perLevel;
    }

    public static bool CanUpgrade(int currentLevel,int gold,out string reason)
    {
        if(currentLevel >= GetMaxLevel())
        {
            reason = "MaxLevel";
            return false;
        }

        int cost = GetUpgradeCost(currentLevel);
        if (!CurrencyManager.Instance.CanAfford(cost))
        {
            reason = $"金币不足,需要{cost}金";
            return false;
        }
        reason = null;
        return true;
    }
}

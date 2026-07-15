using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class WeaponUpgradeSystem
{
    public static int GetMaxLevel() => 90;

    public static int CalculateDamage(int baseDamage,int level)
    {
        return baseDamage + (level -1) * 2;
    }   

    public static float CalculateCritRate(float baseCritRate,int level)
    {
        return baseCritRate + (level -1) * 0.003f;
    }

    public static int GetUpgradeCost(int curremtLevel)
    {
        return (curremtLevel + 1) * 30;
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

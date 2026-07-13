using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CurrencyManager 
{
    private static CurrencyManager _instance;
    public static CurrencyManager Instance
    {
        get
        {
            if(_instance == null)
            {
                _instance = new CurrencyManager();
            }
            return _instance;
        }
    }

    private const string GOLD_KEY = "PlayerGold";
    private const int DEFAULT_GOLD = 100000;

    private int gold;
    public int Gold => gold;

    public event Action<int> OnGoldChanged;

    private CurrencyManager()
    {
        Load();
    }

    public bool CanAfford(int amount)
    {
        return gold >= amount;
    }

    public bool Spend(int amout)
    {
        if (amout <= 0) return false;
        if (!CanAfford(amout)) return false;

        gold -= amout;
        Save();
        OnGoldChanged?.Invoke(gold);
        return true;
    }

    public void Earn(int amount)
    {
        if (amount <= 0) return;
        gold += amount;
        Save();
        OnGoldChanged?.Invoke(gold);
    }

    public void SetGold(int amount)
    {
        gold = Mathf.Max(0, amount);

        Save();

        OnGoldChanged?.Invoke(gold);
    }


    private void Load()
    {
        gold = PlayerPrefs.GetInt(GOLD_KEY, DEFAULT_GOLD);
    }

    private void Save()
    {
        PlayerPrefs.SetInt(GOLD_KEY, gold);
        PlayerPrefs.Save();
    }

}

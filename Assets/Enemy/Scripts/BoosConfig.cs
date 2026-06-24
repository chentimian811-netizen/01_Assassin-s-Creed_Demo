using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Boss 配置数据（ScriptableObject）
/// 通过菜单 Create → Combat System → Boss Config 创建
/// </summary>
[CreateAssetMenu(menuName = "Combat System/Boss Config")]
public class BossConfig : ScriptableObject
{
    [Header("Boss基础属性")]
    [Tooltip("BOSS最大生命值(普通敌人默认25)")]
    public float maxHealth = 100f;

    [Header("召唤配置")]
    [Tooltip("召唤小怪的血量阈值数组（按从高到低排列，如 0.66, 0.33）\n每次触发一个阈值后，下一个阈值才会生效")]
    public float[] summonHealthThresholds = new float[] {0.33f};

    [Tooltip("召唤小怪的预制体")]
    public GameObject minionPrefab;

    [Tooltip("每次召唤小怪的数量")]
    public int minionCount = 3;

    [Tooltip("小怪环绕BOSS的生成半径")]
    public float summonRadius = 5f;

    [Tooltip("召唤动画名称")]
    public string summonAnimName = "Summon";

    [Tooltip("召唤动画时长，动画结束后BOSS返回战斗")]
    public float summonAnimDuration = 2f;

    [Tooltip("小怪实际生成的延迟")]
    public float spawnDelay = 1f;

    [Tooltip("召唤给BOSS回复的血量")]
    public float healOnSummon = 0f;
}

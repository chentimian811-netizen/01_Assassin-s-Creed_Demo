using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Boss 控制器，继承自 EnemyController
/// 复用所有敌人状态（Idle/Patrol/Combat/Attack/Retreat/GettingHit/Dead）
/// 新增：多阈值血量监测 → 触发召唤机制
/// </summary>
public class BossController : EnemyController
{
    //boss的配置数据
    public BossConfig bossConfig;

    //召唤生产点
    public Transform[] summonPoints;

    //当前已触发到第几个阈值
    private int nextThresholdIndex = 0;

    //记录初始血量 用于计算阈值
    private float originalHealth;

    //是否正在召唤的过程中
    [HideInInspector]
    public bool isSummoning = false;

    /// <summary>
    /// 初始化 Boss
    /// 在 EnemyController.Start() 基础上设置 Boss 专属属性
    /// </summary>
    protected override void Start()
    {
        //调用父类初始化
        base.Start();

        //注册召唤状态到状态字典
        SummonState summonState = GetComponent<SummonState>();

        RegisterState(E_EnemyState.Sumon,summonState);

        //设置boss血量
        if(bossConfig != null)
        {
            Fighter.SetHealth(bossConfig.maxHealth);
            originalHealth = bossConfig.maxHealth;
        }
        else
        {
            originalHealth = Fighter.Health;
        }
    }

    /// <summary>
    /// 重写 Update：父类逻辑（状态机 + 动画 + 分离力）+ Boss 血量阈值监测
    /// </summary>
    protected override void Update()
    {
        base.Update();

        //检测召唤的阈值
        CheckSummonThreshold();
    }

    /// <summary>
    /// 检查血量是否跌破下一个召唤阈值
    /// 支持多阈值：例如 {0.66, 0.33} 表示 2/3 和 1/3 血量各触发一次
    /// </summary>
    private void CheckSummonThreshold()
    {
        if(bossConfig == null) return;
        if(bossConfig.minionPrefab == null) return;
        if(isSummoning) return;//正在召唤 跳过检查

        //所有阈值都已出发完毕
        if(nextThresholdIndex >= bossConfig.summonHealthThresholds.Length) return;

        //计算当前血量比例
        float healthRatio = Fighter.Health / originalHealth;

        //检查是否跌破到下一个阈值
        float threshold = bossConfig.summonHealthThresholds[nextThresholdIndex];
        if(healthRatio <= threshold)
        {
            //检查当前转台是否允许召唤
            if(!IsInState(E_EnemyState.GettingHit) && !IsInState(E_EnemyState.Dead))
            {
                //推进到下一个阈值，防止同一阈值重复触发
                nextThresholdIndex++;
                TriggerSummo();
            }
        }
    }


    //触发召唤：标记状态、切换到SummonState
    public void TriggerSummo()
    {
        isSummoning = true;
        ChangeState(E_EnemyState.Sumon);
    }

    /// <summary>
    /// 召唤完成后的回调（由 SummonState.Exit 调用）
    /// </summary>
    public void OnSummonComplete()
    {
        isSummoning = false;
    }
    
    /// <summary>
    /// 重置 Boss 状态（用于重新开始游戏）
    /// </summary>
    public void ResetBossState()
    {
        nextThresholdIndex = 0;
        isSummoning = false;

        // 重新设置血量
        if (bossConfig != null && Fighter != null)
        {
            Fighter.SetHealth(bossConfig.maxHealth);
            originalHealth = bossConfig.maxHealth;
        }
    }

    //获取第Index个小怪的生成位置
    public Vector3 GetSummonPostion(int index)
    {
        //优先使用预设生产点
        if(summonPoints != null && summonPoints.Length > 0)
        {
            return summonPoints[index % summonPoints.Length].position;
        }

        //在boss周围按圆形均匀分布
        float angle = (360f/Mathf.Max(1,bossConfig.minionCount) * index);
        float rad = angle * Mathf.Deg2Rad;
        Vector3 offset = new Vector3(
            Mathf.Cos(rad) * bossConfig.summonRadius,
            0f,
            Mathf.Sin(rad) * bossConfig.summonRadius
        );
        return transform.position + offset;
    }


}

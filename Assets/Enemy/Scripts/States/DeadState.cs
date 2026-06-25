using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DeadState : State<EnemyController>
{   

    [SerializeField] private float destroyDelay = 5f;

    [Header("击杀奖励")]
    [Tooltip("击杀普通敌人获得的金币")]
    [SerializeField] private int normalEnemyGold = 500;

     [Tooltip("击杀 Boss 获得的金币")]
    [SerializeField] private int bossGold = 1000;

    public override void Enter(EnemyController owner)
    {
        owner.VisionSensor.gameObject.SetActive(false);
        EnemyManager.i.RemoveEnemyInRange(owner);

        owner.NavAgent.enabled = false;
        owner.character.enabled = false;

        //击杀奖励金币
        GrantKillReward(owner);

        Destroy(owner.gameObject, destroyDelay);
    }


    /// <summary>
    /// 根据敌人类型发放击杀金币奖励
    /// </summary>
    private void GrantKillReward(EnemyController owner)
    {
        // 判断是否为 Boss
        BossController boss = owner.GetComponent<BossController>();
        int reward = (boss != null) ? bossGold : normalEnemyGold;

        // 增加金币
        CurrencyManager.Instance.Earn(reward);

        Debug.Log($"击杀敌人获得 {reward} 金币");
    }

}

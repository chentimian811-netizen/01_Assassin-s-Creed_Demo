using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DeadState : State<EnemyController>
{   

    [SerializeField] private float destroyDelay = 5f;


    public override void Enter(EnemyController owner)
    {
        owner.VisionSensor.gameObject.SetActive(false);
        EnemyManager.i.RemoveEnemyInRange(owner);

        owner.NavAgent.enabled = false;
        owner.character.enabled = false;

        // 打断未完成的攻击/受击协程
        var mf = owner.GetComponent<MeleeFighter>();
        if (mf != null) mf.StopAllCoroutines();

        Destroy(owner.gameObject,destroyDelay);
    }
    
}

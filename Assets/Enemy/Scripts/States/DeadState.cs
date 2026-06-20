using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DeadState : State<EnemyController>
{   

    [SerializeField] private float destroyDelay = 2f;


    public override void Enter(EnemyController owner)
    {
        owner.VisionSensor.gameObject.SetActive(false);
        EnemyManager.i.RemoveEnemyInRange(owner);

        owner.NavAgent.enabled = false;
        owner.character.enabled = false;

        Destroy(owner.gameObject,destroyDelay);
    }
}

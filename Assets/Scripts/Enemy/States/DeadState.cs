using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DeadState : State<EnemyController>
{
    public override void Enter(EnemyController owner)
    {


        owner.VersionSensor.gameObject.SetActive(false);
        EnemyManager.i.RemoveEnemyInRange(owner);

        owner.NavAgent.enabled = false;
        owner.Animator.enabled = false;
    }
}

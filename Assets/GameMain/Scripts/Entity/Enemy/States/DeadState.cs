using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DeadState : State<EnemyController>
{

    [SerializeField] private float destroyDelay = 5f;


    public override void Enter(EnemyController owner)
    {
        // 任一步抛异常都不能阻止 Destroy，否则尸体永远留在场上且可能被其它逻辑救活
        try
        {
            if (owner.VisionSensor != null)
                owner.VisionSensor.gameObject.SetActive(false);

            if (EnemyManager.i != null)
                EnemyManager.i.RemoveEnemyInRange(owner);

            if (owner.NavAgent != null)
                owner.NavAgent.enabled = false;
            if (owner.character != null)
                owner.character.enabled = false;

            // 停掉攻击/受击/各 State 协程，并禁用组件，杜绝死后被拉回战斗
            var mf = owner.GetComponent<MeleeFighter>();
            if (mf != null)
            {
                mf.StopAllCoroutines();
                mf.enabled = false;
            }
            foreach (var state in owner.GetComponents<State<EnemyController>>())
            {
                if (state != null && state != this)
                    state.StopAllCoroutines();
            }

            if (owner.Animator != null)
            {
                owner.Animator.SetFloat("forwardSpeed", 0f);
                owner.Animator.SetFloat("strafeSpeed", 0f);
                // 战斗状态在 Override Layer(1)。默认 CrossFade 打在 layer0 会找不到状态，
                // 死亡姿态播不出来，看起来像「倒地失败又站起来」。
                owner.Animator.CrossFade("Melee_FallBackDeath", 0.15f, 1);
            }

            // 关掉 EnemyController.Update：死后不再 Execute 状态机、不再刷速度参数
            owner.enabled = false;
        }
        finally
        {
            Destroy(owner.gameObject, destroyDelay);
        }
    }

}

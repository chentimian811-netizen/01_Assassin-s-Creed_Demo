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

            // 停掉攻击/受击/各 State 协程，关掉命中盒，并禁用组件，杜绝死后被拉回战斗。
            // ⚠️ 必须先 ForceStopCombat 再 enabled = false：组件一旦禁用就退订了 OnEnemyKilled，
            //    MeleeFighter.HandleEnemyKilled 里的 DisableAllHitxboxes() 永远等不到，
            //    尸体带着"攻击生效中"的命中盒躺在地上（尸体打死活人）。
            var mf = owner.GetComponent<MeleeFighter>();
            if (mf != null)
            {
                mf.ForceStopCombat();
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
            // 改用真实时间销毁：Destroy(go, t) 与 Invoke 一样走【缩放时间】，
            // 一旦 Time.timeScale 被卡肉/暂停压到 0，延迟会被无限拉长，
            // 表现就是"敌人明明死了却一直躺在地上不消失"。
            StartCoroutine(CoDestroyAfterRealtime(owner.gameObject, destroyDelay));
        }
    }

    /// <summary>
    /// 真实时间延迟销毁。宿主是本组件：死亡时只有 EnemyController/MeleeFighter 被禁用，
    /// DeadState 自身仍启用、root 也保持 active，协程一定能跑完。
    /// </summary>
    private IEnumerator CoDestroyAfterRealtime(GameObject go, float delay)
    {
        Debug.Log($"[DeadState] {go.name} 进入死亡终态，{delay:0.##}s 后销毁", this);

        yield return new WaitForSecondsRealtime(delay);

        if (go != null) Destroy(go);
    }
}

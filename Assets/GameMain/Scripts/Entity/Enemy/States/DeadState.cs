using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DeadState : State<EnemyController>
{

    // 回收延迟来自 Enemy 表 destroy_delay（真实时间，不受 timeScale 影响）；
    // 到点只把物体隐藏，不销毁 —— 等 EnemyManager.RespawnAll 再打开（Phase 4 第 3 步：敌人重生）


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
            // 改用真实时间回收：Destroy(go, t) 与 Invoke 一样走【缩放时间】，
            // 一旦 Time.timeScale 被卡肉/暂停压到 0，延迟会被无限拉长，
            // 表现就是"敌人明明死了却一直躺在地上不消失"。
            StartCoroutine(CoRecycleAfterRealtime(owner, owner.Config != null ? owner.Config.DestroyDelay : 0f));
        }
    }

    /// <summary>
    /// 真实时间延迟**回收**（不销毁）：到点把整个敌人物体 SetActive(false)，
    /// 等 <see cref="EnemyManager.RespawnAll"/> → <see cref="EnemyController.ResetToSpawn"/> 再打开。
    ///
    /// 【为什么从 Destroy 改成回收】
    ///   Phase 4 要做「篝火休息 / 玩家死亡 → 敌人重生」。单场景阶段不能重载场景，
    ///   Destroy 掉就只剩「重新 Instantiate」一条路，而重新实例化会丢巡逻点引用、
    ///   血条绑定（EnemyHealthBarsForm 是开局一次性建表）与 EnemyManager 的注册关系。
    ///
    /// 【为什么要判 IsInDeadState】
    ///   延迟期内玩家可能已经点篝火（或死亡回篝火）把这只敌人复活了；
    ///   不判的话回收协程到点会把刚复活的敌人重新关掉（表现：复活后立刻消失）。
    ///   宿主是本组件：死亡时只有 EnemyController/MeleeFighter 被禁用，
    ///   DeadState 自身仍启用、root 也保持 active，协程一定能跑完。
    /// </summary>
    private IEnumerator CoRecycleAfterRealtime(EnemyController owner, float delay)
    {
        if (owner == null) yield break;

        Debug.Log($"[DeadState] {owner.name} 进入死亡终态，{delay:0.##}s 后回收（等待重生）", owner);

        yield return new WaitForSecondsRealtime(delay);

        if (owner == null) yield break;
        if (!owner.IsInDeadState) yield break;   // 这段延迟里已经被复活了

        owner.gameObject.SetActive(false);
    }
}

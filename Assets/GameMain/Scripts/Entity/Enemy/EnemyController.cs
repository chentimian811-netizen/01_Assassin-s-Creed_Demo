using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using Cinemachine;
using Unity.Mathematics;

public enum E_EnemyState
{
    Idle,
    Patrol,
    CombatMovement,
    Attack,
    RetreatAfterAttack,
    Dead,
    GettingHit,
}

public class EnemyController : MonoBehaviour
{
    // ---- 数值来源：Enemy 表（Gameplay.xlsx 的 Enemy Sheet），Awake 解析一次 ----
    //   各 FSM 状态统一通过 Config 读数值，不各自查表、不各自保存配置 ID。
    private Enemy m_configRow;

    /// <summary>本敌人的配置行。可能为 null（缺配置时已由解析入口报错）</summary>
    public Enemy Config => m_configRow;

    /// <summary>视野角度（度）</summary>
    public float Fov => m_configRow != null ? m_configRow.Fov : 0f;

    /// <summary>警戒范围（米）</summary>
    public float AlertRange => m_configRow != null ? m_configRow.AlertRange : 0f;

    public List<MeleeFighter> TargetsInRange { get; set; } = new List<MeleeFighter>();

    public MeleeFighter Target { get; set; }
    public StateMachine<EnemyController> stateMachine { get; private set; }

    public SkinnedMeshHighlighter MeshHightlighter { get; private set; }

    Dictionary<E_EnemyState, State<EnemyController>> stateDict;

    public NavMeshAgent NavAgent { get; private set; }

    public Animator Animator { get; private set; }
    public MeleeFighter Fighter { get; private set; }
    public VisionSensor VisionSensor { get;  set; }
    public CharacterController character { get; private set; }
    public Health Health { get; private set; }
    public float CombatMovementTimer { get; set; } = 0f;

    [Header("相机锁定观察点")]
    [Tooltip("锁定相机对准的锚点（Enemy.prefab 根下的 CameraAimPoint）；留空则退回根节点")]
    [SerializeField] private Transform m_CameraAimPoint;

    /// <summary>锁定相机的观察点。未配置锚点时退回根节点，保证旧 Prefab 不会空引用。</summary>
    public Transform CameraAimPoint =>
        m_CameraAimPoint != null ? m_CameraAimPoint : transform;

    Vector3 prevPos;

    // 与玩家的最小距离（米）
    private float minSeparationDistance => m_configRow != null ? m_configRow.MinSeparationDistance : 0f;
    // 分离推力强度
    private float separationForce => m_configRow != null ? m_configRow.SeparationForce : 0f;

    private void Awake()
    {
        // 必须在 Start 建状态机之前解析：PatrolState.Enter 等会在 Start 里立刻读数值
        m_configRow = CharacterConfigIdentity.GetEnemyRow(this);
    }

    private void Start()
    {
        MeshHightlighter = GetComponent<SkinnedMeshHighlighter>();

        NavAgent = GetComponent<NavMeshAgent>();

        Animator = GetComponent<Animator>();

        Fighter = GetComponent<MeleeFighter>();

        Health = GetComponent<Health>();

        character = GetComponent<CharacterController>();

        stateDict = new Dictionary<E_EnemyState, State<EnemyController>>();

        stateDict[E_EnemyState.Idle] = GetComponent<IdleState>();

        stateDict[E_EnemyState.Patrol] = GetComponent<PatrolState>();

        stateDict[E_EnemyState.CombatMovement] = GetComponent<CombatMovementStates>();

        stateDict[E_EnemyState.Attack] = GetComponent<AttackStates>();

        stateDict[E_EnemyState.RetreatAfterAttack] = GetComponent<RetreatAfterAttackState>();

        stateDict[E_EnemyState.Dead] = GetComponent<DeadState>();

        stateDict[E_EnemyState.GettingHit] = GetComponent<GettingHitState>();

        stateMachine = new StateMachine<EnemyController>(this);

        if(GetComponent<PatrolPoute>() != null && GetComponent<PatrolPoute>().HasPoints)
        {
            ChangeState(E_EnemyState.Patrol);
        }
        else
        {
            ChangeState(E_EnemyState.Idle);
        }
    }

    private void OnEnable()
    {
        GameEvents.OnUnitDamaged += HandleUnitDamaged;
    }

    private void OnDisable()
    {
        GameEvents.OnUnitDamaged -= HandleUnitDamaged;
    }

    /// <summary>
    /// 受击处理。替代原 Fighter.OnGotHit 的 lambda。
    /// 必须先用 victimUnitId 过滤"是不是我"，
    /// 否则一只敌人被打，全场敌人都会进受击/死亡状态。
    /// </summary>
    private void HandleUnitDamaged(string victimUnitId, DamageInfo info)
    {
        if (Health == null || victimUnitId != Health.UnitId) return;

        // ① 死亡 —— 全工程唯一的 Dead 状态转换入口，不能丢
        if (Health.IsDead)
        {
            ChangeState(E_EnemyState.Dead);
            return;
        }

        // ② 仇恨：从 info.Source 取攻击者
        MeleeFighter attacker = info.Source != null
            ? info.Source.GetComponent<MeleeFighter>()
            : null;

        if (Target == null && attacker != null)
        {
            Target = attacker;
            AlertNearbyEnemies();
        }

        // ③ 进入受击
        ChangeState(E_EnemyState.GettingHit);
    }

      
    public void ReactToHit(E_EnemyState state)
    {
        ChangeState(E_EnemyState.GettingHit);
    }

    /// <summary>是否已进入死亡终态。不依赖 Health.IsDead —— 处决/剧情杀可能绕过血量置死</summary>
    private bool m_inDeadState;

    /// <summary>是否处于配对演出锁定（处决/暗杀的受害者）。锁定时不跑 FSM、不动导航</summary>
    private bool m_inPerformance;

    /// <summary>演出锁自动解锁时刻（真实时间）：演出方被销毁/停用、finally 没跑到时的保险</summary>
    private float m_performanceUnlockRealtime;

    /// <summary>配对演出锁最长持续（真实秒）。超时自动解锁，避免演出方中途消失把敌人永久冻住</summary>
    private float performanceLockTimeout => m_configRow != null ? m_configRow.PerformanceLockTimeout : 0f;

    public void ChangeState(E_EnemyState state)
    {
        // 死亡为终态：GettingHit 协程、Attack 连段、EnemyManager 调度都可能在死后仍调 ChangeState，
        // 若放行会从 Dead 拉回 CombatMovement/Attack，表现为「倒地后又爬起来」。
        if (state == E_EnemyState.Dead)
        {
            m_inDeadState = true;
        }
        else if (m_inDeadState || m_inPerformance || (Health != null && Health.IsDead))
        {
            // m_inPerformance：处决演出期间锁死状态机。受害者只是被"借走"播配对动画，
            // 若放行 Attack→Retreat→CombatMovement，它会顶着受害动画追玩家，
            // 还会被 EnemyManager 排到出手（CrossFade 招式把受害动画顶掉 = "被处决的人站起来打我"）。
            return;
        }

        stateMachine.ChangeState(stateDict[state]);
    }

    /// <summary>
    /// 清掉仇恨并回到待机/巡逻（玩家复活 / 「返回主菜单 → 再开始」时调用）。
    ///
    /// 【为什么需要】
    ///   敌人的 Target 是玩家 MeleeFighter 的对象引用；玩家死亡 → 回主菜单 → 再开始时，
    ///   敌人仍然保持 CombatMovement/Attack，表现为「刚回到出生点就被围」。
    ///   这里把目标、视野缓存、导航路径一次性清掉；回到 Patrol/Idle 后下一帧会自己用
    ///   FindTarget 重新搜（只有玩家真的进了视野才会再拉仇恨）。
    ///
    /// 【只处理正在交战的敌人】
    ///   开局复位时无脑 `ChangeState(Idle)` 会把 PatrolState 顶掉 —— 表现就是「敌人
    ///   全都站在原地不巡逻了」。待机/巡逻的敌人保持原状态，只有正处在
    ///   CombatMovement / Attack / Retreat / GettingHit 的才拉回来。
    /// </summary>
    public void ResetAggro()
    {
        Target = null;
        TargetsInRange.Clear();
        CombatMovementTimer = 0f;

        // 已经死了的敌人只清目标，不要把它从 Dead 状态拉回来
        if (Health != null && Health.IsDead) return;

        // Start 还没跑完（stateMachine 未建）时不要动状态
        if (stateMachine == null) return;

        if (!IsInState(E_EnemyState.CombatMovement)
            && !IsInState(E_EnemyState.Attack)
            && !IsInState(E_EnemyState.RetreatAfterAttack)
            && !IsInState(E_EnemyState.GettingHit))
        {
            // 本来就在 Idle / Patrol：不要动，否则会把巡逻顶成原地站着
            return;
        }

        // 解除处决/演出锁，并清掉残留的攻击姿态（敌人同样用 Override Layer 播招式）
        SetPerformanceLock(false);
        Fighter?.ResetCombatPose();

        if (NavAgent != null && NavAgent.enabled)
        {
            if (NavAgent.isOnNavMesh) NavAgent.ResetPath();
            NavAgent.isStopped = false;
        }

        // 有巡逻点的回巡逻，没有的回待机（PatrolState.Enter 自己也会在无路线时退到 Idle）
        PatrolPoute route = GetComponent<PatrolPoute>();
        ChangeState(route != null && route.HasPoints ? E_EnemyState.Patrol : E_EnemyState.Idle);
    }

    /// <summary>
    /// 配对演出（处决/暗杀）锁定开关，由 MeleeFighter.PerformCounterAttack 开关。
    ///
    /// 为什么要锁：
    ///   处决是双人配对动画，受害者只是被借来播动画，它的 FSM 与 NavMeshAgent 仍在独立运行。
    ///   不锁的话，受害者在演出期间会照常 Retreat/CombatMovement 追击（"处决完先朝我跑一下"），
    ///   甚至被 EnemyManager 排到出手、用招式动画顶掉受害动画（"站起来朝我攻击"）。
    /// </summary>
    public void SetPerformanceLock(bool value)
    {
        if (m_inPerformance == value) return;
        m_inPerformance = value;

        // 上锁时记一个保险截止时刻：演出方（玩家）被销毁/停用时 finally 不一定跑得到
        if (value) m_performanceUnlockRealtime = Time.unscaledTime + Mathf.Max(1f, performanceLockTimeout);

        // 上锁时清掉在途寻路：否则 agent 会继续沿旧路径把"受害者"拖走
        if (NavAgent != null && NavAgent.enabled && NavAgent.isOnNavMesh)
        {
            if (value)
            {
                NavAgent.ResetPath();
                NavAgent.isStopped = true;
            }
            else
            {
                NavAgent.isStopped = false;
            }
        }
    }

    /// <summary>
    /// 处决/剧情杀的兜底致死：无视无敌与血量直接进死亡终态。
    /// 存在的意义见 Health.ForceKill —— 致死伤害和普通伤害共用同一道无敌闸，
    /// 一旦被拦下就再也没有第二条致死路径可走。
    /// </summary>
    public void ForceDeath()
    {
        m_inDeadState = true;

        if (Health != null) Health.ForceKill();   // 血量归零 + 补发 OnEnemyKilled（死亡表现订阅它）
        ChangeState(E_EnemyState.Dead);           // DeadState 会禁用组件、播死亡姿态并排销毁
    }


    public bool IsInState(E_EnemyState state)
    {
         return stateMachine.CurrentState == stateDict[state];
    }

    private void Update()
    {
        // 死亡后不再推进 FSM / 刷动画参数，避免与死亡姿态抢控制。
        // m_inDeadState 一起判：处决/剧情杀可能绕过血量置死，只信 Health.IsDead 会漏
        if (m_inDeadState || (Health != null && Health.IsDead))
        {
            transform.eulerAngles = new Vector3(0f, transform.eulerAngles.y, 0f);
            return;
        }

        // 演出锁保险：演出方若被销毁/停用，finally 不一定跑得到，超时自动解锁，
        // 避免这只敌人被永久冻在原地
        if (m_inPerformance && Time.unscaledTime >= m_performanceUnlockRealtime)
        {
            Debug.LogWarning($"[EnemyController] {name} 演出锁超时，自动解锁", this);
            SetPerformanceLock(false);
        }

        // 配对演出锁定：不跑状态机、不刷移动参数、不做分离推挤。
        // 受害者这期间只负责播受害动画，位移/朝向全交给演出方（MeleeFighter.PerformCounterAttack）
        if (m_inPerformance)
        {
            prevPos = transform.position;   // 保持速度基准，解锁那一帧不会算出巨大速度
            return;
        }

        stateMachine.Execute();


        // 巡逻状态下由 PatrolState 自己控制动画，跳过这里的计算
        if (!IsInState(E_EnemyState.Patrol))
        {
            var deltaPos = Animator.applyRootMotion ? Vector3.zero : transform.position - prevPos;


            var velocity = Time.deltaTime > 0 ? deltaPos / Time.deltaTime : Vector3.zero;

            // 计算实际移动速度（不归一化，保留原始值）
            float forwardSpeed = Vector3.Dot(velocity, transform.forward);
            if (IsInState(E_EnemyState.RetreatAfterAttack))
            {
                Animator.SetFloat("forwardSpeed", forwardSpeed);
            }
            else
            {
                Animator.SetFloat("forwardSpeed", forwardSpeed, 0.2f, Time.deltaTime);
            }
            
            float angle = Vector3.SignedAngle(transform.forward, velocity, Vector3.up);
            
            float strafeSpeed = Mathf.Sin(angle * Mathf.Deg2Rad);
            if (IsInState(E_EnemyState.RetreatAfterAttack))
            {
                Animator.SetFloat("strafeSpeed", strafeSpeed);
            }
            else
            {
                Animator.SetFloat("strafeSpeed", strafeSpeed, 0.2f, Time.deltaTime);
            }
        }

        if(Target != null && Target.HealthComponent != null && Target.HealthComponent.IsDead)
        {
            TargetsInRange.Remove(Target);
            EnemyManager.i.RemoveEnemyInRange(this);
        }

        EnforceSeparation();

        var euler = transform.eulerAngles;
        transform.eulerAngles = new Vector3(0,euler.y,0);

        prevPos = transform.position;

    }

    private void EnforceSeparation()
    {
        if(Target == null) return;
        if(Health != null && Health.IsDead)return;

        Vector3 playerPos = Target.transform.position;
        Vector3 diff = transform.position - playerPos;
        diff.y = 0;
        float dist = diff.magnitude;

        if(dist<minSeparationDistance && dist > 0.01f)
        {
            Vector3 pushDir = diff.normalized;
            float pushAmount = minSeparationDistance - dist;

            if(NavAgent != null && NavAgent.enabled && NavAgent.isOnNavMesh)
            {
                NavAgent.Move(pushDir * pushAmount * separationForce *Time.deltaTime);

            }
            else
            {
                transform.position += pushDir * pushAmount * separationForce * Time.deltaTime;
            }
        }
    }

    //只保留旋转，不应用位移，防止攻击动画穿过敌人
    private void OnAnimatorMove()
    {
        if(!Animator.applyRootMotion) return;

        transform.rotation = Animator.rootRotation;
    }
    public MeleeFighter FindTarget()
    {
        foreach (var target in TargetsInRange)
        {
            var vecToTarget = target.transform.position - transform.position;

            float angle = Vector3.Angle(transform.forward, vecToTarget);

            if (angle <= Fov / 2)
            {
                return target;
            }
        }

        return null;
    }

    public void AlertNearbyEnemies()
    {
        var colliders =  Physics.OverlapBox(transform.position, new Vector3(AlertRange / 2f, 1, AlertRange / 2f),
            Quaternion.identity, EnemyManager.i.EnemyLayer);

        foreach (var collider in colliders)
        {
            if(collider.gameObject == gameObject) continue;

            var naerbyEnemy = collider.GetComponent<EnemyController>();

            if(naerbyEnemy != null && naerbyEnemy.Target == null)
            {
                naerbyEnemy.Target = Target;
                naerbyEnemy.ChangeState(E_EnemyState.CombatMovement);
            }
        }
    }
}

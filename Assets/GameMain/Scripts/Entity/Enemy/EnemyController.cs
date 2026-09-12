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
    [field: SerializeField] public float Fov { get; private set; } = 180f;

    [field: SerializeField] public float AlertRange { get; private set; } = 20f;

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

    Vector3 prevPos;

    [SerializeField] private float minSeparationDistance = 1.2f; // 与玩家的最小距离
    [SerializeField] private float separationForce = 5f;         // 分离推力强度

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

    public void ChangeState(E_EnemyState state)
    {
        stateMachine.ChangeState(stateDict[state]);
    }


    public bool IsInState(E_EnemyState state)
    {
         return stateMachine.CurrentState == stateDict[state];
    }

    private void Update()
    {
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

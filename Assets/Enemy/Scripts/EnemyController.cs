
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
    public float CombatMovementTimer { get; set; } = 0f;




    Vector3 prevPos;

    private void Start()
    {
        MeshHightlighter = GetComponent<SkinnedMeshHighlighter>();

        NavAgent = GetComponent<NavMeshAgent>();

        Animator = GetComponent<Animator>();

        Fighter = GetComponent<MeleeFighter>();

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

        // stateMachine.ChangeState(stateDict[E_EnemyState.Idle]);

        if(GetComponent<PatrolPoute>() != null && GetComponent<PatrolPoute>().HasPoints)
        {
            ChangeState(E_EnemyState.Patrol);
        }
        else
        {
            ChangeState(E_EnemyState.Idle);
        }

        Fighter.OnGotHit += (MeleeFighter attacker) =>
        {
            if(Fighter.Health > 0)
            {
                if(Target == null)
                {
                    Target = attacker;
                    AlertNearbyEnemies();
                }
                ChangeState(E_EnemyState.GettingHit);
            }
            else
            {
                ChangeState(E_EnemyState.Dead);
            }
            
        };
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
            var velocity = deltaPos / Time.deltaTime;

            // 计算实际移动速度（不归一化，保留原始值）
            float forwardSpeed = Vector3.Dot(velocity, transform.forward);
            Animator.SetFloat("forwardSpeed", forwardSpeed, 0.2f, Time.deltaTime);

            float angle = Vector3.SignedAngle(transform.forward, velocity, Vector3.up);
            float strafeSpeed = Mathf.Sin(angle * Mathf.Deg2Rad);
            Animator.SetFloat("strafeSpeed", strafeSpeed, 0.2f, Time.deltaTime);
        }

        if(Target?.Health <= 0)
        {

            TargetsInRange.Remove(Target);
            EnemyManager.i.RemoveEnemyInRange(this);
        }

        prevPos = transform.position;

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

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using Cinemachine;

public enum E_EnemyState
{
    Idle,
    CombatMovement,
    Attack,
    ReteatAfterAttack,
    Dead,
    GettingHit,
}

public class EnemyController : MonoBehaviour
{
    [field: SerializeField] public float Fov { get; private set; } = 180f;

    public List<MeeleFighter> TargetsInRange { get; set; } = new List<MeeleFighter>();

    public MeeleFighter Target { get; set; }
    public StateMachine<EnemyController> stateMachine { get; private set; }

    public SkinnedMeshHighlighter MeshHightlighter { get; private set; }

    Dictionary<E_EnemyState, State<EnemyController>> stateDict;

    public NavMeshAgent NavAgent { get; private set; }

    public Animator Animator { get; private set; }
    public MeeleFighter Fighter { get; private set; }
    public VersionSensor VersionSensor { get;  set; }
    public CharacterController character { get; private set; }
    public float CombatMovementTimer { get; set; } = 0f;




    Vector3 prevPos;

    private void Start()
    {
        MeshHightlighter = GetComponent<SkinnedMeshHighlighter>();

        NavAgent = GetComponent<NavMeshAgent>();

        Animator = GetComponent<Animator>();

        Fighter = GetComponent<MeeleFighter>();

        character = GetComponent<CharacterController>();

        stateDict = new Dictionary<E_EnemyState, State<EnemyController>>();

        stateDict[E_EnemyState.Idle] = GetComponent<IdleState>();

        stateDict[E_EnemyState.CombatMovement] = GetComponent<CombatMovmentStates>();

        stateDict[E_EnemyState.Attack] = GetComponent<AttackSates>();

        stateDict[E_EnemyState.ReteatAfterAttack] = GetComponent<RetreatAfterAttackState>();

        stateDict[E_EnemyState.Dead] = GetComponent<DeadState>();

        stateDict[E_EnemyState.GettingHit] = GetComponent<GettingHitState>();

        stateMachine = new StateMachine<EnemyController>(this);
        stateMachine.ChangeState(stateDict[E_EnemyState.Idle]);

        Fighter.OnGotHit += () =>
        {
            if(Fighter.Health > 0)
            {
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

        var deltaPos =Animator.applyRootMotion ? Vector3.zero:transform.position - prevPos;
        var velocity = deltaPos / Time.deltaTime;

        float forwardSpeed =Vector3.Dot(velocity, transform.forward);
        Animator.SetFloat("forwardSpeed", forwardSpeed / NavAgent.speed, 0.2f, Time.deltaTime);

        float angle = Vector3.SignedAngle(transform.forward, velocity, Vector3.up);
        float strafeSpeed = Mathf.Sin(angle * Mathf.Deg2Rad);
        Animator.SetFloat("strafeSpeed", strafeSpeed, 0.2f, Time.deltaTime);

        if(Target?.Health <= 0)
        {

            TargetsInRange.Remove(Target);
            EnemyManager.i.RemoveEnemyInRange(this);
        }

        prevPos = transform.position;

    }

    public MeeleFighter FindTarget()
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
}

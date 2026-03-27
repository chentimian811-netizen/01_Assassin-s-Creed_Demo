using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public enum E_EnemyState
{
    Idle,
    CombatMovement,
    Attack,
}

public class EnemyController : MonoBehaviour
{
    [field: SerializeField] public float Fov { get; private set; } = 180f;

    public List<MeeleFighter> TargetsInRange { get; set; } = new List<MeeleFighter>();

    public MeeleFighter Target { get; set; }
    public StateMachine<EnemyController> stateMachine { get; private set; }

    Dictionary<E_EnemyState, State<EnemyController>> stateDict;

    public NavMeshAgent NavAgent { get; private set; }

    public Animator Animator { get; private set; }
    public MeeleFighter Fighter { get; private set; }
    public float CombatMovementTimer { get; set; } = 0f;


    Vector3 prevPos;

    private void Start()
    {
        NavAgent = GetComponent<NavMeshAgent>();

        Animator = GetComponent<Animator>();

        Fighter = GetComponent<MeeleFighter>();

        stateDict = new Dictionary<E_EnemyState, State<EnemyController>>();

        stateDict[E_EnemyState.Idle] = GetComponent<IdleState>();

        stateDict[E_EnemyState.CombatMovement] = GetComponent<CombatMovmentStates>();

        stateDict[E_EnemyState.Attack] = GetComponent<AttackSates>();

        stateMachine = new StateMachine<EnemyController>(this);
        stateMachine.ChangeState(stateDict[E_EnemyState.Idle]);
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

        prevPos = transform.position;

    }
}

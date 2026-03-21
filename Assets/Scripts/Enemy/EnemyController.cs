using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public enum E_EnemyState
{
    Idle,
    Chase,
}

public class EnemyController : MonoBehaviour
{
    [field:SerializeField] public float Fov { get; private set; } = 180f;

    public List<MeeleFighter> TargetsInRange {  get;  set; } = new List<MeeleFighter>();  

    public MeeleFighter Target { get; set; }
    public StateMachine<EnemyController> stateMachine {  get; private set; }

    Dictionary<E_EnemyState, State<EnemyController>> stateDict;

    public NavMeshAgent NavAgent { get; private set; }

    public Animator animator { get; private set; }

    private void Start()
    {
        NavAgent = GetComponent<NavMeshAgent>();

        animator = GetComponent<Animator>();

        stateDict = new Dictionary<E_EnemyState, State<EnemyController>>();
        stateDict[E_EnemyState.Idle] = GetComponent<IdleState>(); 
        stateDict[E_EnemyState.Chase] = GetComponent<ChaseState>(); 

        stateMachine = new StateMachine<EnemyController>(this);
        stateMachine.ChangeState(stateDict[E_EnemyState.Idle]);
    }

    public void ChangeState(E_EnemyState state)
    {
        stateMachine.ChangeState(stateDict[state]);
    }

    private void Update()
    {
        stateMachine.Execute();
    }
}

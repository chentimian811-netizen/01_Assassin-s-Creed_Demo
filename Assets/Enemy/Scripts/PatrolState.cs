using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 巡逻状态 —— 敌人沿 PatrolRoute 路径点移动
/// 替代 IdleState 作为敌人无目标时的默认行为
/// </summary>
public class PatrolState : State<EnemyController>
{
    // Animator 参数哈希缓存（提高性能）
    private static readonly int AnimForwardSpeed = Animator.StringToHash("forwardSpeed");
    private static readonly int AnimStrafeSpeed = Animator.StringToHash("strafeSpeed");
    private static readonly int AnimCombatMode = Animator.StringToHash("combatMode");

    //巡逻移动速度（对应走路动画 forwardSpeed=0.2）
    [SerializeField] private float patrolSpeed = 0.4f;

    //巡逻时的动画前进速度参数（对应 Walking 动画）
    [SerializeField] private float patrolForwardAnimSpeed = 0.2f;

    //到达路径点的判定距离
    [SerializeField] private float arrivalDistance = 0.5f;

    //巡逻路径引用
    [SerializeField] private PatrolPoute patrolRoute;

    //当前目标路径点
    private PatrolPiont currentPoint;

    //路径点等待计时器
    private float waitTimer = 0f;

    //是否正在等待
    private bool isWaiting = false;

    //进入巡逻状态
    public override void Enter(EnemyController owner)
    {
        //如果没有指定路径，尝试从同物体或子物体获取
        if (patrolRoute == null)
        {
            patrolRoute = owner.GetComponent<PatrolPoute>();
        }
        if (patrolRoute == null)
        {
            patrolRoute = owner.GetComponentInChildren<PatrolPoute>();
        }

        //无路径时回退到 Idle 行为
        if (patrolRoute == null || !patrolRoute.HasPoints)
        {
            owner.ChangeState(E_EnemyState.Idle);
            return;
        }

        //检查 NavMeshAgent
        if (owner.NavAgent == null)
        {
            owner.ChangeState(E_EnemyState.Idle);
            return;
        }

        //设置 NavMeshAgent 巡逻参数
        owner.NavAgent.speed = patrolSpeed;
        owner.NavAgent.stoppingDistance = arrivalDistance;
        owner.NavAgent.isStopped = false;

        //设置巡逻动画（走路）
        owner.Animator.SetFloat(AnimForwardSpeed, patrolForwardAnimSpeed);
        owner.Animator.SetFloat(AnimStrafeSpeed, 0f);
        owner.Animator.SetBool(AnimCombatMode, false);

        //从最近的路径点开始巡逻
        patrolRoute.SetNearestAsStart(owner.transform.position);

        //获取第一个目标
        currentPoint = patrolRoute.GetNextPoint();
        SetDestination(owner);

        isWaiting = false;
        waitTimer = 0f;
    }

    /// <summary>
    /// 巡逻状态每帧执行
    /// </summary>
    public override void Execute()
    {
        EnemyController owner = GetComponent<EnemyController>();

        //优先检测：发现玩家则切换到战斗
        if (owner.FindTarget() != null)
        {
            owner.ChangeState(E_EnemyState.CombatMovement);
            return;
        }

        //等待阶段，在路径点停留
        if (isWaiting)
        {
            waitTimer -= Time.deltaTime;

            //等待时播放 Idle 动画
            owner.Animator.SetFloat(AnimForwardSpeed, 0f, 0.2f, Time.deltaTime);

            if (waitTimer <= 0f)
            {
                isWaiting = false;
                currentPoint = patrolRoute.GetNextPoint();
                SetDestination(owner);
                owner.Animator.SetFloat(AnimForwardSpeed, patrolForwardAnimSpeed);
            }
            return;
        }

        //移动阶段：持续设置走路动画参数
        owner.Animator.SetFloat(AnimForwardSpeed, patrolForwardAnimSpeed, 0.2f, Time.deltaTime);
        owner.Animator.SetFloat(AnimStrafeSpeed, 0f, 0.2f, Time.deltaTime);

        //检查是否到达路径点
        if (!owner.NavAgent.pathPending && owner.NavAgent.remainingDistance <= arrivalDistance)
        {
            //如果路径点有等待时间，进入等待
            if (currentPoint != null && currentPoint.WaiteTime > 0f)
            {
                isWaiting = true;
                waitTimer = currentPoint.WaiteTime;
                owner.NavAgent.isStopped = true;
            }
            else
            {
                //立即前往下一个路径点
                currentPoint = patrolRoute.GetNextPoint();
                SetDestination(owner);
            }
        }
    }

    //退出巡逻状态
    public override void Exit()
    {
        EnemyController owner = GetComponent<EnemyController>();
        // 添加空值和状态检查
        if (owner.NavAgent == null || !owner.NavAgent.enabled || !owner.NavAgent.isOnNavMesh)
        {
            return;
        }
        //恢复默认速度
        owner.NavAgent.speed = 3.5f;
        owner.NavAgent.isStopped = false;
    }

    //设置 NavMeshAgent 目标位置
    private void SetDestination(EnemyController owner)
    {
        if (currentPoint != null)
        {
            owner.NavAgent.isStopped = false;
            owner.NavAgent.SetDestination(currentPoint.transform.position);
        }
    }
}

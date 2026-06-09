using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 巡逻状态 —— 敌人沿 PatrolRoute 路径点移动
/// 替代 IdleState 作为敌人无目标时的默认行为
/// </summary>
public class PatrolState : State<EnemyController>
{
    //巡逻移动速度
    [SerializeField] private float patrolSpeed = 1.5f;

    //到达路径点的判定距离
    [SerializeField] private float arrivalDistance = 0.5f;

    //巡逻路径引用
    [SerializeField] private PatrolPoute patrolRoute;

    //当前目标路近点
    private PatrolPiont currentPoint;

    //路近点等待计时器
    private float waitTimer = 0f;

    //是否正在等待
    private bool isWaiting = false;

    //进入巡逻转台
    public override void Enter(EnemyController owner)
    {
        //如果没有指定路径 尝试从同物体获取
        if(patrolRoute == null)
        {
            patrolRoute = owner.GetComponent<PatrolPoute>();

        }   

        //无路径时回退到 Idle 行为
        if(patrolRoute == null || !patrolRoute.HasPoints)
        {
            owner.ChangeState(E_EnemyState.Idle);
            return;
        }

        //设置NavMeshAgent 巡逻参数
        owner.NavAgent.speed = patrolSpeed;
        owner.NavAgent.stoppingDistance = arrivalDistance;
        owner.NavAgent.isStopped = false;

        //从最近的路近点开始巡逻
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

        //优先检测:发现玩家则切换到战斗
        if(owner.FindTarget() != null)
        {
            owner.ChangeState(E_EnemyState.CombatMovement);
            return;
        }

        //等待阶段，在路近点停留
        if (isWaiting)
        {
            waitTimer -= Time.deltaTime;
            if(waitTimer <= 0f)
            {
                isWaiting = false;
                //获得下一个路近点
                currentPoint = patrolRoute.GetNextPoint();
                SetDestination(owner);
            }
            return;
        }
        
        //移动阶段 检查是否达到路近点
        if(!owner.NavAgent.pathPending && owner.NavAgent.remainingDistance <= arrivalDistance)
        {
            //如果路近点有等待时间，进入等待
            if(currentPoint != null && currentPoint.WaiteTime > 0f)
            {
                isWaiting = true;
                waitTimer = currentPoint.WaiteTime;
                owner.NavAgent.isStopped = true;
            }
            else
            {
                //立即前往下一个路近点
                currentPoint = patrolRoute.GetNextPoint();
                SetDestination(owner);
            }
        }
    }

    //退出巡逻状态
    public override void Exit()
    {
        EnemyController owner = GetComponent<EnemyController>();

        //恢复默认速度
        owner.NavAgent.speed = 3.5f;
        owner.NavAgent.isStopped = false;
    }

    //设置NavMeshAgent 目标位置
    private void SetDestination(EnemyController owner)
    {
        if(currentPoint != null)
        {
            owner.NavAgent.isStopped = false;
            owner.NavAgent.SetDestination(currentPoint.transform.position);
        }
    }
}

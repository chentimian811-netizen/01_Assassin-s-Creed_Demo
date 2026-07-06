using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 巡逻路径配置 —— 定义一组路径点和巡逻模式
/// 挂载到敌人或路径管理物体上
/// </summary>
public class PatrolPoute : MonoBehaviour
{
    //巡逻模式枚举
    public enum E_PatrolMode
    {
        Loop,       //循环
        PingPong,   //折返
        Random,     //随机:随机选择下一个路近点（不重复上一个)
    }

    //巡逻点路径点列表
    [SerializeField] private List<PatrolPiont> patrolPionts = new List<PatrolPiont>();

    //当前的巡逻模式
    [SerializeField] private E_PatrolMode patrolMode = E_PatrolMode.Loop;

    //当前路近点的索引值
    private int currentIndex = 0;

    //折返模式的方向标记（1=正向 -1=反向)
    private int pingPongDirection = 1;

    //路近点的数量
    public int PointCount => patrolPionts.Count;

    //是否有 有效的路径点
    public bool HasPoints => patrolPionts.Count > 0;

    //从当前的路径点 推进到下一个
    public PatrolPiont GetNextPoint()
    {
        if(!HasPoints) return null;
        PatrolPiont point = patrolPionts[currentIndex];

        //根据巡逻模式计算下一个索引
        switch (patrolMode)
        {
            case E_PatrolMode.Loop:
                currentIndex = (currentIndex + 1) % patrolPionts.Count;
                break;

            case E_PatrolMode.PingPong:
                currentIndex += pingPongDirection;

                //到达两端时反转方向
                if(currentIndex >= patrolPionts.Count -1 || currentIndex <= 0)
                {
                    pingPongDirection *= -1;
                }

                //确保索引值不越界
                currentIndex = Mathf.Clamp(currentIndex,0,patrolPionts.Count -1);
                break;
            
            case E_PatrolMode.Random:
                if(patrolPionts.Count > 1)
                {
                    int newIndex;

                    //确保不连续选中同一个点
                    do
                    {
                        newIndex = Random.Range(0,patrolPionts.Count);
                    } while (newIndex == currentIndex);
                    currentIndex = newIndex;
                }
                break;
        }
        return point;
    }

    //获取最近的路径点索引(用于敌人初始位置的匹配)
    public int GetNearestPointIndex(Vector3 position)
    {
        int nearest = 0;
        float minDist = float.MaxValue;

        for(int i = 0 ; i < patrolPionts.Count; i++)
        {
            if(patrolPionts[i] == null) continue;
            float dist = Vector3.Distance(position,patrolPionts[i].transform.position);
            if(dist < minDist)
            {
                minDist = dist;
                nearest = i;
            }
        }

        return nearest;
    }

    //设置起始点为里指定地点位置最近的路径点
    public void SetNearestAsStart(Vector3 position)
    {
        currentIndex = GetNearestPointIndex(position);
    }

    //在场景视图中绘制路径连线，方便调试
    private void OnDrawGizmos()
    {
        if(patrolPionts == null || patrolPionts.Count < 2) return;

        Gizmos.color = Color.yellow;
        for(int i = 0; i < patrolPionts.Count; i++)
        {
            if(patrolPionts[i] == null )continue;

            //画点之间的连线
            int nextIndex = (i + 1) % patrolPionts.Count;
            if(patrolPionts[nextIndex] == null) continue;

            Gizmos.DrawLine(
                patrolPionts[i].transform.position,
                patrolPionts[nextIndex].transform.position
            );
        }       
    }
}

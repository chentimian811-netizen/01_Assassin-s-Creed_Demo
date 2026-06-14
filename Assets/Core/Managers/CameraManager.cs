using Cinemachine;
using UnityEngine;

/// <summary>
/// 摄像机管理器 —— 单例，集中管理所有 Cinemachine 相关操作
/// </summary>
public class CameraManager : MonoBehaviour
{
    //单例实例
    public static CameraManager Instance {get;private set;}

    //负责驱动实际相机
    public CinemachineBrain cmBrain;

    //自由观察相机
    public CinemachineFreeLook freeLook;

    //获取主相机的Transform，计算角色移动方向
    public Transform MainCameraTransform => cmBrain.transform;

    //玩家模型Transform
    public Transform playerModel;

    private void Awake()
    {
        if(Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    /// <summary>
    /// 重置自由观察相机视角到角色背后
    /// Y轴回到中间(0.5)，X轴对齐角色朝向
    /// </summary>
    public void ResetFreeLookCamera()
    {
        if (freeLook == null) return;

        // Y轴 0.5 = 中间视角（不仰不俯）
        freeLook.m_YAxis.Value = 0.5f;
        // X轴对齐角色当前朝向，这样相机就自动转到角色背后
        if(playerModel != null)
        {
            freeLook.m_XAxis.Value = playerModel.eulerAngles.y;
        }
    }


    /// <summary>
    /// 锁定敌相机时，冻结水平轴输入（不让鼠标转动水平方向）
    /// </summary>
    public void LockFreeLookXAxis()
    {
        if (freeLook == null) return;

        freeLook.m_XAxis.m_InputAxisName = "";
        freeLook.m_XAxis.m_InputAxisValue = 0f;
    }


    /// <summary>
    /// 解锁敌相机时，恢复水平轴和垂直轴的鼠标输入
    /// </summary>
    public void UnlockFreeLookAxes()
    {
        if (freeLook == null) return;

        freeLook.m_XAxis.m_InputAxisName = "Mouse X";
        freeLook.m_YAxis.m_InputAxisName = "Mouse Y";
    }


    /// <summary>
    /// 修改 FreeLook 的跟随目标和看向目标
    /// 用于过场动画或切换观察对象
    /// </summary>
    public void SetFollowAndLookAt(Transform target)
    {
        if (freeLook == null) return;

        freeLook.Follow = target;
        freeLook.m_LookAt = target;
    }
}

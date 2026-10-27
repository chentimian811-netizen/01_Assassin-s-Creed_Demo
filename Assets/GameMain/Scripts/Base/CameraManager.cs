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

    public CinemachineInputProvider cmInputProvider;

    [Header("游戏相机（新几何跟随 Rig）")]
    [Tooltip("参考手感相机控制器。留空则全部退回旧 FreeLook 逻辑，可用于快速 A/B 与回退")]
    [SerializeField] ACCameraRigController m_GameplayRig;

    /// <summary>统一暴露当前跟随锚点，避免其他系统继续依赖旧 FreeLook 的 LookAt。</summary>
    public Transform FollowTarget
    {
        get
        {
            if (m_GameplayRig != null)
                return m_GameplayRig.FollowPoint;
            return freeLook != null ? freeLook.m_LookAt : null;
        }
    }

    [Header("屏幕震动设置")]
    [Tooltip("震动信号源（CinemachineImpulseSource组件）")]
    [SerializeField] CinemachineImpulseSource impulseSource;

    [Tooltip("默认震动强度")]
    [SerializeField] float defaultForce = 1f;

    [SerializeField] float shakeCooldown = 0.5f;

    float lastShakeTime = -1f;

    private void Awake()
    {
        if(Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if(impulseSource == null)
        {
            impulseSource = GetComponent<CinemachineImpulseSource>();
        }
    }

    /// <summary>
    /// 重置自由观察相机视角到角色背后。
    /// 新方案只复位 Yaw（保留当前 Pitch）；没有新 Rig 时走旧 FreeLook 逻辑（Y 回 0.5、X 对齐角色）。
    /// </summary>
    public void ResetFreeLookCamera()
    {
        if (m_GameplayRig != null)
        {
            if (playerModel != null)
                m_GameplayRig.ResetCamera(playerModel.eulerAngles.y);
            return;
        }

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
    /// 进入/离开游戏时统一切换相机，避免新旧两台同时竞争 Brain。
    /// 清空 m_GameplayRig 引用即可回退旧 FreeLook。
    /// </summary>
    public void SetGameplayCameraActive(bool value)
    {
        if (m_GameplayRig != null)
        {
            m_GameplayRig.gameObject.SetActive(value);
            // 新 Rig 在用时旧 FreeLook 必须关掉，否则 CinemachineInputProvider 会重复消费鼠标
            if (freeLook != null)
                freeLook.gameObject.SetActive(false);
        }
        else if (freeLook != null)
        {
            freeLook.gameObject.SetActive(value);
        }
    }

    /// <summary>右键瞄准：切到右肩机位。</summary>
    public void SetAiming(bool value)
    {
        if (m_GameplayRig != null)
            m_GameplayRig.SetAiming(value);
    }

    /// <summary>锁定敌人：把机位对准敌人身上的观察点。</summary>
    public void SetLockTarget(Transform target)
    {
        if (m_GameplayRig != null)
        {
            m_GameplayRig.SetLockTarget(target);
        }
        else if (target != null)
        {
            LockFreeLookXAxis();
        }
        else
        {
            UnlockFreeLookAxes();
        }
    }

    /// <summary>解锁：恢复自由视角。</summary>
    public void ClearLockTarget()
    {
        if (m_GameplayRig != null)
            m_GameplayRig.ClearLockTarget();
        else
            UnlockFreeLookAxes();
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
    /// 修改跟随目标和看向目标
    /// 新方案只换观察点（机位朝向由控制器决定）；没有新 Rig 时改旧 FreeLook 的 Follow/LookAt。
    /// </summary>
    public void SetFollowAndLookAt(Transform target)
    {
        if (m_GameplayRig != null)
        {
            m_GameplayRig.SetFollowPoint(target);
            return;
        }

        if (freeLook == null) return;

        freeLook.Follow = target;
        freeLook.m_LookAt = target;
    }

    //触发屏幕震动效果
    public void ShakeScreen()
    {   
        if(Time.time - lastShakeTime < shakeCooldown)
        {
            return;
        }
        lastShakeTime = Time.time;

        ShakeScreen(defaultForce);
    }

    public void ShakeScreen(float force)
    {
        if(impulseSource == null)
        {
            return;
        }

        //生成震动信号 velocity表示震动方向和强度
        Vector3 velocity = new Vector3(
            Random.Range(-1f,1f),
            Random.Range(-1,1f),
            0f
        ).normalized * force;

        impulseSource.GenerateImpulse(velocity);
    }

    //在指定的位置触发屏幕震动效果
    public void ShakeScreenAtPosition(Vector3 postion,float force = -1f)
    {
        if(impulseSource == null)
        {
            return;
        }

        float shakeForce = force > 0 ? force : defaultForce;

        impulseSource.GenerateImpulseAt(postion,Vector3.one * shakeForce);
    }

     public void SetLookEnabled(bool enabled)
        {
            // 新方案下输入由控制器统一读，禁用后不再消费鼠标/摇杆；
            // 旧方案关掉 InputProvider，两套都保留，方便回退。
            if (m_GameplayRig != null)
            {
                m_GameplayRig.SetInputEnabled(enabled);
                return;
            }

            if (cmInputProvider != null)
                cmInputProvider.enabled = enabled;
        }
}

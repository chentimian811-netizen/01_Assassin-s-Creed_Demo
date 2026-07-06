using UnityEngine;
using Cinemachine;

/// <summary>
/// 主菜单相机控制器 - 固定镜头 + 轻微摇晃效果
/// </summary>
public class MainMenuCamera : MonoBehaviour
{
    [Header("相机设置")]
    [SerializeField] private CinemachineVirtualCamera menuCamera;  // 主菜单虚拟相机
    [SerializeField] private Transform lookAtTarget;               // 相机注视目标

    [Header("摇晃参数")]
    [SerializeField] private bool enableNoise = true;              // 是否启用摇晃
    [SerializeField] private float noiseAmplitude = 0.2f;          // 摇晃幅度（越小越轻微）
    [SerializeField] private float noiseFrequency = 0.3f;          // 摇晃频率（越慢越自然）

    [Header("过渡设置")]
    [SerializeField] private float transitionDuration = 2f;        // 过渡到游戏的时间

    private bool isTransitioning = false;

    private void Start()
    {
        SetupMenuCamera();
    }

    /// <summary>
    /// 设置主菜单相机的Cinemachine参数
    /// </summary>
    private void SetupMenuCamera()
    {
        if(menuCamera == null)
        {
            menuCamera = GetComponent<CinemachineVirtualCamera>();
        }

        if(menuCamera != null)
        {
            // 只有在 m_LookAt 还没被设置过的情况下才使用序列化字段
        // 避免覆盖 GameManager 通过 SetLookAtTarget() 动态设置的目标
        if(menuCamera.m_LookAt == null && lookAtTarget != null)
        {
            menuCamera.m_LookAt = lookAtTarget;
        }

            if (enableNoise)
            {
                ConfigureNoise();
            }
        }
    }
    /// <summary>
    /// 设置菜单相机的注视目标（供GameManager调用）
    /// </summary>
    public void SetLookAtTarget(Transform target)
    {
        lookAtTarget = target;
        if(menuCamera != null)
        {
            menuCamera.m_LookAt = target;
        }
    }

    /// <summary>
    /// 配置Cinemachine Noise参数
    /// </summary>
    private void ConfigureNoise()
    {
        var noise = menuCamera.GetCinemachineComponent<CinemachineBasicMultiChannelPerlin>();
        
        if (noise != null)
        {
            noise.m_AmplitudeGain = noiseAmplitude;
            noise.m_FrequencyGain = noiseFrequency;
        }
        else
        {
            Debug.LogWarning("相机缺少 CinemachineBasicMultiChannelPerlin 组件，请在VirtualCamera的Noise中添加");
        }
    }

    /// <summary>
    /// 启用/禁用相机摇晃
    /// </summary>
    public void SetNoiseEnabled(bool enabled)
    {
        enableNoise = enabled;
        var noise = menuCamera.GetCinemachineComponent<CinemachineBasicMultiChannelPerlin>();

        if(noise != null)
        {
            noise.m_AmplitudeGain = enabled?noiseAmplitude:0f;
        }
    }

    /// <summary>
    /// 过渡到游戏模式
    /// </summary>
    public void TransitionToGameplay()
    {
        if (!isTransitioning)
        {
            StartCoroutine(TransitionToGameplayCoroutine());
        }
    }
    
    /// <summary>
    /// 过渡到游戏相机的协程
    /// 只负责禁用菜单相机，FreeLook 的激活由 GameManager.StartGame() 处理
    /// </summary>
    private System.Collections.IEnumerator TransitionToGameplayCoroutine()
    {
        isTransitioning = true;

        // 等一帧，确保淡出动画完成
        yield return null;

        // 禁用菜单虚拟相机，让出控制权
        if(menuCamera != null)
        {
            menuCamera.gameObject.SetActive(false);
        }

        isTransitioning = false;
    }
    
}
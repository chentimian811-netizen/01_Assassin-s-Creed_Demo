using Cinemachine;
using UnityEngine;

/// <summary>
/// 把 ACCameraRigController 算好的机位写入 VCam。
/// 回调挂在 Aim 阶段之后：内置 Body/Aim 都是 Do Nothing，之后只剩 Noise/Impulse 与 Brain 混合，
/// 所以写入的 RawPosition/RawOrientation 不会被别的组件覆盖，也不会绕过震屏。
/// </summary>
[RequireComponent(typeof(CinemachineVirtualCamera))]
public sealed class ACCameraStateExtension : CinemachineExtension
{
    [Tooltip("机位来源控制器，通常挂在父物体 GameplayCameraRig 上")]
    [SerializeField] private ACCameraRigController m_Controller;
    [Tooltip("本 VCam 代表哪种机位")]
    [SerializeField] private ACCameraRigController.ViewMode m_Mode = ACCameraRigController.ViewMode.Normal;

    /// <summary>在编辑器里挂上扩展时自动找父级控制器，省一次手拖。</summary>
    private void Reset()
    {
        m_Controller = GetComponentInParent<ACCameraRigController>();
    }

    protected override void PostPipelineStageCallback(
        CinemachineVirtualCameraBase vcam,
        CinemachineCore.Stage stage,
        ref CameraState state,
        float deltaTime)
    {
        if (stage != CinemachineCore.Stage.Aim
            || m_Controller == null
            || !m_Controller.IsInitialized)
        {
            return;
        }

        ACCameraRigController.CameraFrame frame = m_Controller.GetFrame(m_Mode);
        state.RawPosition = frame.Position;
        state.RawOrientation = frame.Rotation;
    }
}

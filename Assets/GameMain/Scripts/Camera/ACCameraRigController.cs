using Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 参考相机几何控制器：统一计算自由、锁定、复位和右肩瞄准机位。
/// 只负责生成相机状态，不直接移动 Main Camera，最终驱动仍由 CinemachineBrain 完成。
///
/// 与旧 FreeLook 的关键差别：这里保存「上一帧无碰撞理想机位」，自由模式先用它反推 Yaw，
/// 再叠加本帧输入，所以横移时镜头会自然环绕，不需要 Body/Composer 阻尼去模拟。
/// </summary>
[DisallowMultipleComponent]
public sealed class ACCameraRigController : MonoBehaviour
{
    /// <summary>两种机位：普通第三人称 / 右肩瞄准。</summary>
    public enum ViewMode
    {
        Normal,
        Shoot
    }

    /// <summary>提供给 Cinemachine 扩展的一帧机位。</summary>
    public struct CameraFrame
    {
        public Vector3 Position;
        public Quaternion Rotation;

        public CameraFrame(Vector3 position, Quaternion rotation)
        {
            Position = position;
            Rotation = rotation;
        }
    }

    [Header("场景引用")]
    [Tooltip("玩家根对象下的相机观察点（CameraWatchPoint），局部高度 2.06")]
    [SerializeField] private Transform m_FollowPoint;
    [Tooltip("真正的 Main Camera，用来取 aspect 与兜底 nearClip")]
    [SerializeField] private Camera m_TargetCamera;
    [Tooltip("Main Camera 上的 CinemachineBrain，运行时注入 0.2s 混合规则")]
    [SerializeField] private CinemachineBrain m_Brain;
    [Tooltip("普通机位 VCam")]
    [SerializeField] private CinemachineVirtualCamera m_NormalCamera;
    [Tooltip("右肩瞄准机位 VCam")]
    [SerializeField] private CinemachineVirtualCamera m_ShootCamera;

    [Header("视角输入")]
    [Tooltip("由 CameraManager.SetLookEnabled 控制；关闭后不再消费鼠标/摇杆")]
    [SerializeField] private bool m_InputEnabled = true;
    [Tooltip("鼠标每个原始像素增量旋转多少度；鼠标增量不能乘 deltaTime")]
    [SerializeField] private float m_MouseDegreesPerPixel = 0.3f;
    [Tooltip("手柄右摇杆死区")]
    [SerializeField] private float m_PadDeadzone = 0.6f;
    [Tooltip("手柄内部速度换算成角速度的倍率")]
    [SerializeField] private float m_PadGain = 200f;
    [Tooltip("手柄内部速度每帧增长量（乘 deltaTime）")]
    [SerializeField] private float m_PadAcceleration = 5f;
    [Tooltip("手柄内部速度上限，乘 Gain 后为 300°/s")]
    [SerializeField] private float m_MaxPadYawSpeed = 1.5f;
    [Tooltip("手柄内部速度上限，乘 Gain 后为 200°/s")]
    [SerializeField] private float m_MaxPadPitchSpeed = 1f;

    [Header("普通视角与碰撞")]
    [Tooltip("普通机位臂长；无碰撞时实际相机再退一个 nearClip")]
    [SerializeField] private float m_NormalDistance = 5f;
    [Tooltip("俯仰范围 ±MaxPitch")]
    [SerializeField] private float m_MaxPitch = 75f;
    [SerializeField] private float m_InitialPitch = 0f;
    [Tooltip("相机碰撞层。本工程没有任何物体在 Obstacles(11)，首版同时包含 Default(0)")]
    [SerializeField] private LayerMask m_CollisionMask = (1 << 0) | (1 << 11);
    [Tooltip("近裁剪面盒外扩余量")]
    [SerializeField] private float m_CollisionMargin = 0.05f;
    [Tooltip("近裁剪面盒的厚度半值")]
    [SerializeField] private float m_CollisionHalfDepth = 0.02f;

    [Header("锁定与复位")]
    [Tooltip("锁定角响应（参考 0.08 / 0.02s 的帧率无关等效值）")]
    [SerializeField] private float m_LockResponse = 4f;
    [Tooltip("复位角响应；只复位 Yaw")]
    [SerializeField] private float m_ResetResponse = 5f;
    [Tooltip("锁定目标 Pitch 额外偏移，形成略俯视构图")]
    [SerializeField] private float m_LockPitchOffset = 15f;
    [SerializeField] private float m_LockMinStep = 0.01f;
    [SerializeField] private float m_ResetMinStep = 0.5f;

    [Header("右肩瞄准")]
    [SerializeField] private float m_ShootSideDistance = 1f;
    [SerializeField] private float m_ShootMinBackDistance = 0.25f;
    [SerializeField] private float m_ShootMaxBackDistance = 0.5f;
    [Tooltip("右侧空间不足时向上抬升的最大距离")]
    [SerializeField] private float m_ShootLiftDistance = 1.1f;
    [SerializeField] private float m_ModeBlendTime = 0.2f;
    [SerializeField] private int m_ActivePriority = 20;
    [SerializeField] private int m_InactivePriority = 10;

    // 当前机位模式
    private ViewMode m_Mode = ViewMode.Normal;
    // 锁定目标的观察点（敌人身上的 CameraAimPoint）
    private Transform m_LockTarget;
    // 两台 VCam 每帧的机位，Brain 双向混合时两边都要是最新状态
    private CameraFrame m_NormalFrame;
    private CameraFrame m_ShootFrame;

    private float m_Yaw;
    private float m_Pitch;
    private float m_PadYawSpeed;
    private float m_PadPitchSpeed;
    // 上一帧的无碰撞理想机位，自由模式靠它反推 Yaw
    private Vector3 m_NoCollisionPosition;

    private bool m_Resetting;
    private float m_ResetYaw;
    private bool m_Initialized;
    // 缺引用只报一次，避免每帧刷屏
    private bool m_InitErrorLogged;

    private CinemachineBlenderSettings m_RuntimeBlenderSettings;
    private CinemachineBlenderSettings m_PreviousBlenderSettings;

    public bool IsInitialized => m_Initialized;
    public Transform FollowPoint => m_FollowPoint;
    public float Yaw => m_Yaw;
    public float Pitch => m_Pitch;
    public bool IsAiming => m_Mode == ViewMode.Shoot;

    private Quaternion CurrentRotation => Quaternion.Euler(m_Pitch, m_Yaw, 0f);

    private void Awake()
    {
        if (m_TargetCamera == null)
            m_TargetCamera = Camera.main;
    }

    private void Start()
    {
        if (!Initialize())
        {
            enabled = false;
            return;
        }

        ConfigureBlends();
        ApplyPriorities();
        UpdateFrames();
    }

    private void Update()
    {
        if (!m_Initialized && !Initialize())
            return;

        // 跟随目标被销毁（玩家回收/换场景）时不再刷新机位，等外部重新 SetFollowPoint
        if (m_FollowPoint == null)
            return;

        // 用缩放时间是有意的：卡肉（timeScale=0.05）与暂停（=0）期间机位也跟着近乎停住，
        // 手感上像「打击顿住」。把 hit_stop 时长调大之前要重新评估这里是否该用 unscaledDeltaTime。
        float deltaTime = Time.deltaTime;
        Vector3 watchPoint = m_FollowPoint.position;

        // 输入每帧最多消费一次；参考在 0.2s 切换期重复读两次鼠标属于不应继承的副作用。
        if (m_Mode == ViewMode.Shoot)
        {
            if (m_InputEnabled)
                ApplyLookInput(deltaTime);
        }
        else if (m_LockTarget != null)
        {
            UpdateLockedOrientation(watchPoint, deltaTime);
        }
        else
        {
            UpdateFreeOrientation(watchPoint, deltaTime);
        }

        m_Pitch = Mathf.Clamp(m_Pitch, -m_MaxPitch, m_MaxPitch);
        m_Yaw = Mathf.Repeat(m_Yaw + 180f, 360f) - 180f;
        UpdateFrames();
    }

    /// <summary>首帧以角色朝向建立无碰撞机位，避免相机从场景编辑位置跳过来。</summary>
    private bool Initialize()
    {
        if (m_Initialized)
            return true;

        if (m_TargetCamera == null)
            m_TargetCamera = Camera.main;

        if (m_FollowPoint == null || m_TargetCamera == null || m_Brain == null
            || m_NormalCamera == null || m_ShootCamera == null)
        {
            if (!m_InitErrorLogged)
            {
                m_InitErrorLogged = true;
                Debug.LogError(
                    "[ACCameraRigController] 缺少 FollowPoint/MainCamera/Brain/两台 VCam 引用，"
                    + "请检查 GameplayCameraRig 上的绑定。", this);
            }
            return false;
        }

        m_Yaw = m_FollowPoint.eulerAngles.y;
        m_Pitch = Mathf.Clamp(m_InitialPitch, -m_MaxPitch, m_MaxPitch);
        m_NoCollisionPosition = m_FollowPoint.position
            - CurrentRotation * Vector3.forward * m_NormalDistance;
        m_Initialized = true;
        return true;
    }

    /// <summary>自由模式先由上一帧理想机位反推 Yaw，再叠加本帧输入。</summary>
    private void UpdateFreeOrientation(Vector3 watchPoint, float deltaTime)
    {
        if (m_Resetting)
        {
            float ratio = Mathf.Clamp01(m_ResetResponse * deltaTime);
            m_Yaw = MoveAngleByRatio(m_Yaw, m_ResetYaw, ratio, m_ResetMinStep);
            if (Mathf.Abs(Mathf.DeltaAngle(m_Yaw, m_ResetYaw)) < 0.0001f)
            {
                m_Yaw = m_ResetYaw;
                m_Resetting = false;
            }
            return;
        }

        Vector3 toWatchPoint = watchPoint - m_NoCollisionPosition;
        toWatchPoint.y = 0f;
        if (toWatchPoint.sqrMagnitude > 0.000001f)
            m_Yaw = Mathf.Atan2(toWatchPoint.x, toWatchPoint.z) * Mathf.Rad2Deg;

        if (m_InputEnabled)
            ApplyLookInput(deltaTime);
    }

    /// <summary>锁定时忽略自由输入，持续追踪敌人的胸口观察点。</summary>
    private void UpdateLockedOrientation(Vector3 watchPoint, float deltaTime)
    {
        Vector3 toTarget = m_LockTarget.position - watchPoint;
        float magnitude = toTarget.magnitude;
        if (magnitude < 0.001f)
            return;

        float targetPitch = -Mathf.Asin(Mathf.Clamp(toTarget.y / magnitude, -1f, 1f))
            * Mathf.Rad2Deg + m_LockPitchOffset;
        targetPitch = Mathf.Clamp(targetPitch, -m_MaxPitch, m_MaxPitch);

        float ratio = Mathf.Clamp01(m_LockResponse * deltaTime);
        Vector3 flatTarget = toTarget;
        flatTarget.y = 0f;
        if (flatTarget.sqrMagnitude > 0.000001f)
        {
            float targetYaw = Mathf.Atan2(flatTarget.x, flatTarget.z) * Mathf.Rad2Deg;
            m_Yaw = MoveAngleByRatio(m_Yaw, targetYaw, ratio, m_LockMinStep);
        }
        m_Pitch = MoveAngleByRatio(m_Pitch, targetPitch, ratio, m_LockMinStep);
    }

    /// <summary>鼠标按帧增量，手柄按秒速度，二者不能共用同一个时间公式。</summary>
    private void ApplyLookInput(float deltaTime)
    {
        Vector2 mouseDelta = Mouse.current != null
            ? Mouse.current.delta.ReadValue()
            : Vector2.zero;
        Vector2 pad = Gamepad.current != null
            ? Gamepad.current.rightStick.ReadValue()
            : Vector2.zero;

        float acceleration = m_PadAcceleration * deltaTime;

        if (pad.x > m_PadDeadzone)
            m_PadYawSpeed = Mathf.Min(m_MaxPadYawSpeed, m_PadYawSpeed + acceleration);
        else if (pad.x < -m_PadDeadzone)
            m_PadYawSpeed = Mathf.Max(-m_MaxPadYawSpeed, m_PadYawSpeed - acceleration);
        else
            m_PadYawSpeed = 0f;

        if (pad.y > m_PadDeadzone)
            m_PadPitchSpeed = Mathf.Min(m_MaxPadPitchSpeed, m_PadPitchSpeed + acceleration);
        else if (pad.y < -m_PadDeadzone)
            m_PadPitchSpeed = Mathf.Max(-m_MaxPadPitchSpeed, m_PadPitchSpeed - acceleration);
        else
            m_PadPitchSpeed = 0f;

        float deltaYaw = mouseDelta.x * m_MouseDegreesPerPixel
            + m_PadYawSpeed * m_PadGain * deltaTime;
        float deltaPitch = mouseDelta.y * m_MouseDegreesPerPixel
            + m_PadPitchSpeed * m_PadGain * deltaTime;

        m_Yaw += deltaYaw;
        m_Pitch -= deltaPitch;
    }

    /// <summary>每帧同时准备两个机位，保证 Brain 双向混合时两边都是当前状态。</summary>
    private void UpdateFrames()
    {
        Vector3 watchPoint = m_FollowPoint.position;
        Quaternion rotation = CurrentRotation;
        Vector3 collisionHalfExtents = GetCollisionHalfExtents();
        float nearClip = GetNearClip();

        UpdateNormalFrame(watchPoint, rotation, collisionHalfExtents, nearClip);
        UpdateShootFrame(watchPoint, rotation, collisionHalfExtents, nearClip);
    }

    private void UpdateNormalFrame(
        Vector3 watchPoint, Quaternion rotation, Vector3 halfExtents, float nearClip)
    {
        Vector3 watchDir = rotation * Vector3.forward;
        float distance = m_NormalDistance;

        if (Physics.BoxCast(
            watchPoint, halfExtents, -watchDir, out RaycastHit hit,
            rotation, m_NormalDistance, m_CollisionMask, QueryTriggerInteraction.Ignore))
        {
            distance = hit.distance;
        }

        m_NormalFrame = new CameraFrame(
            watchPoint - watchDir * (distance + nearClip), rotation);

        // 自动环绕必须使用无碰撞、未追加 NearClip 的理想机位。
        m_NoCollisionPosition = watchPoint - watchDir * m_NormalDistance;
    }

    private void UpdateShootFrame(
        Vector3 watchPoint, Quaternion rotation, Vector3 halfExtents, float nearClip)
    {
        Quaternion yawRotation = Quaternion.Euler(0f, m_Yaw, 0f);
        float pitchFactor = 1f - Mathf.Abs(m_Pitch) / m_MaxPitch;
        float backDistance = Mathf.Lerp(
            m_ShootMinBackDistance, m_ShootMaxBackDistance, pitchFactor);
        Vector3 backPosition = watchPoint + yawRotation * Vector3.back * backDistance;

        Vector3 rightDirection = yawRotation * Vector3.right;
        float rightDistance = m_ShootSideDistance;
        if (Physics.BoxCast(
            backPosition, halfExtents, rightDirection, out RaycastHit hit,
            rotation, rightDistance, m_CollisionMask, QueryTriggerInteraction.Ignore))
        {
            rightDistance = hit.distance;
        }

        float rightRatio = m_ShootSideDistance > 0.0001f
            ? rightDistance / m_ShootSideDistance
            : 0f;
        if (rightRatio < 0.5f)
        {
            backPosition += Vector3.up
                * (1f - rightRatio * 2f) * m_ShootLiftDistance;
        }

        Vector3 position = backPosition + rightDirection * rightDistance
            - rotation * Vector3.forward * nearClip;
        m_ShootFrame = new CameraFrame(position, rotation);
    }

    /// <summary>按近裁剪面而不是按相机中心点估算防穿墙盒。</summary>
    private Vector3 GetCollisionHalfExtents()
    {
        float fov = m_NormalCamera.m_Lens.FieldOfView;
        float nearClip = GetNearClip();
        float halfY = nearClip * Mathf.Tan(fov * 0.5f * Mathf.Deg2Rad)
            + m_CollisionMargin;
        float aspect = m_TargetCamera.aspect > 0.01f ? m_TargetCamera.aspect : 16f / 9f;
        return new Vector3(
            halfY * aspect + m_CollisionMargin,
            halfY,
            m_CollisionHalfDepth);
    }

    private float GetNearClip()
    {
        return m_NormalCamera != null
            ? m_NormalCamera.m_Lens.NearClipPlane
            : m_TargetCamera.nearClipPlane;
    }

    /// <summary>与参考 AngleTowardsByDiff 同义：按比例接近，近处用最小步长收尾。</summary>
    private static float MoveAngleByRatio(float from, float to, float ratio, float minStep)
    {
        float difference = Mathf.DeltaAngle(from, to);
        float distance = Mathf.Abs(difference);
        if (distance < 0.000001f)
            return to;

        float step = Mathf.Max(distance * Mathf.Abs(ratio), Mathf.Abs(minStep));
        return step >= distance ? to : from + Mathf.Sign(difference) * step;
    }

    public CameraFrame GetFrame(ViewMode mode)
    {
        return mode == ViewMode.Shoot ? m_ShootFrame : m_NormalFrame;
    }

    public void SetAiming(bool aiming)
    {
        SetViewMode(aiming ? ViewMode.Shoot : ViewMode.Normal);
    }

    public void SetViewMode(ViewMode mode)
    {
        if (m_Mode == mode)
            return;

        m_Mode = mode;
        if (mode == ViewMode.Shoot)
        {
            // 与参考 AimState 一致：进入右肩瞄准后不再维持近战锁定。
            m_LockTarget = null;
            m_Resetting = false;
        }
        else if (m_Initialized && m_FollowPoint != null)
        {
            m_NoCollisionPosition = m_FollowPoint.position
                - CurrentRotation * Vector3.forward * m_NormalDistance;
        }

        ApplyPriorities();
    }

    public void SetLockTarget(Transform target)
    {
        if (target != null && m_Mode == ViewMode.Shoot)
            SetViewMode(ViewMode.Normal);

        m_LockTarget = target;
        m_Resetting = false;
    }

    public void ClearLockTarget()
    {
        m_LockTarget = null;
    }

    /// <summary>只复位水平朝向，保留玩家当前俯仰角。</summary>
    public void ResetCamera(float yaw)
    {
        if (m_Mode != ViewMode.Normal)
            return;

        m_LockTarget = null;
        m_ResetYaw = yaw;
        m_Resetting = true;
    }

    public void SetFollowPoint(Transform target)
    {
        m_FollowPoint = target;
        if (target == null)
        {
            m_Initialized = false;
            return;
        }

        if (m_Initialized)
        {
            m_NoCollisionPosition = target.position
                - CurrentRotation * Vector3.forward * m_NormalDistance;
        }
    }

    public void SetInputEnabled(bool value)
    {
        m_InputEnabled = value;
        if (!value)
        {
            // 失焦/暂停时把摇杆内部速度清零，避免恢复后补一段加速
            m_PadYawSpeed = 0f;
            m_PadPitchSpeed = 0f;
        }
    }

    private void ApplyPriorities()
    {
        if (m_NormalCamera == null || m_ShootCamera == null)
            return;

        bool normalActive = m_Mode == ViewMode.Normal;
        m_NormalCamera.Priority = normalActive ? m_ActivePriority : m_InactivePriority;
        m_ShootCamera.Priority = normalActive ? m_InactivePriority : m_ActivePriority;
    }

    /// <summary>只给两台游戏相机加混合，不改菜单/过场使用的默认 Cut。</summary>
    private void ConfigureBlends()
    {
        if (m_Brain.m_CustomBlends != null)
        {
            Debug.LogWarning(
                "[ACCameraRigController] Brain 已有 Custom Blends；请手工添加 "
                + "VCam_Normal / VCam_Shoot 双向 Linear 0.2s 规则。", this);
            return;
        }

        m_PreviousBlenderSettings = m_Brain.m_CustomBlends;
        m_RuntimeBlenderSettings = ScriptableObject.CreateInstance<CinemachineBlenderSettings>();
        var blend = new CinemachineBlendDefinition(
            m_ModeBlendTime > 0f
                ? CinemachineBlendDefinition.Style.Linear
                : CinemachineBlendDefinition.Style.Cut,
            m_ModeBlendTime);

        m_RuntimeBlenderSettings.m_CustomBlends = new[]
        {
            new CinemachineBlenderSettings.CustomBlend
            {
                m_From = m_NormalCamera.name,
                m_To = m_ShootCamera.name,
                m_Blend = blend
            },
            new CinemachineBlenderSettings.CustomBlend
            {
                m_From = m_ShootCamera.name,
                m_To = m_NormalCamera.name,
                m_Blend = blend
            }
        };
        m_Brain.m_CustomBlends = m_RuntimeBlenderSettings;
    }

    private void OnDestroy()
    {
        if (m_RuntimeBlenderSettings == null)
            return;

        if (m_Brain != null && m_Brain.m_CustomBlends == m_RuntimeBlenderSettings)
            m_Brain.m_CustomBlends = m_PreviousBlenderSettings;
        Destroy(m_RuntimeBlenderSettings);
    }
}

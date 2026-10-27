using Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 一次性施工工具：生成「参考相机手感」所需的场景层级与预制体锚点，并把所有引用绑好。
///
/// 为什么用生成器而不是手写 YAML：新增 Cinemachine 组件与扩展需要正确的 fileID/GUID 和
/// 大段序列化块，手写极易让场景/预制体打不开；生成器只操作「它自己创建的那棵子树」，可反复执行，
/// 也不会碰你手动加的物体（与 HUDPrefabBuilder 的处理方式一致）。
///
/// 生成内容：
///   CameraManager
///   └─ GameplayCameraRig（初始 inactive，由 ProcedureGame.SetGameplayCameraActive 激活）
///      ├─ VCam_Normal  [CinemachineVirtualCamera + ACCameraStateExtension(Mode=Normal)]
///      └─ VCam_Shoot   [CinemachineVirtualCamera + ACCameraStateExtension(Mode=Shoot)]
///   Male.prefab  → 根下 CameraWatchPoint (0, 2.06, 0)
///   Enemy.prefab → 根下 CameraAimPoint  (0, 1.35, 0)，并绑定 EnemyController.m_CameraAimPoint
///   同时：停用旧 FreeLook Camera（保留为回退备份）、CameraManager.m_GameplayRig 指向新控制器。
///
/// 菜单：
///   ACDemo/Camera/Build Gameplay Camera Rig     生成 / 刷新（幂等，可反复执行）
///   ACDemo/Camera/Validate Gameplay Camera Rig  只检查引用并打印报告，不改任何东西
/// 生成后记得 Ctrl+S 保存场景。
/// </summary>
public static class CameraRigBuilder
{
    private const string ScenePath = "Assets/GameMain/Scenes/TestScene.unity";
    private const string EnemyPrefabPath = "Assets/GameMain/Entities/Enemy/Enemy.prefab";

    private const string RigName = "GameplayCameraRig";
    private const string NormalName = "VCam_Normal";
    private const string ShootName = "VCam_Shoot";
    private const string WatchPointName = "CameraWatchPoint";
    private const string AimPointName = "CameraAimPoint";

    // 观察点高度：玩家 2.06 m 来自参考工程 Sight 节点；敌人 1.35 m 是针对本工程模型（CC 高 2.15、中心 0.77）的初始胸口高度
    private const float WatchPointHeight = 2.06f;
    private const float AimPointHeight = 1.35f;

    // 构图：参考 70° FOV / 0.12 近裁剪；Far 保持 5000
    private const float FieldOfView = 70f;
    private const float NearClip = 0.12f;
    private const float FarClip = 5000f;

    private const int NormalPriority = 20;
    private const int ShootPriority = 10;

    // 碰撞层：本工程没有任何物体在 Obstacles(11)（环境全在 Default(0)），只留 11 的话碰撞永不生效。
    // 首版取 Default(0) | Obstacles(11)：玩家(6)/敌人(7)/命中盒(8,9)/视野(10)/小地图(12) 仍被排除。
    private const int CollisionMask = (1 << 0) | (1 << 11);

    // ==================== 菜单 ====================

    [MenuItem("ACDemo/Camera/Build Gameplay Camera Rig", priority = 200)]
    public static void BuildMenu()
    {
        if (!Build())
        {
            EditorUtility.DisplayDialog("相机 Rig 生成失败", "详见 Console。", "好");
            return;
        }

        EditorUtility.DisplayDialog(
            "相机 Rig 已生成",
            "已生成 GameplayCameraRig + 两台 VCam，并给 Male/Enemy 预制体加了锚点。\n\n"
            + "下一步：\n1. Ctrl+S 保存场景（必须）\n2. 直接 Play 验证普通跟随与碰撞",
            "好");
    }

    [MenuItem("ACDemo/Camera/Validate Gameplay Camera Rig", priority = 201)]
    public static void ValidateMenu()
    {
        Validate();
    }

    // ==================== 主流程 ====================

    public static bool Build()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogError("[CameraRigBuilder] 请先退出 Play 模式再执行。");
            return false;
        }

        Scene scene = SceneManager.GetActiveScene();
        if (!string.Equals(scene.path, ScenePath))
        {
            Debug.LogError($"[CameraRigBuilder] 当前打开的场景是「{scene.path}」，请先打开 {ScenePath}。");
            return false;
        }

        CameraManager camManager = FindFirst<CameraManager>();
        if (camManager == null)
        {
            Debug.LogError("[CameraRigBuilder] 场景里找不到 CameraManager。");
            return false;
        }

        if (camManager.cmBrain == null)
        {
            Debug.LogError("[CameraRigBuilder] CameraManager.cmBrain 未绑定，无法配置与 VCam 的混合规则。");
            return false;
        }

        Camera mainCamera = camManager.cmBrain.GetComponent<Camera>();
        if (mainCamera == null)
        {
            Debug.LogError("[CameraRigBuilder] CinemachineBrain 所在的物体上没有 Camera 组件。");
            return false;
        }

        // 玩家根：优先用 CameraManager.playerModel（场景里就是 Male 实例根），否则退回 PlayerController 所在物体
        Transform playerRoot = camManager.playerModel;
        if (playerRoot == null)
        {
            PlayerController player = FindFirst<PlayerController>();
            playerRoot = player != null ? player.transform : null;
        }
        if (playerRoot == null)
        {
            Debug.LogError("[CameraRigBuilder] 找不到玩家根物体（CameraManager.playerModel 与 PlayerController 都为空）。");
            return false;
        }

        Transform watchPoint = EnsurePlayerWatchPoint(playerRoot);
        if (watchPoint == null)
            return false;

        EnsureEnemyAimPoint();

        Transform rigRoot = EnsureRig(camManager);
        ACCameraRigController controller = rigRoot.GetComponent<ACCameraRigController>();
        CinemachineVirtualCamera normalVcam = EnsureVcam(rigRoot, NormalName, NormalPriority, watchPoint);
        CinemachineVirtualCamera shootVcam = EnsureVcam(rigRoot, ShootName, ShootPriority, watchPoint);
        EnsureExtension(normalVcam, controller, ACCameraRigController.ViewMode.Normal);
        EnsureExtension(shootVcam, controller, ACCameraRigController.ViewMode.Shoot);
        BindController(controller, watchPoint, mainCamera, camManager.cmBrain, normalVcam, shootVcam);
        BindCameraManager(camManager, controller);
        DisableLegacyFreeLook(camManager);

        EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log("[CameraRigBuilder] 生成完成。请 Ctrl+S 保存场景，再 Play 验证普通跟随与碰撞。");
        Validate();
        return true;
    }

    // ==================== 预制体锚点 ====================

    private static Transform EnsurePlayerWatchPoint(Transform playerRoot)
    {
        string prefabPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(playerRoot);
        if (string.IsNullOrEmpty(prefabPath))
        {
            Debug.LogError($"[CameraRigBuilder] 玩家根「{playerRoot.name}」不是 Prefab 实例，无法在预制体上加锚点。");
            return null;
        }

        bool changed = false;
        GameObject contents = PrefabUtility.LoadPrefabContents(prefabPath);
        try
        {
            Transform anchor = contents.transform.Find(WatchPointName);
            if (anchor == null)
            {
                anchor = CreateAnchor(contents.transform, WatchPointName, WatchPointHeight);
                changed = true;
                Debug.Log($"[CameraRigBuilder] 已在 {prefabPath} 根下新建 {WatchPointName} (0, {WatchPointHeight}, 0)。");
            }
            else if (!IsExpectedLocalPosition(anchor, WatchPointHeight))
            {
                Debug.LogWarning(
                    $"[CameraRigBuilder] {WatchPointName} 当前局部坐标是 {anchor.localPosition}，"
                    + $"期望 (0, {WatchPointHeight}, 0)。工具不覆盖手工调整，请自行确认。");
            }

            if (changed)
                PrefabUtility.SaveAsPrefabAsset(contents, prefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(contents);
        }

        Transform sceneAnchor = playerRoot.Find(WatchPointName);
        if (sceneAnchor == null)
        {
            Debug.LogError(
                $"[CameraRigBuilder] {prefabPath} 已更新，但场景实例还没拿到 {WatchPointName}。"
                + "请保存场景后重新执行本工具（或重启 Unity 让它刷新预制体实例）。");
        }
        return sceneAnchor;
    }

    private static void EnsureEnemyAimPoint()
    {
        bool changed = false;
        GameObject contents = PrefabUtility.LoadPrefabContents(EnemyPrefabPath);
        try
        {
            Transform anchor = contents.transform.Find(AimPointName);
            if (anchor == null)
            {
                anchor = CreateAnchor(contents.transform, AimPointName, AimPointHeight);
                changed = true;
                Debug.Log($"[CameraRigBuilder] 已在 {EnemyPrefabPath} 根下新建 {AimPointName} (0, {AimPointHeight}, 0)。");
            }

            EnemyController enemy = contents.GetComponentInChildren<EnemyController>(true);
            if (enemy == null)
            {
                Debug.LogError($"[CameraRigBuilder] {EnemyPrefabPath} 里找不到 EnemyController，无法绑定 m_CameraAimPoint。");
            }
            else
            {
                SerializedObject serializedEnemy = new SerializedObject(enemy);
                SerializedProperty aimPointProperty = serializedEnemy.FindProperty("m_CameraAimPoint");
                if (aimPointProperty == null)
                {
                    Debug.LogError(
                        "[CameraRigBuilder] EnemyController 上没有 m_CameraAimPoint 字段。"
                        + "请先切回 Unity 让 Assembly-CSharp 重新编译，再执行本工具。");
                }
                else if (aimPointProperty.objectReferenceValue != anchor)
                {
                    aimPointProperty.objectReferenceValue = anchor;
                    serializedEnemy.ApplyModifiedPropertiesWithoutUndo();
                    changed = true;
                }
            }

            if (changed)
                PrefabUtility.SaveAsPrefabAsset(contents, EnemyPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(contents);
        }
    }

    private static Transform CreateAnchor(Transform parent, string anchorName, float localHeight)
    {
        GameObject anchorObject = new GameObject(anchorName);
        anchorObject.transform.SetParent(parent, false);
        anchorObject.transform.localPosition = new Vector3(0f, localHeight, 0f);
        anchorObject.transform.localRotation = Quaternion.identity;
        anchorObject.transform.localScale = Vector3.one;
        return anchorObject.transform;
    }

    private static bool IsExpectedLocalPosition(Transform anchor, float localHeight)
    {
        Vector3 position = anchor.localPosition;
        return Mathf.Abs(position.x) < 0.001f
            && Mathf.Abs(position.y - localHeight) < 0.001f
            && Mathf.Abs(position.z) < 0.001f;
    }

    // ==================== 场景层级 ====================

    private static Transform EnsureRig(CameraManager camManager)
    {
        Transform rig = camManager.transform.Find(RigName);
        if (rig == null)
        {
            GameObject rigObject = new GameObject(RigName);
            Undo.RegisterCreatedObjectUndo(rigObject, "Create Gameplay Camera Rig");
            rigObject.transform.SetParent(camManager.transform, false);
            // 初始 inactive：避免新 Rig 在菜单/过场里抢 Brain，进游戏时由 ProcedureGame 激活
            rigObject.SetActive(false);
            rig = rigObject.transform;
            Debug.Log("[CameraRigBuilder] 已新建 GameplayCameraRig（初始 inactive）。");
        }

        if (rig.GetComponent<ACCameraRigController>() == null)
            Undo.AddComponent<ACCameraRigController>(rig.gameObject);

        return rig;
    }

    private static CinemachineVirtualCamera EnsureVcam(
        Transform rig, string vcamName, int priority, Transform follow)
    {
        Transform child = rig.Find(vcamName);
        if (child == null)
        {
            GameObject vcamObject = new GameObject(vcamName);
            Undo.RegisterCreatedObjectUndo(vcamObject, "Create " + vcamName);
            vcamObject.transform.SetParent(rig, false);
            child = vcamObject.transform;
        }

        CinemachineVirtualCamera vcam = child.GetComponent<CinemachineVirtualCamera>();
        if (vcam == null)
            vcam = Undo.AddComponent<CinemachineVirtualCamera>(child.gameObject);

        // Lens：两台都 70 / 0.12 / 5000；机身 Transform 不参与机位计算
        LensSettings lens = vcam.m_Lens;
        lens.FieldOfView = FieldOfView;
        lens.NearClipPlane = NearClip;
        lens.FarClipPlane = FarClip;
        lens.Dutch = 0f;
        vcam.m_Lens = lens;

        // LookAt 留空：朝向完全由 ACCameraStateExtension 写入
        vcam.Follow = follow;
        vcam.LookAt = null;
        vcam.Priority = priority;
        return vcam;
    }

    private static void EnsureExtension(
        CinemachineVirtualCamera vcam,
        ACCameraRigController controller,
        ACCameraRigController.ViewMode mode)
    {
        ACCameraStateExtension extension = vcam.GetComponent<ACCameraStateExtension>();
        if (extension == null)
            extension = Undo.AddComponent<ACCameraStateExtension>(vcam.gameObject);

        SerializedObject serializedExtension = new SerializedObject(extension);
        serializedExtension.FindProperty("m_Controller").objectReferenceValue = controller;
        serializedExtension.FindProperty("m_Mode").intValue = (int)mode;
        serializedExtension.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void BindController(
        ACCameraRigController controller,
        Transform watchPoint,
        Camera mainCamera,
        CinemachineBrain brain,
        CinemachineVirtualCamera normalVcam,
        CinemachineVirtualCamera shootVcam)
    {
        SerializedObject serializedController = new SerializedObject(controller);
        serializedController.FindProperty("m_FollowPoint").objectReferenceValue = watchPoint;
        serializedController.FindProperty("m_TargetCamera").objectReferenceValue = mainCamera;
        serializedController.FindProperty("m_Brain").objectReferenceValue = brain;
        serializedController.FindProperty("m_NormalCamera").objectReferenceValue = normalVcam;
        serializedController.FindProperty("m_ShootCamera").objectReferenceValue = shootVcam;
        serializedController.FindProperty("m_CollisionMask").intValue = CollisionMask;
        serializedController.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(controller);
    }

    private static void BindCameraManager(CameraManager camManager, ACCameraRigController controller)
    {
        SerializedObject serializedManager = new SerializedObject(camManager);
        SerializedProperty rigProperty = serializedManager.FindProperty("m_GameplayRig");
        if (rigProperty == null)
        {
            Debug.LogError(
                "[CameraRigBuilder] CameraManager 上没有 m_GameplayRig 字段。"
                + "请先切回 Unity 让 Assembly-CSharp 重新编译，再执行本工具。");
            return;
        }

        rigProperty.objectReferenceValue = controller;
        serializedManager.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(camManager);
    }

    private static void DisableLegacyFreeLook(CameraManager camManager)
    {
        if (camManager.freeLook == null)
        {
            Debug.LogWarning("[CameraRigBuilder] CameraManager.freeLook 为空，没有可停用的旧相机。");
            return;
        }

        GameObject legacyCamera = camManager.freeLook.gameObject;
        if (legacyCamera.activeSelf)
        {
            Undo.RecordObject(legacyCamera, "Deactivate Legacy FreeLook Camera");
            legacyCamera.SetActive(false);
            Debug.Log("[CameraRigBuilder] 已停用旧 FreeLook Camera（物体保留，作为回退备份）。");
        }
    }

    // ==================== 校验 ====================

    /// <summary>只读检查：引用是否齐全、Lens/优先级/扩展模式是否正确、旧 FreeLook 是否已停用。</summary>
    public static bool Validate()
    {
        bool ok = true;
        CameraManager camManager = FindFirst<CameraManager>();
        if (camManager == null)
        {
            Debug.LogError("[CameraRigBuilder][校验] 场景里找不到 CameraManager。");
            return false;
        }

        SerializedObject serializedManager = new SerializedObject(camManager);
        SerializedProperty rigProperty = serializedManager.FindProperty("m_GameplayRig");
        ACCameraRigController controller = rigProperty != null
            ? rigProperty.objectReferenceValue as ACCameraRigController
            : null;

        if (controller == null)
        {
            Debug.LogError("[CameraRigBuilder][校验] CameraManager.m_GameplayRig 为空："
                + "进游戏后仍会走旧 FreeLook，新相机不会生效。");
            return false;
        }

        SerializedObject serializedController = new SerializedObject(controller);
        ok &= CheckReference(serializedController, "m_FollowPoint", "FollowPoint(CameraWatchPoint)");
        ok &= CheckReference(serializedController, "m_TargetCamera", "TargetCamera(Main Camera)");
        ok &= CheckReference(serializedController, "m_Brain", "Brain");
        ok &= CheckReference(serializedController, "m_NormalCamera", "NormalCamera(VCam_Normal)");
        ok &= CheckReference(serializedController, "m_ShootCamera", "ShootCamera(VCam_Shoot)");

        ok &= CheckVcam(controller, NormalName, NormalPriority, ACCameraRigController.ViewMode.Normal);
        ok &= CheckVcam(controller, ShootName, ShootPriority, ACCameraRigController.ViewMode.Shoot);

        if (camManager.freeLook != null && camManager.freeLook.gameObject.activeSelf)
        {
            Debug.LogWarning("[CameraRigBuilder][校验] 旧 FreeLook Camera 仍是 active："
                + "会和 InputProvider 一起重复消费鼠标。");
            ok = false;
        }

        Debug.Log(ok
            ? "[CameraRigBuilder][校验] 通过：Rig、两台 VCam、锚点、旧相机停用状态都正确。"
            : "[CameraRigBuilder][校验] 存在问题，见上面的 Error/Warning。");
        return ok;
    }

    private static bool CheckReference(SerializedObject serializedObject, string propertyName, string label)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property == null)
        {
            Debug.LogError($"[CameraRigBuilder][校验] 找不到字段 {propertyName}。");
            return false;
        }

        if (property.objectReferenceValue == null)
        {
            Debug.LogError($"[CameraRigBuilder][校验] {label} 未绑定。");
            return false;
        }

        return true;
    }

    private static bool CheckVcam(
        ACCameraRigController controller,
        string vcamName,
        int expectedPriority,
        ACCameraRigController.ViewMode expectedMode)
    {
        Transform rig = controller.transform;
        Transform child = rig.Find(vcamName);
        if (child == null)
        {
            Debug.LogError($"[CameraRigBuilder][校验] GameplayCameraRig 下找不到 {vcamName}。");
            return false;
        }

        bool ok = true;
        CinemachineVirtualCamera vcam = child.GetComponent<CinemachineVirtualCamera>();
        if (vcam == null)
        {
            Debug.LogError($"[CameraRigBuilder][校验] {vcamName} 上没有 CinemachineVirtualCamera。");
            return false;
        }

        if (vcam.Priority != expectedPriority)
        {
            Debug.LogWarning($"[CameraRigBuilder][校验] {vcamName}.Priority = {vcam.Priority}，期望 {expectedPriority}。");
            ok = false;
        }

        // 两台 VCam 必须是 Body/Aim 都没有的纯 Do Nothing：
        // Cinemachine 在极老流版本下会按 Follow/LookAt 自动补 HardLockToTarget/HardLookAt，
        // 那会和控制器算好的机位重复计算，所以这里显式检查。
        if (vcam.GetCinemachineComponent(CinemachineCore.Stage.Body) != null
            || vcam.GetCinemachineComponent(CinemachineCore.Stage.Aim) != null)
        {
            Debug.LogError($"[CameraRigBuilder][校验] {vcamName} 上出现了 Body/Aim 组件，会和控制器算法重复计算，请删掉该组件。");
            ok = false;
        }

        LensSettings lens = vcam.m_Lens;
        if (!Mathf.Approximately(lens.FieldOfView, FieldOfView)
            || !Mathf.Approximately(lens.NearClipPlane, NearClip)
            || !Mathf.Approximately(lens.FarClipPlane, FarClip))
        {
            Debug.LogWarning($"[CameraRigBuilder][校验] {vcamName}.Lens = FOV {lens.FieldOfView} / Near {lens.NearClipPlane} / Far {lens.FarClipPlane}，"
                + $"期望 {FieldOfView} / {NearClip} / {FarClip}。");
            ok = false;
        }

        ACCameraStateExtension extension = child.GetComponent<ACCameraStateExtension>();
        if (extension == null)
        {
            Debug.LogError($"[CameraRigBuilder][校验] {vcamName} 上没有 ACCameraStateExtension。");
            return false;
        }

        SerializedObject serializedExtension = new SerializedObject(extension);
        SerializedProperty modeProperty = serializedExtension.FindProperty("m_Mode");
        if (modeProperty == null || modeProperty.intValue != (int)expectedMode)
        {
            Debug.LogError($"[CameraRigBuilder][校验] {vcamName} 的扩展 Mode 期望 {expectedMode}。");
            ok = false;
        }

        SerializedProperty controllerProperty = serializedExtension.FindProperty("m_Controller");
        if (controllerProperty == null || controllerProperty.objectReferenceValue == null)
        {
            Debug.LogError($"[CameraRigBuilder][校验] {vcamName} 的扩展未绑定 Controller。");
            ok = false;
        }

        return ok;
    }

    // ==================== 工具 ====================

    private static T FindFirst<T>() where T : Object
    {
        T[] found = Object.FindObjectsOfType<T>(true);
        return found != null && found.Length > 0 ? found[0] : null;
    }
}

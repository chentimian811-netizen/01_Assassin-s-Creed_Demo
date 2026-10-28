using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Phase 4（死亡循环）场景部署工具：把篝火与掉魂系统放进 TestScene。
/// 第 4 步（掉魂 / 拾魂）新增「掉魂系统」的部署与预制体引用补全（2026-10-28）。
///
/// 【为什么用 Editor 工具，而不是手改 TestScene.unity】
///   场景在 Unity 里是打开状态，手写 YAML 会在下一次保存时被覆盖；
///   用 API 建对象 + SaveOpenScenes 才能和 Inspector 一致（组件顺序、根物体列表、
///   层级纹理都由 Unity 自己写）。本工具因此只依赖 C# 类型，不写任何 GUID。
///
/// 【一次性自动执行】
///   与 LegacyHUDCleanup 同一套做法：存在 Temp/phase4_deploy.request 时，
///   回到 Unity 触发重编译后自动跑一次。这样新会话里不需要人工点菜单。
///   （Temp/ 在 .gitignore 里，请求文件不会进版本库。）
///
/// 【幂等】
///   两个物件都按名字查重：已存在就只补「动态引用」（掉魂系统的预制体引用），
///   不会生成第二份。
///
/// 【为什么用「玩家出生点」定位篝火】
///   关卡的坐标体系带 (960, 540) 偏移（环境物体整体位移），手算绝对坐标极易放歪。
///   出生点一定是合法可站立的地面，篝火放在它旁边最稳；放完再在 Scene 视图里拖。
/// </summary>
public static class Phase4DeployMenu
{
    private const string RequestRelativePath = "Temp/phase4_deploy.request";
    private const string RequiredSceneName = "TestScene";

    private const string BonfireName = "Bonfire_01";
    private const string SoulsDropObjectName = "SoulsDropSystem";
    private const string SoulsPickupPrefabPath = "Assets/GameMain/Entities/Level/SoulsPickup.prefab";

    /// <summary>篝火相对玩家出生点的偏移（右前方 2.2m）</summary>
    private static readonly Vector3 BonfireOffset = new Vector3(2.2f, 0f, 1.2f);

    [MenuItem("ACDemo/Level/Deploy Phase 4 Props", priority = 200)]
    public static void DeployPhase4Props()
    {
        TryDeployPhase4Props();
    }

    /// <summary>
    /// 清除 Phase 4 的关卡存档（篝火点亮 + 重生点），恢复到「什么都没点亮」。
    /// 想从零验一遍 C1/C2/C3 时用；不影响背包/金币（那两个各有自己的 key）。
    /// </summary>
    [MenuItem("ACDemo/Level/Clear Phase 4 Save", priority = 201)]
    public static void ClearPhase4Save()
    {
        LevelPersistence.ClearAll();
    }

    /// <summary>部署 Phase 4 的两个场景物件并保存场景。返回 false 表示前置条件不满足</summary>
    private static bool TryDeployPhase4Props()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (scene.name != RequiredSceneName)
        {
            Debug.LogError($"[Phase4Deploy] 当前打开的是「{scene.name}」，本阶段只在 {RequiredSceneName} 里部署，已取消");
            return false;
        }

        bool bonfireOk = TryDeployBonfire();
        bool soulsOk = TryDeploySoulsDropSystem();
        if (!bonfireOk && !soulsOk) return false;

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveOpenScenes();

        Debug.Log("[Phase4Deploy] Phase 4 物件部署完成（篝火 + 掉魂系统），场景已保存");
        return true;
    }

    // ==================== 篝火 ====================

    private static bool TryDeployBonfire()
    {
        // 幂等：已经部署过就不再生成第二份（重复执行只是提示一句）
        GameObject existing = GameObject.Find(BonfireName);
        if (existing != null)
        {
            Debug.Log($"[Phase4Deploy] 场景里已有 {BonfireName}（{existing.transform.position}），跳过");
            return true;
        }

        PlayerController player = UnityEngine.Object.FindObjectOfType<PlayerController>(true);
        if (player == null)
        {
            Debug.LogError("[Phase4Deploy] 场景里找不到 PlayerController，无法定位出生点，已取消");
            return false;
        }

        Vector3 position = player.transform.position + BonfireOffset;
        position = SnapToGround(position, player.transform.position.y);

        // 1) 篝火根物体
        GameObject root = new GameObject(BonfireName);
        Undo.RegisterCreatedObjectUndo(root, "Deploy Bonfire");
        root.transform.position = position;

        // 2) 火坑（占位视觉：内置圆柱，去掉碰撞体，避免把玩家挡在门外）
        GameObject pit = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Undo.RegisterCreatedObjectUndo(pit, "Deploy Bonfire Pit");
        pit.name = "FirePit";
        UnityEngine.Object.DestroyImmediate(pit.GetComponent<Collider>());
        pit.transform.SetParent(root.transform, false);
        pit.transform.localPosition = new Vector3(0f, 0.12f, 0f);
        pit.transform.localScale = new Vector3(0.9f, 0.12f, 0.9f);

        // 3) 点光：初始关闭，点燃时由 Bonfire 打开（未点燃的篝火不该冒火光）
        GameObject lightObject = new GameObject("Light");
        Undo.RegisterCreatedObjectUndo(lightObject, "Deploy Bonfire Light");
        lightObject.transform.SetParent(root.transform, false);
        lightObject.transform.localPosition = new Vector3(0f, 0.6f, 0f);
        Light light = lightObject.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = new Color(1f, 0.55f, 0.2f);
        light.range = 7f;
        light.intensity = 2.5f;
        light.enabled = false;

        // 4) 交互范围 + 提示（InteractableSensor 只做距离判定，不需要碰撞体）
        InteractableSensor sensor = root.AddComponent<InteractableSensor>();
        // 半径只在 Editor 里用，不必为此把字段开成 public；用 SerializedObject 直接写序列化值
        SerializedObject serializedSensor = new SerializedObject(sensor);
        SerializedProperty radiusProperty = serializedSensor.FindProperty("m_Radius");
        if (radiusProperty != null) radiusProperty.floatValue = 2.5f;
        serializedSensor.ApplyModifiedPropertiesWithoutUndo();

        // 5) 篝火本体（bonfireId / respawnOffset 用脚本里的默认值）
        root.AddComponent<Bonfire>();

        Debug.Log($"[Phase4Deploy] 篝火已部署：{BonfireName} @ {position}（玩家出生点 {player.transform.position} 右前方 2.2m）；" +
                  "可在 Scene 视图里拖动调整位置。");
        return true;
    }

    // ==================== 掉魂系统 ====================

    private static bool TryDeploySoulsDropSystem()
    {
        GameObject existing = GameObject.Find(SoulsDropObjectName);
        if (existing != null)
        {
            SoulsDropSystem existingSystem = existing.GetComponent<SoulsDropSystem>();
            if (existingSystem != null) EnsurePickupPrefabBound(existingSystem);
            Debug.Log($"[Phase4Deploy] 场景里已有 {SoulsDropObjectName}，跳过创建");
            return true;
        }

        SoulsPickup prefab = AssetDatabase.LoadAssetAtPath<SoulsPickup>(SoulsPickupPrefabPath);
        if (prefab == null)
        {
            Debug.LogError($"[Phase4Deploy] 找不到掉落物预制体 {SoulsPickupPrefabPath}，掉魂系统未部署");
            return false;
        }

        GameObject root = new GameObject(SoulsDropObjectName);
        Undo.RegisterCreatedObjectUndo(root, "Deploy Souls Drop System");
        SoulsDropSystem system = root.AddComponent<SoulsDropSystem>();

        // 预制体引用是 private + SerializeField，只能在 Editor 里用 SerializedObject 写
        SerializedObject serializedSystem = new SerializedObject(system);
        SerializedProperty prefabProperty = serializedSystem.FindProperty("m_PickupPrefab");
        if (prefabProperty != null) prefabProperty.objectReferenceValue = prefab;
        serializedSystem.ApplyModifiedPropertiesWithoutUndo();

        Debug.Log($"[Phase4Deploy] 掉魂系统已部署：{SoulsDropObjectName}（掉落物 {SoulsPickupPrefabPath}）");
        return true;
    }

    /// <summary>已存在时补引用：预制体被改名/重导入后引用会变 null，掉魂就会静默失败</summary>
    private static void EnsurePickupPrefabBound(SoulsDropSystem system)
    {
        SerializedObject serializedSystem = new SerializedObject(system);
        SerializedProperty prefabProperty = serializedSystem.FindProperty("m_PickupPrefab");
        if (prefabProperty == null || prefabProperty.objectReferenceValue != null) return;

        SoulsPickup prefab = AssetDatabase.LoadAssetAtPath<SoulsPickup>(SoulsPickupPrefabPath);
        if (prefab == null) return;

        prefabProperty.objectReferenceValue = prefab;
        serializedSystem.ApplyModifiedPropertiesWithoutUndo();
        Debug.Log("[Phase4Deploy] 已补上掉魂系统的掉落物引用");
    }

    /// <summary>
    /// 从给定位置向下打一条射线落到地面上；打不到就退回玩家脚下高度。
    /// 关卡里有高低差，直接沿用玩家 y 可能让篝火悬空或埋进地里。
    /// </summary>
    private static Vector3 SnapToGround(Vector3 position, float fallbackY)
    {
        Vector3 rayStart = new Vector3(position.x, fallbackY + 3f, position.z);
        if (Physics.Raycast(rayStart, Vector3.down, out RaycastHit hit, 12f, ~0, QueryTriggerInteraction.Ignore))
        {
            return new Vector3(position.x, hit.point.y, position.z);
        }

        return new Vector3(position.x, fallbackY, position.z);
    }

    // ==================== 一次性自动执行 ====================

    [InitializeOnLoadMethod]
    private static void TryAutoDeploy()
    {
        if (Application.isBatchMode) return;
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;

        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        string requestPath = Path.Combine(projectRoot, RequestRelativePath);
        if (!File.Exists(requestPath)) return;

        EditorApplication.delayCall += () =>
        {
            try
            {
                // 部署成功才删请求文件：场景没打开 / 不是 TestScene 时保留，下次重编译再试
                if (TryDeployPhase4Props()) File.Delete(requestPath);
            }
            catch (Exception e)
            {
                Debug.LogError("[Phase4Deploy] 自动部署失败：" + e);
            }
        };
    }
}

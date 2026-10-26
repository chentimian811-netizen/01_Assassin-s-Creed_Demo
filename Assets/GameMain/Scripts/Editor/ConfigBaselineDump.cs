#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 临时工具（Task 0 专用）：把 Prefab、场景实例和配置资产上的“实际序列化值”导出为文本。
/// 存在的意义：YAML 缺字段时 Unity 会使用 C# 字段初始值，只读 YAML 会把缺字段误判成 0，
/// 所以基准必须从 Unity 序列化对象读取。迁移完成后本文件删除。
/// </summary>
public static class ConfigBaselineDump
{
    /// <summary>项目脚本所在程序集名；只导出这里面的组件，过滤掉框架与第三方组件。</summary>
    private const string ProjectAssemblyName = "Assembly-CSharp";

    /// <summary>需要导出的 Prefab 清单（相对项目根的 Assets 路径）。</summary>
    private static readonly string[] PrefabPaths =
    {
        "Assets/GameMain/Entities/Player/Male.prefab",
        "Assets/GameMain/Entities/Enemy/Enemy.prefab",
        "Assets/GameMain/Entities/EnemyManager.prefab",
    };

    /// <summary>需要导出的场景。</summary>
    private const string ScenePath = "Assets/GameMain/Scenes/TestScene.unity";

    [MenuItem("ACDemo/Config/Dump Baseline (Temp)")]
    public static void DumpBaseline()
    {
        // 顺带验证 Packages/com.code-philosophy.luban 的运行时程序集能被 Assembly-CSharp 引用
        Debug.Log($"[ConfigBaselineDump] Luban 运行时程序集：{typeof(Luban.BeanBase).Assembly.GetName().Name}");

        var sb = new StringBuilder();
        sb.AppendLine("# ACDemo 配置基准导出（Task 0）");
        sb.AppendLine("# 值来自 Unity 序列化对象，缺字段即 C# 字段初始值，也就是当前真实生效值。");
        sb.AppendLine();

        foreach (var path in PrefabPaths)
        {
            DumpPrefab(path, sb);
        }

        DumpScene(sb);

        DumpAllAssetsOfType<WeaponConfig>(sb);
        DumpAllAssetsOfType<AttackData>(sb);
        DumpAllAssetsOfType<StaminaConfig>(sb);
        DumpAllAssetsOfType<ParryConfig>(sb);
        DumpAllAssetsOfType<BossPhaseConfig>(sb);
        DumpAllAssetsOfType<MovesetConfig>(sb);
        DumpAllAssetsOfType<ItemConfig_SO>(sb);

        var outputPath = Path.GetFullPath(Path.Combine(Application.dataPath, "../Logs/config-baseline-dump.txt"));
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath));
        File.WriteAllText(outputPath, sb.ToString(), new UTF8Encoding(false));
        Debug.Log($"[ConfigBaselineDump] 基准已导出：{outputPath}");

        if (Application.isBatchMode)
        {
            EditorApplication.Exit(0);
        }
    }

    private static void DumpPrefab(string path, StringBuilder sb)
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null)
        {
            sb.AppendLine($"# 缺失 Prefab：{path}");
            sb.AppendLine();
            return;
        }

        var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            sb.AppendLine($"===== PREFAB {path} =====");
            DumpGameObjects(root.GetComponentsInChildren<Component>(true), sb);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void DumpScene(StringBuilder sb)
    {
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null)
        {
            sb.AppendLine($"# 缺失场景：{ScenePath}");
            return;
        }

        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        sb.AppendLine($"===== SCENE {ScenePath} =====");
        var all = new List<Component>();
        foreach (var go in scene.GetRootGameObjects())
        {
            all.AddRange(go.GetComponentsInChildren<Component>(true));
        }
        DumpGameObjects(all, sb);
    }

    private static void DumpGameObjects(IEnumerable<Component> components, StringBuilder sb)
    {
        foreach (var component in components)
        {
            if (component == null)
            {
                continue;
            }

            var type = component.GetType();
            if (type.Assembly.GetName().Name != ProjectAssemblyName)
            {
                continue;
            }

            sb.AppendLine($"--- {type.Name} @ {GetHierarchyPath(component.gameObject)}");
            var so = new SerializedObject(component);
            so.Update();
            var iterator = so.GetIterator();
            var enterChildren = true;
            while (iterator.NextVisible(enterChildren))
            {
                enterChildren = true;
                if (iterator.name == "m_Script")
                {
                    continue;
                }
                sb.AppendLine($"  {iterator.propertyPath} = {ValueText(iterator)}");
            }
            sb.AppendLine();
        }
    }

    private static void DumpAllAssetsOfType<T>(StringBuilder sb) where T : Object
    {
        var guids = AssetDatabase.FindAssets($"t:{typeof(T).Name}", new[] { "Assets" });
        if (guids.Length == 0)
        {
            sb.AppendLine($"===== ASSETS t:{typeof(T).Name} （无）=====");
            sb.AppendLine();
            return;
        }

        var paths = new List<string>();
        foreach (var guid in guids)
        {
            paths.Add(AssetDatabase.GUIDToAssetPath(guid));
        }
        paths.Sort(string.CompareOrdinal);

        sb.AppendLine($"===== ASSETS t:{typeof(T).Name}（共 {paths.Count} 个）=====");
        foreach (var path in paths)
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null)
            {
                sb.AppendLine($"--- 无法加载：{path}");
                continue;
            }

            sb.AppendLine($"--- {typeof(T).Name} @ {path}");
            var so = new SerializedObject(asset);
            so.Update();
            var iterator = so.GetIterator();
            var enterChildren = true;
            while (iterator.NextVisible(enterChildren))
            {
                enterChildren = true;
                if (iterator.name == "m_Script")
                {
                    continue;
                }
                sb.AppendLine($"  {iterator.propertyPath} = {ValueText(iterator)}");
            }
            sb.AppendLine();
        }
    }

    private static string ValueText(SerializedProperty property)
    {
        switch (property.propertyType)
        {
            case SerializedPropertyType.Integer:
                return property.intValue.ToString();
            case SerializedPropertyType.Boolean:
                return property.boolValue ? "true" : "false";
            case SerializedPropertyType.Float:
                return property.floatValue.ToString("0.######");
            case SerializedPropertyType.String:
                return "\"" + property.stringValue + "\"";
            case SerializedPropertyType.Enum:
                var names = property.enumNames;
                var displayName = property.enumValueIndex >= 0 && property.enumValueIndex < names.Length
                    ? names[property.enumValueIndex]
                    : "?";
                return property.intValue + " (" + displayName + ")";
            case SerializedPropertyType.ObjectReference:
                var target = property.objectReferenceValue;
                return target == null ? "null" : target.name + " [" + AssetDatabase.GetAssetPath(target) + "]";
            case SerializedPropertyType.Vector2:
                return property.vector2Value.ToString();
            case SerializedPropertyType.Vector3:
                return property.vector3Value.ToString();
            case SerializedPropertyType.Color:
                return property.colorValue.ToString();
            case SerializedPropertyType.LayerMask:
                return property.intValue.ToString();
            case SerializedPropertyType.AnimationCurve:
                return "AnimationCurve(keys=" + property.animationCurveValue.length + ")";
            default:
                return "<" + property.propertyType + ">";
        }
    }

    private static string GetHierarchyPath(GameObject go)
    {
        var path = go.name;
        var parent = go.transform.parent;
        while (parent != null)
        {
            path = parent.name + "/" + path;
            parent = parent.parent;
        }
        return path;
    }
}
#endif

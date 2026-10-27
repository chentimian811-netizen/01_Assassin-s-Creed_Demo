using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 清理旧 HUD（已被 GF HUD 取代的部分）。
///
/// 只删“确认被替代”的东西，不动还能用的旧面板：
///   - 场景 Canvas/PlayerBar（旧底部血条，PlayerHealthBar）
///   - GameManager.healthBarRoot 的引用（否则 Inspector 显示 Missing）
///   - Setting.prefab 里同一套旧 PlayerBar（该预制体不在 TestScene 里用）
///
/// 保留：
///   - Canvas/MinimapPanel（新 HUD 还没有小地图）
///   - Canvas/BackPack、Canvas/Lottery（GF 的 PackageForm / LotteryForm 预制体还没做，
///     这两个旧按钮目前仍是背包/抽卡的唯一入口）
///
/// 菜单：ACDemo/UI/HUD/Cleanup Legacy HUD
/// 也支持一次性自动执行：存在 Temp/legacy_cleanup.request 时，切回 Unity 自动跑完并保存场景。
/// </summary>
public static class LegacyHUDCleanup
{
    private const string RequestRelativePath = "Temp/legacy_cleanup.request";
    private const string SettingPrefabPath = "Assets/GameMain/Scenes/Test/Setting.prefab";

    [MenuItem("ACDemo/UI/HUD/Cleanup Legacy HUD", priority = 120)]
    public static void Cleanup()
    {
        int removedBars = 0;

        // 1) 场景里的旧底部血条
        GameObject sceneCanvas = GameObject.Find("Canvas");
        if (sceneCanvas != null)
        {
            Transform playerBar = sceneCanvas.transform.Find("PlayerBar");
            if (playerBar != null)
            {
                UnityEngine.Object.DestroyImmediate(playerBar.gameObject);
                removedBars++;
            }
        }
        else
        {
            Debug.LogWarning("[LegacyHUDCleanup] 场景里找不到 Canvas，跳过场景内的 PlayerBar");
        }

        // 2) GameManager 上的旧血条引用
        GameManager gameManager = UnityEngine.Object.FindObjectOfType<GameManager>(true);
        if (gameManager != null)
        {
            SerializedObject serializedObject = new SerializedObject(gameManager);
            SerializedProperty property = serializedObject.FindProperty("healthBarRoot");
            if (property != null && property.objectReferenceValue != null)
            {
                property.objectReferenceValue = null;
                serializedObject.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        // 3) Setting.prefab 里的同一套旧血条
        CleanupSettingPrefab();

        if (sceneCanvas != null)
        {
            EditorSceneManager.MarkSceneDirty(sceneCanvas.scene);
        }

        EditorSceneManager.SaveOpenScenes();
        AssetDatabase.SaveAssets();

        Debug.Log($"[LegacyHUDCleanup] 清理完成：场景 PlayerBar 移除 {removedBars} 个，GameManager.healthBarRoot 已清空，场景已保存");
    }

    private static void CleanupSettingPrefab()
    {
        if (!File.Exists(SettingPrefabPath)) return;

        GameObject root = PrefabUtility.LoadPrefabContents(SettingPrefabPath);
        if (root == null) return;

        try
        {
            Transform canvas = root.transform.Find("Canvas");
            Transform playerBar = canvas != null ? canvas.Find("PlayerBar") : null;
            if (playerBar == null) return;

            UnityEngine.Object.DestroyImmediate(playerBar.gameObject, true);
            PrefabUtility.SaveAsPrefabAsset(root, SettingPrefabPath);
            Debug.Log("[LegacyHUDCleanup] Setting.prefab 的旧 PlayerBar 已移除");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    // ==================== 一次性自动执行 ====================

    [InitializeOnLoadMethod]
    private static void TryAutoCleanup()
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
                File.Delete(requestPath);
                Cleanup();
            }
            catch (Exception e)
            {
                Debug.LogError("[LegacyHUDCleanup] 自动清理失败：" + e);
            }
        };
    }
}

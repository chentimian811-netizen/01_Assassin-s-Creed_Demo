using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// 临时迁移工具（Task 3/10/12 用）：把 CharacterConfigIdentity 挂到角色 Prefab 根对象并写入配置 ID。
///
/// 【为什么不手改 Prefab YAML】
///   手改 YAML 容易漏改 m_Component 列表、fileID 引用或 m_AddedComponents，
///   一旦写错整个 Prefab 会静默损坏。走 PrefabUtility 才能保证序列化结构正确。
///
/// 迁移完成后（Task 14）本文件删除。
/// </summary>
public static class ConfigIdentityMigration
{
    /// <summary>玩家 Prefab 与配置 ID</summary>
    private const string MalePrefabPath = "Assets/GameMain/Entities/Player/Male.prefab";
    private const string PlayerConfigId = "player_main";

    /// <summary>普通敌人 Prefab 与配置 ID。生命先迁到 Character 表（Task 5 期间不打断 Play），AI 数值留给 Task 10</summary>
    private const string EnemyPrefabPath = "Assets/GameMain/Entities/Enemy/Enemy.prefab";
    private const string EnemyConfigId = "enemy_default";

    [MenuItem("ACDemo/Config/Migration/Apply Character Identities", priority = 40)]
    public static void ApplyCharacterIdentities()
    {
        ApplyPrefabIdentity(MalePrefabPath, PlayerConfigId);
        ApplyPrefabIdentity(EnemyPrefabPath, EnemyConfigId);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[ConfigIdentityMigration] 角色配置身份写入完成");
    }

    /// <summary>给 Prefab 根对象挂/复用 CharacterConfigIdentity 并写入 ID</summary>
    private static void ApplyPrefabIdentity(string prefabPath, string configId)
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) == null)
        {
            Debug.LogError($"[ConfigIdentityMigration] Prefab 不存在：{prefabPath}");
            return;
        }

        var root = PrefabUtility.LoadPrefabContents(prefabPath);
        try
        {
            var identity = root.GetComponent<CharacterConfigIdentity>();
            if (identity == null)
            {
                identity = root.AddComponent<CharacterConfigIdentity>();
            }
            identity.SetConfigId(configId);

            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            Debug.Log($"[ConfigIdentityMigration] {prefabPath} → configId={configId}");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }
}

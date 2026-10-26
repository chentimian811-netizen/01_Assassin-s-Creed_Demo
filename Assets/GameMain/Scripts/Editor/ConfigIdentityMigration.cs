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

    /// <summary>
    /// 重存全部 WeaponConfig 资产：把已经迁到 Weapon 表的数值字段从 .asset 里清掉，
    /// 避免 Inspector 上还留着一份"看起来能改"的旧数值。
    /// 只 SetDirty + SaveAssets，不重建资产，GUID 与招式/模型引用都不会变。
    /// </summary>
    [MenuItem("ACDemo/Config/Migration/Resave Weapon Configs", priority = 41)]
    public static void ResaveWeaponConfigs()
    {
        int count = 0;
        foreach (var guid in AssetDatabase.FindAssets("t:WeaponConfig", new[] { "Assets" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var config = AssetDatabase.LoadAssetAtPath<WeaponConfig>(path);
            if (config == null) continue;
            EditorUtility.SetDirty(config);
            count++;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[ConfigIdentityMigration] 已重存 {count} 个 WeaponConfig（清掉已迁移的数值字段）");
    }

    /// <summary>
    /// 为全部 AttackData 写入 attackId（首次填表用资产文件名）。
    /// 走后门 SerializedObject 而不是暴露运行时 setter：attackId 只允许迁移期改一次。
    /// </summary>
    [MenuItem("ACDemo/Config/Migration/Apply Attack Ids", priority = 42)]
    public static void ApplyAttackIds()
    {
        int count = 0;
        foreach (var guid in AssetDatabase.FindAssets("t:AttackData", new[] { "Assets" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var asset = AssetDatabase.LoadAssetAtPath<AttackData>(path);
            if (asset == null) continue;

            string id = System.IO.Path.GetFileNameWithoutExtension(path);
            var so = new SerializedObject(asset);
            var prop = so.FindProperty("attackId");
            if (prop == null)
            {
                Debug.LogError($"[ConfigIdentityMigration] AttackData 上找不到 attackId 字段：{path}");
                continue;
            }

            if (prop.stringValue != id)
            {
                prop.stringValue = id;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            EditorUtility.SetDirty(asset);
            count++;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[ConfigIdentityMigration] 已为 {count} 个 AttackData 写入 attackId（= 资产名）");
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

using UnityEngine;
using UnityEditor;

/// <summary>
/// 场景清理工具 - 批量处理缺失脚本引用和负缩放 BoxCollider 警告
/// 菜单位置：Tools → 清理
/// </summary>
public class SceneCleanupTool
{
    // ==================== 功能一：移除缺失脚本引用 ====================

    /// <summary>
    /// 菜单入口：遍历场景中所有 GameObject，移除 Missing Script 组件
    /// </summary>
    [MenuItem("Tools/清理/移除缺失脚本引用")]
    static void RemoveMissingScripts()
    {
        // FindObjectsOfTypeAll 能找到包括未激活对象在内的所有 GameObject
        GameObject[] allObjects = Resources.FindObjectsOfTypeAll<GameObject>();
        int removedCount = 0;

        foreach (GameObject go in allObjects)
        {
            // 跳过 Project 窗口中的预制体资产，只处理场景中的对象
            if (EditorUtility.IsPersistent(go)) continue;

            // 使用 GameObjectUtility 内置方法移除缺失脚本
            // 该方法返回被移除的组件数量
            int count = GameObjectUtility.RemoveMonoBehavioursWithMissingScript(go);
            if (count > 0)
            {
                removedCount += count;
                // 标记场景已修改，方便保存
                EditorUtility.SetDirty(go);
            }
        }

        Debug.Log($"<color=green>[清理完成]</color> 共移除 {removedCount} 个缺失脚本引用");
    }

    // ==================== 功能二：修复负缩放 BoxCollider ====================

    /// <summary>
    /// 菜单入口：将场景中负缩放物体的 BoxCollider 替换为 MeshCollider
    /// BoxCollider 不支持负数缩放，会产生警告；MeshCollider（Convex）可以正确处理
    /// </summary>
    [MenuItem("Tools/清理/修复负缩放BoxCollider")]
    static void FixNegativeScaleBoxColliders()
    {
        // 获取场景中所有 BoxCollider（true = 包含未激活对象）
        BoxCollider[] boxColliders = Object.FindObjectsOfType<BoxCollider>(true);
        int fixedCount = 0;

        foreach (BoxCollider box in boxColliders)
        {
            Transform t = box.transform;
            // lossyScale 是世界空间下的最终缩放，考虑了父物体的缩放
            Vector3 lossyScale = t.lossyScale;

            // 检测是否有任意轴为负数
            if (lossyScale.x < 0 || lossyScale.y < 0 || lossyScale.z < 0)
            {
                GameObject go = box.gameObject;

                // 记录 BoxCollider 的原始属性
                Vector3 center = box.center;
                Vector3 size = box.size;
                bool isTrigger = box.isTrigger;
                PhysicMaterial material = box.sharedMaterial;

                // 移除 BoxCollider
                Object.DestroyImmediate(box);

                // 添加 MeshCollider 并设置为 Convex（支持负缩放）
                MeshCollider meshCol = go.AddComponent<MeshCollider>();
                meshCol.convex = true;
                meshCol.isTrigger = isTrigger;
                meshCol.sharedMaterial = material;

                // 标记对象已修改
                EditorUtility.SetDirty(go);
                fixedCount++;
            }
        }

        Debug.Log($"<color=green>[清理完成]</color> 共修复 {fixedCount} 个负缩放 BoxCollider（已替换为 MeshCollider）");
    }

    // ==================== 功能三：一键全部清理 ====================

    /// <summary>
    /// 菜单入口：依次执行所有清理操作
    /// </summary>
    [MenuItem("Tools/清理/全部清理")]
    static void CleanAll()
    {
        RemoveMissingScripts();
        FixNegativeScaleBoxColliders();
        Debug.Log("<color=cyan>[全部清理完成]</color>");
    }
}

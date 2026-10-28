using UnityEngine;

/// <summary>
/// 关卡进度持久化（Phase 4 第 5 步）：篝火是否点亮、重生点在哪。
///
/// 【为什么集中在一个类里】
///   `PlayerPrefs` 的 key 一旦散落在各脚本里，改一个字符就会「读不到自己上次写的」，
///   而且重置存档得满地找。这里把 key 与读写口径收在一处，其它脚本只说业务语义
///   （`IsBonfireLit` / `MarkBonfireLit` / `SaveCheckpoint`）。
///
/// 【为什么存「篝火 id + 坐标 + 朝向」而不是只存 id】
///   单场景阶段篝火是场景物体、没有注册表；只存 id 的话下次启动还得遍历场景找 id。
///   坐标是点篝火那一刻记下的固定值，直接读回来最省事。
///   朝向只存 yaw：篝火都是直立摆放，俯仰/翻滚没有意义。
///
/// 【惯例对齐】
///   与 `PackageLocalData`（PlayerPrefs["PackageLocalData"]）、`CurrencyManager`（PlayerPrefs["PlayerGold"]）同类：
///   懒加载静态类 + 即时写盘，不做内存缓存表。
///
/// 【捷径门（C4）暂未做】
///   需要「门 + 两侧区域」的关卡几何才能验单向打通；等场景搭好再补
///   `IsShortcutUnlocked / MarkShortcutUnlocked` 与门的开关表现（见方案 §0.4 第 5 步记录）。
/// </summary>
public static class LevelPersistence
{
    // ——— key（改这里之前先想清楚：老存档会读不到）———
    private const string BonfireLitPrefix = "BonfireLit_";          // + bonfireId
    private const string CheckpointBonfireIdKey = "Checkpoint_BonfireId";
    private const string CheckpointPosXKey = "Checkpoint_PosX";
    private const string CheckpointPosYKey = "Checkpoint_PosY";
    private const string CheckpointPosZKey = "Checkpoint_PosZ";
    private const string CheckpointYawKey = "Checkpoint_Yaw";

    /// <summary>这个篝火点亮过没有（第一次点燃后才为 true）</summary>
    public static bool IsBonfireLit(string bonfireId)
    {
        if (string.IsNullOrEmpty(bonfireId)) return false;
        return PlayerPrefs.GetInt(BonfireLitPrefix + bonfireId, 0) == 1;
    }

    /// <summary>记下「这个篝火点亮过」。休息不改变这个标记。</summary>
    public static void MarkBonfireLit(string bonfireId)
    {
        if (string.IsNullOrEmpty(bonfireId)) return;

        PlayerPrefs.SetInt(BonfireLitPrefix + bonfireId, 1);
        PlayerPrefs.Save();
    }

    /// <summary>存重生点（点篝火那一刻的落点与朝向）</summary>
    public static void SaveCheckpoint(string bonfireId, Vector3 position, Quaternion rotation)
    {
        if (string.IsNullOrEmpty(bonfireId)) return;

        PlayerPrefs.SetString(CheckpointBonfireIdKey, bonfireId);
        PlayerPrefs.SetFloat(CheckpointPosXKey, position.x);
        PlayerPrefs.SetFloat(CheckpointPosYKey, position.y);
        PlayerPrefs.SetFloat(CheckpointPosZKey, position.z);
        PlayerPrefs.SetFloat(CheckpointYawKey, rotation.eulerAngles.y);
        PlayerPrefs.Save();
    }

    /// <summary>读重生点；没有存档时返回 false</summary>
    public static bool TryLoadCheckpoint(out string bonfireId, out Vector3 position, out Quaternion rotation)
    {
        bonfireId = PlayerPrefs.GetString(CheckpointBonfireIdKey, string.Empty);
        if (string.IsNullOrEmpty(bonfireId))
        {
            position = Vector3.zero;
            rotation = Quaternion.identity;
            return false;
        }

        position = new Vector3(
            PlayerPrefs.GetFloat(CheckpointPosXKey, 0f),
            PlayerPrefs.GetFloat(CheckpointPosYKey, 0f),
            PlayerPrefs.GetFloat(CheckpointPosZKey, 0f));

        // 只回填 yaw：篝火是直立摆放，pitch/roll 没有意义
        rotation = Quaternion.Euler(0f, PlayerPrefs.GetFloat(CheckpointYawKey, 0f), 0f);
        return true;
    }

    /// <summary>
    /// 清掉 Phase 4 的全部关卡存档（篝火点亮 + 重生点）。
    /// 测试用：想从「什么都没点亮」的状态重新验一遍时，用 Editor 菜单
    /// `ACDemo/Level/Clear Phase 4 Save`，或任何脚本里调这里。
    /// </summary>
    public static void ClearAll()
    {
        // 目前只有一个篝火；将来多篝火时把 id 列出来删（PlayerPrefs 没有枚举 API）
        PlayerPrefs.DeleteKey(BonfireLitPrefix + "bonfire_01");
        PlayerPrefs.DeleteKey(CheckpointBonfireIdKey);
        PlayerPrefs.DeleteKey(CheckpointPosXKey);
        PlayerPrefs.DeleteKey(CheckpointPosYKey);
        PlayerPrefs.DeleteKey(CheckpointPosZKey);
        PlayerPrefs.DeleteKey(CheckpointYawKey);
        PlayerPrefs.Save();

        Debug.Log("[LevelPersistence] Phase 4 关卡存档已清除（篝火点亮 / 重生点）");
    }
}

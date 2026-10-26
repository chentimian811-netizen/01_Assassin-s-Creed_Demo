using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 配置引用校验：检查"生成文件是否完整"和"Excel 表与 Unity 资产之间的引用是否对得上"。
///
/// 【为什么放在 Editor】
///   这些检查需要 AssetDatabase 与 Resources.Load，只能跑在编辑器里；
///   运行时不做这类扫描，避免启动开销与被裁剪的资源引用拦路。
///
/// 【输出约定】
///   每条问题都带"资产路径 / 配置 ID / 表名"，让人不用猜就能定位到具体单元格或资产。
/// </summary>
public static class ConfigReferenceValidator
{
    /// <summary>Luban 生成代码目录（禁止手写文件）</summary>
    public const string GeneratedCodeDir = "Assets/GameMain/Scripts/Config/Generated";

    /// <summary>Luban 生成数据目录（禁止手写文件）</summary>
    public const string GeneratedDataDir = "Assets/GameMain/Resources/DataTables/Luban";

    /// <summary>Resources 内访问生成数据用的相对路径</summary>
    private const string GeneratedDataResourceRoot = "DataTables/Luban";

    /// <summary>扫描角色配置身份时使用的 Prefab 根目录</summary>
    private const string EntityPrefabRoot = "Assets/GameMain/Entities";

    /// <summary>玩家配置 ID：第一批垂直迁移固定使用这个值</summary>
    private const string PlayerConfigId = "player_main";

    /// <summary>一条校验问题</summary>
    public sealed class Problem
    {
        public string Where;
        public string Message;

        /// <summary>
        /// 已知缺陷：有明确决策记录、本次不修的问题。只提示，不计入失败。
        /// 目前只用于"弓的 weaponID=1111 与 Item 表不一致"这一条既有数据缺陷（计划 §2.7-1）。
        /// </summary>
        public bool IsKnownIssue;

        public override string ToString()
        {
            return (IsKnownIssue ? "[已知缺陷] " : "") + $"{Where} → {Message}";
        }
    }

    [MenuItem("ACDemo/Config/Validate Only", priority = 20)]
    public static void ValidateOnly()
    {
        ValidateAndLog();
    }

    /// <summary>执行全部校验并输出一份汇总日志；返回是否全部通过</summary>
    public static bool ValidateAndLog()
    {
        var problems = new List<Problem>();
        Validate(problems);

        int errorCount = 0;
        int knownCount = 0;
        var sb = new StringBuilder();
        foreach (var problem in problems)
        {
            if (problem.IsKnownIssue) knownCount++;
            else errorCount++;
            sb.AppendLine().Append("  - ").Append(problem);
        }

        if (errorCount == 0)
        {
            Debug.Log($"[ConfigValidator] 全部检查通过（{Time.realtimeSinceStartup:0.0}），" +
                      $"已知缺陷 {knownCount} 条（不计入失败）{sb}");
            return true;
        }

        Debug.LogError($"[ConfigValidator] 发现 {errorCount} 个配置问题（另有已知缺陷 {knownCount} 条）：{sb}");
        return false;
    }

    /// <summary>校验入口（供导出菜单与批处理调用）</summary>
    public static void Validate(List<Problem> problems)
    {
        if (!CheckGeneratedFiles(problems)) return;
        if (!CheckTablesLoadable(problems)) return;

        CheckCharacterIdentities(problems);
        CheckTableReferences(problems);
        CheckWeaponReferences(problems);
        CheckAttackReferences(problems);
        CheckGlobalTables(problems);
    }

    /// <summary>
    /// 生成文件是否齐全：从 Tables.cs 里抽出 loader("xxx") 的名字，
    /// 逐个确认 Resources/DataTables/Luban/xxx.json 真的存在。
    /// </summary>
    private static bool CheckGeneratedFiles(List<Problem> problems)
    {
        int before = problems.Count;

        if (!Directory.Exists(GeneratedCodeDir))
            problems.Add(Make(GeneratedCodeDir, "生成代码目录不存在，请先执行 ACDemo/Config 导出"));
        if (!Directory.Exists(GeneratedDataDir))
            problems.Add(Make(GeneratedDataDir, "生成数据目录不存在，请先执行 ACDemo/Config 导出"));
        if (problems.Count > before) return false;

        string tablesCsPath = Path.Combine(GeneratedCodeDir, "Tables.cs");
        if (!File.Exists(tablesCsPath))
        {
            problems.Add(Make(tablesCsPath, "缺少 Tables.cs，生成过程没有跑完"));
            return false;
        }

        string tablesCs = File.ReadAllText(tablesCsPath, Encoding.UTF8);
        var matches = Regex.Matches(tablesCs, "loader\\(\"([^\"]+)\"\\)");
        if (matches.Count == 0)
        {
            problems.Add(Make(tablesCsPath, "没有解析到任何 loader(\"...\")，生成代码可能被手改过"));
            return false;
        }

        foreach (Match match in matches)
        {
            string name = match.Groups[1].Value;
            string jsonPath = Path.Combine(GeneratedDataDir, name + ".json");
            if (!File.Exists(jsonPath))
            {
                problems.Add(Make(jsonPath, $"表 {name} 的 JSON 缺失（Tables.cs 引用了它）"));
            }
        }

        return problems.Count == before;
    }

    /// <summary>Tables 能否真正加载（缺 JSON、JSON 字段与生成类不匹配都会在这里暴露）</summary>
    private static bool CheckTablesLoadable(List<Problem> problems)
    {
        DataRepository.ResetForReload();
        DataRepository.Initialize();

        if (!DataRepository.IsReady)
        {
            problems.Add(Make(GeneratedDataResourceRoot,
                $"DataRepository 初始化失败：{DataRepository.LastError}"));
            return false;
        }

        var tables = DataRepository.ConfigTables;
        if (tables.TbCharacter == null || tables.TbPlayer == null ||
            tables.TbStamina == null || tables.TbParry == null)
        {
            problems.Add(Make(GeneratedCodeDir, "Tables 缺少 TbCharacter / TbPlayer / TbStamina / TbParry"));
            return false;
        }

        return true;
    }

    /// <summary>
    /// 角色配置身份：非空、不重复、且能在 Character 表里找到对应行。
    /// 只扫描 Prefab 资产；场景实例的数值与 ID 覆盖由人工 Play 前检查。
    /// </summary>
    private static void CheckCharacterIdentities(List<Problem> problems)
    {
        var characterTable = DataRepository.ConfigTables.TbCharacter;
        var seen = new Dictionary<string, string>();

        foreach (var path in FindAssets("t:Prefab", EntityPrefabRoot))
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) continue;

            var identity = prefab.GetComponent<CharacterConfigIdentity>();
            if (identity == null) continue;

            if (!identity.HasConfigId)
            {
                problems.Add(Make(path, "CharacterConfigIdentity 的配置 ID 为空"));
                continue;
            }

            string configId = identity.ConfigId;
            if (seen.TryGetValue(configId, out string otherPath))
            {
                problems.Add(Make(path, $"配置 ID 重复：{configId}（另一处：{otherPath}）"));
                continue;
            }
            seen[configId] = path;

            if (characterTable.GetOrDefault(configId) == null)
            {
                problems.Add(Make(path, $"Character 表缺少配置行：id={configId}"));
            }
        }

        if (!seen.ContainsKey(PlayerConfigId))
        {
            problems.Add(Make(EntityPrefabRoot, $"没有找到配置 ID 为 {PlayerConfigId} 的角色 Prefab"));
        }
    }

    /// <summary>表与表之间的引用：Stamina.id / Character.id 必须互相对得上</summary>
    private static void CheckTableReferences(List<Problem> problems)
    {
        var tables = DataRepository.ConfigTables;
        var characterTable = tables.TbCharacter;

        foreach (var stamina in tables.TbStamina.DataList)
        {
            if (string.IsNullOrEmpty(stamina.Id))
            {
                problems.Add(Make("Stamina", "存在 id 为空的耐力行"));
                continue;
            }

            if (characterTable.GetOrDefault(stamina.Id) == null)
            {
                problems.Add(Make("Stamina", $"id={stamina.Id} 在 Character 表里没有对应行"));
            }
        }

        var playerCharacter = characterTable.GetOrDefault(PlayerConfigId);
        if (playerCharacter == null)
        {
            problems.Add(Make("Character", $"缺少玩家配置行：id={PlayerConfigId}"));
        }
        else if (playerCharacter.MaxHealth <= 0f)
        {
            problems.Add(Make("Character", $"玩家 {PlayerConfigId} 的 max_health 必须大于 0，当前 {playerCharacter.MaxHealth}"));
        }

        if (tables.TbStamina.GetOrDefault(PlayerConfigId) == null)
        {
            problems.Add(Make("Stamina", $"缺少玩家配置行：id={PlayerConfigId}"));
        }

        // Player / Parry 目前只有玩家一行；敌人与 Boss 的行由各自迁移批次补
        foreach (var playerRow in tables.TbPlayer.DataList)
        {
            if (characterTable.GetOrDefault(playerRow.Id) == null)
            {
                problems.Add(Make("Player", $"id={playerRow.Id} 在 Character 表里没有对应行"));
            }
        }
        if (tables.TbPlayer.GetOrDefault(PlayerConfigId) == null)
        {
            problems.Add(Make("Player", $"缺少玩家配置行：id={PlayerConfigId}"));
        }
        if (tables.TbParry.GetOrDefault(PlayerConfigId) == null)
        {
            problems.Add(Make("Parry", $"缺少玩家配置行：id={PlayerConfigId}"));
        }

        CheckItemAndShopReferences(problems, tables);
    }

    /// <summary>
    /// Item 与 Shop 的引用：asset_id 非空且唯一，Shop 引用的 ItemAssetId 必须存在。
    /// Shop 第一阶段仍来自 TSV，但它的 ItemAssetId 指向 Item 表，必须一起校验。
    /// </summary>
    private static void CheckItemAndShopReferences(List<Problem> problems, Tables tables)
    {
        var assetIdToItemId = new Dictionary<string, int>();

        foreach (var item in tables.TbItem.DataList)
        {
            if (string.IsNullOrEmpty(item.AssetId))
            {
                problems.Add(Make("Item", $"id={item.Id} 的 asset_id 为空"));
            }
            else if (assetIdToItemId.TryGetValue(item.AssetId, out int otherId))
            {
                problems.Add(Make("Item", $"asset_id 重复：{item.AssetId}（id={otherId} 与 id={item.Id}）"));
            }
            else
            {
                assetIdToItemId[item.AssetId] = item.Id;
            }

            if (item.BaseDamage < 0)
            {
                problems.Add(Make("Item", $"id={item.Id} 的 base_damage 不能为负：{item.BaseDamage}"));
            }
            if (item.CritRate < 0f || item.CritRate > 1f)
            {
                problems.Add(Make("Item", $"id={item.Id} 的 crit_rate 必须在 0~1：{item.CritRate}"));
            }
        }

        if (DataRepository.ShopTable == null) return;

        foreach (var shop in DataRepository.ShopTable.Values)
        {
            if (string.IsNullOrEmpty(shop.ItemAssetId))
            {
                problems.Add(Make("Shop", $"id={shop.Id} 的 ItemAssetId 为空"));
                continue;
            }
            if (!assetIdToItemId.ContainsKey(shop.ItemAssetId))
            {
                problems.Add(Make("Shop", $"id={shop.Id} 引用的 ItemAssetId={shop.ItemAssetId} 在 Item 表里不存在"));
            }
        }
    }

    /// <summary>
    /// 武器引用：每个 WeaponConfig 的 weaponID 非空、不重复、在 Weapon 表里有行，
    /// 且 Weapon.weapon_id 应能对应到 Item.id。
    /// 弓的 1111 是既有数据缺陷（计划 §2.7-1），用户已决定本次不修，因此按"已知缺陷"提示。
    /// </summary>
    private static void CheckWeaponReferences(List<Problem> problems)
    {
        var tables = DataRepository.ConfigTables;
        var itemTable = DataRepository.ItemTable;
        var seenWeaponIds = new Dictionary<int, string>();

        foreach (var path in FindAssets("t:WeaponConfig", "Assets"))
        {
            var config = AssetDatabase.LoadAssetAtPath<WeaponConfig>(path);
            if (config == null) continue;

            if (config.weaponID <= 0)
            {
                problems.Add(Make(path, "weaponID 未填写或非法"));
                continue;
            }

            if (seenWeaponIds.TryGetValue(config.weaponID, out string otherPath))
            {
                problems.Add(Make(path, $"weaponID 重复：{config.weaponID}（另一处：{otherPath}）"));
                continue;
            }
            seenWeaponIds[config.weaponID] = path;

            if (tables.TbWeapon.GetOrDefault(config.weaponID) == null)
            {
                problems.Add(Make(path, $"Weapon 表缺少配置行：weapon_id={config.weaponID}"));
            }

            if (itemTable != null && !itemTable.ContainsKey(config.weaponID))
            {
                // 1111：BowConfig 与 Item 10004 Hunting Bow 不一致，属于计划里已经记录、
                // 由用户决定"本次不修"的既有缺陷，只提示不计失败。
                problems.Add(new Problem
                {
                    Where = path,
                    Message = $"weaponID={config.weaponID} 在 Item 表里不存在（弓的既有缺陷，待单独修复）",
                    IsKnownIssue = config.weaponID == 1111,
                });
            }
        }

        // Weapon 表里出现的每个 weapon_id 都应对应一把真实存在的武器 SO
        foreach (var row in tables.TbWeapon.DataList)
        {
            if (!seenWeaponIds.ContainsKey(row.WeaponId))
            {
                problems.Add(Make("Weapon", $"weapon_id={row.WeaponId} 没有对应的 WeaponConfig 资产"));
            }
        }

        if (tables.TbWeaponUpgrade.Data == null)
        {
            problems.Add(Make("WeaponUpgrade", "单行表没有数据行"));
        }
        else if (tables.TbWeaponUpgrade.Data.MaxLevel <= 0)
        {
            problems.Add(Make("WeaponUpgrade", $"max_level 必须大于 0，当前 {tables.TbWeaponUpgrade.Data.MaxLevel}"));
        }
    }

    /// <summary>
    /// 招式引用：attackId 非空、不重复、在 Attack 表里有行；
    /// 命中窗口必须 start &lt;= end；Attack 表的行也必须都有对应资产。
    /// </summary>
    private static void CheckAttackReferences(List<Problem> problems)
    {
        var tables = DataRepository.ConfigTables;
        var seenAttackIds = new Dictionary<string, string>();

        foreach (var path in FindAssets("t:AttackData", "Assets"))
        {
            var attack = AssetDatabase.LoadAssetAtPath<AttackData>(path);
            if (attack == null) continue;

            if (string.IsNullOrEmpty(attack.AttackId))
            {
                problems.Add(Make(path, "attackId 为空（执行 ACDemo/Config/Migration/Apply Attack Ids 可自动补齐）"));
                continue;
            }

            if (seenAttackIds.TryGetValue(attack.AttackId, out string otherPath))
            {
                problems.Add(Make(path, $"attackId 重复：{attack.AttackId}（另一处：{otherPath}）"));
                continue;
            }
            seenAttackIds[attack.AttackId] = path;

            if (tables.TbAttack.GetOrDefault(attack.AttackId) == null)
            {
                problems.Add(Make(path, $"Attack 表缺少配置行：attack_id={attack.AttackId}"));
            }
        }

        foreach (var row in tables.TbAttack.DataList)
        {
            if (row.ImpactStartTime > row.ImpactEndTime)
            {
                problems.Add(Make("Attack",
                    $"attack_id={row.AttackId} 的命中窗口倒置：start={row.ImpactStartTime} > end={row.ImpactEndTime}"));
            }

            if (!seenAttackIds.ContainsKey(row.AttackId))
            {
                problems.Add(Make("Attack", $"attack_id={row.AttackId} 没有对应的 AttackData 资产"));
            }
        }
    }

    /// <summary>单行表（CombatGlobal / 以后的 EnemyGlobal）的取值范围检查</summary>
    private static void CheckGlobalTables(List<Problem> problems)
    {
        var tables = DataRepository.ConfigTables;

        var combat = tables.TbCombatGlobal.Data;
        if (combat == null)
        {
            problems.Add(Make("CombatGlobal", "单行表没有数据行"));
            return;
        }

        if (combat.FallbackBaseDamage < 0f)
        {
            problems.Add(Make("CombatGlobal", $"fallback_base_damage 不能为负：{combat.FallbackBaseDamage}"));
        }
        if (combat.DefaultHitStopDuration < 0f)
        {
            problems.Add(Make("CombatGlobal", $"default_hit_stop_duration 不能为负：{combat.DefaultHitStopDuration}"));
        }
        if (combat.DefaultHitStopTimeScale < 0f || combat.DefaultHitStopTimeScale > 1f)
        {
            problems.Add(Make("CombatGlobal", $"default_hit_stop_time_scale 必须在 0~1：{combat.DefaultHitStopTimeScale}"));
        }
        if (combat.MaxHitStopDuration < 0f)
        {
            problems.Add(Make("CombatGlobal", $"max_hit_stop_duration 不能为负：{combat.MaxHitStopDuration}"));
        }
    }

    private static List<string> FindAssets(string filter, string root)
    {
        var result = new List<string>();
        foreach (var guid in AssetDatabase.FindAssets(filter, new[] { root }))
        {
            result.Add(AssetDatabase.GUIDToAssetPath(guid));
        }
        result.Sort(string.CompareOrdinal);
        return result;
    }

    private static Problem Make(string where, string message)
    {
        return new Problem { Where = where, Message = message };
    }
}

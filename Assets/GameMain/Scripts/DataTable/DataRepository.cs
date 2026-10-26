using System;
using System.Collections.Generic;
using System.IO;
using Luban.SimpleJSON;
using UnityEngine;

/// <summary>
/// 数据表的唯一加载与查询入口（静态类，不新增第三种单例）。
///
/// 【职责】
///   1. 加载一个 Luban `Tables` 对象（Resources/DataTables/Luban/*.json）；
///   2. 提供按配置 ID 查询的强类型入口（GetCharacter / GetStamina ...）；
///   3. 继续解析旧 Shop.txt，并把旧 Item 兼容成 DRItem（第一阶段过渡边界）。
///
/// 【为什么 Initialize 必须幂等】
///   GameManager.Awake、InventoryManager、GmCmd 都会在"表为空"时兜底调用它，
///   不能假定只初始化一次。重复调用直接返回；重入（初始化中再次进入）只告警不重跑。
///
/// 【失败策略】
///   初始化一旦失败就清空全部表并置为 Failed，不留"部分表可用"的假象：
///   Editor 侧由 ACDemo/Config/Export And Validate 拦截，运行时由查询入口报出具体表名与 ID。
/// </summary>
public static class DataRepository
{
    /// <summary>物品表（现有 UI/背包/商店/伤害链路都用这个类型，不要改公开类型）</summary>
    public static Dictionary<int, DRItem> ItemTable { get; private set; }

    /// <summary>商店表（第一阶段仍来自 TSV，不迁移）</summary>
    public static Dictionary<int, DRShop> ShopTable { get; private set; }

    /// <summary>按 NPC 分组的商店表</summary>
    public static Dictionary<int, List<DRShop>> ShopByKeeper { get; private set; }

    /// <summary>物品配置 SO 缓存（图标/Prefab 等 Unity 资源引用）</summary>
    public static Dictionary<int, ItemConfig_SO> ItemConfigCache { get; private set; }

    /// <summary>AssetId → 物品行</summary>
    public static Dictionary<string, DRItem> ItemByAssetId { get; private set; }

    /// <summary>Luban 生成数据的 Resources 目录（不带末尾斜杠）</summary>
    private const string LubanDataRoot = "DataTables/Luban";

    private enum EInitState
    {
        NotInitialized,
        Initializing,
        Ready,
        Failed,
    }

    private static EInitState m_state = EInitState.NotInitialized;
    private static Tables m_tables;
    private static string m_lastError;

    /// <summary>是否已成功加载全部表</summary>
    public static bool IsReady => m_state == EInitState.Ready;

    /// <summary>最近一次初始化失败的原因（未失败时为 null）</summary>
    public static string LastError => m_lastError;

    /// <summary>
    /// Luban 生成的 Tables 对象。未就绪时为 null —— 业务代码不要直接用它，
    /// 一律走下面的 GetXxx 入口，这样将来换配置方案时只改本文件。
    /// </summary>
    public static Tables ConfigTables => m_state == EInitState.Ready ? m_tables : null;

    /// <summary>
    /// 进入 Play 前清空静态缓存：关闭 Domain Reload 时静态字段会跨会话存活，
    /// 不清就会读到上一次 Play 的旧 JSON（表现为"改了表却没生效"）。
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        m_state = EInitState.NotInitialized;
        m_tables = null;
        m_lastError = null;
        ItemTable = null;
        ShopTable = null;
        ShopByKeeper = null;
        ItemConfigCache = null;
        ItemByAssetId = null;
    }

    /// <summary>
    /// 加载全部表。幂等：已就绪直接返回；初始化中重入只告警。
    /// 失败时清空并记录错误，不向调用方抛异常（避免打断 GameManager.Awake 的启动流程），
    /// 但错误日志一定带上具体文件/表名。
    /// </summary>
    public static void Initialize()
    {
        if (m_state == EInitState.Ready) return;
        if (m_state == EInitState.Initializing)
        {
            Debug.LogWarning("[DataRepository] Initialize 重入（初始化尚未结束），忽略本次调用");
            return;
        }

        m_state = EInitState.Initializing;
        try
        {
            // Item 的来源已经切到 Luban，必须先有 Tables 才能构建 DRItem 兼容视图
            LoadLubanTables();
            LoadItemAndShop();

            m_state = EInitState.Ready;
            m_lastError = null;
            Debug.Log($"[DataRepository] 加载完成：{ItemTable.Count} items, {ShopTable.Count} shop rows, " +
                      $"Luban 表 {m_tables.TbCharacter.DataList.Count} character / " +
                      $"{m_tables.TbPlayer.DataList.Count} player / " +
                      $"{m_tables.TbStamina.DataList.Count} stamina / " +
                      $"{m_tables.TbParry.DataList.Count} parry");
        }
        catch (Exception e)
        {
            ClearAll();
            m_state = EInitState.Failed;
            m_lastError = e.Message;
            Debug.LogError($"[DataRepository] 初始化失败，已清空全部表（不留部分可用状态）：{e}");
        }
    }

    /// <summary>
    /// Editor 导出后强制重新加载：清掉已加载的 Tables 与旧表，
    /// 下一次查询/校验会重新读资源，避免"导出成功但内存里还是旧数据"。
    /// </summary>
    public static void ResetForReload()
    {
        m_state = EInitState.NotInitialized;
        m_tables = null;
        m_lastError = null;
        ItemTable = null;
        ShopTable = null;
        ShopByKeeper = null;
        ItemConfigCache = null;
        ItemByAssetId = null;
    }

    // ==================== Luban 强类型查询 ====================

    /// <summary>按配置 ID 取角色生命行；缺失时返回 null 并报出具体 ID</summary>
    public static Character GetCharacter(string configId)
    {
        var tables = EnsureTables("Character", configId);
        if (tables == null) return null;

        var row = tables.TbCharacter.GetOrDefault(configId);
        if (row == null)
        {
            Debug.LogError($"[DataRepository] Character 表缺少配置行：id={FormatId(configId)}。请检查 Gameplay.xlsx 的 Character Sheet");
        }
        return row;
    }

    /// <summary>按配置 ID 取耐力行；缺失时返回 null 并报出具体 ID</summary>
    public static Stamina GetStamina(string configId)
    {
        var tables = EnsureTables("Stamina", configId);
        if (tables == null) return null;

        var row = tables.TbStamina.GetOrDefault(configId);
        if (row == null)
        {
            Debug.LogError($"[DataRepository] Stamina 表缺少配置行：id={FormatId(configId)}。请检查 Gameplay.xlsx 的 Stamina Sheet");
        }
        return row;
    }

    /// <summary>按配置 ID 取玩家手感行；缺失时返回 null 并报出具体 ID</summary>
    public static Player GetPlayer(string configId)
    {
        var tables = EnsureTables("Player", configId);
        if (tables == null) return null;

        var row = tables.TbPlayer.GetOrDefault(configId);
        if (row == null)
        {
            Debug.LogError($"[DataRepository] Player 表缺少配置行：id={FormatId(configId)}。请检查 Gameplay.xlsx 的 Player Sheet");
        }
        return row;
    }

    /// <summary>按配置 ID 取弹反/格挡行；缺失时返回 null 并报出具体 ID</summary>
    public static Parry GetParry(string configId)
    {
        var tables = EnsureTables("Parry", configId);
        if (tables == null) return null;

        var row = tables.TbParry.GetOrDefault(configId);
        if (row == null)
        {
            Debug.LogError($"[DataRepository] Parry 表缺少配置行：id={FormatId(configId)}。请检查 Gameplay.xlsx 的 Parry Sheet");
        }
        return row;
    }

    /// <summary>按武器 ID 取 Weapon 行；缺失时返回 null 并报出具体 ID</summary>
    public static Weapon GetWeapon(int weaponId)
    {
        var tables = EnsureTables("Weapon", weaponId.ToString());
        if (tables == null) return null;

        var row = tables.TbWeapon.GetOrDefault(weaponId);
        if (row == null)
        {
            Debug.LogError($"[DataRepository] Weapon 表缺少配置行：weapon_id={weaponId}。请检查 Gameplay.xlsx 的 Weapon Sheet");
        }
        return row;
    }

    /// <summary>武器升级系数（one 单行表）。读不到时返回 null 并由调用方按 0 处理</summary>
    public static WeaponUpgrade GetWeaponUpgrade()
    {
        var tables = EnsureTables("WeaponUpgrade", "<one>");
        if (tables == null) return null;
        return tables.TbWeaponUpgrade.Data;
    }

    /// <summary>按稳定 attackId 取招式行；缺失时返回 null 并报出具体 ID</summary>
    public static Attack GetAttack(string attackId)
    {
        var tables = EnsureTables("Attack", attackId);
        if (tables == null) return null;

        var row = tables.TbAttack.GetOrDefault(attackId);
        if (row == null)
        {
            Debug.LogError($"[DataRepository] Attack 表缺少配置行：attack_id={FormatId(attackId)}。" +
                           "请检查 Gameplay.xlsx 的 Attack Sheet，或该 AttackData 的 attackId");
        }
        return row;
    }

    /// <summary>按配置 ID 取敌人 AI/FSM 行；缺失时返回 null 并报出具体 ID</summary>
    public static Enemy GetEnemy(string configId)
    {
        var tables = EnsureTables("Enemy", configId);
        if (tables == null) return null;

        var row = tables.TbEnemy.GetOrDefault(configId);
        if (row == null)
        {
            Debug.LogError($"[DataRepository] Enemy 表缺少配置行：id={FormatId(configId)}。请检查 Gameplay.xlsx 的 Enemy Sheet");
        }
        return row;
    }

    /// <summary>敌人管理器全局出手排程（one 单行表）。读不到时返回 null，由调用方按 0 处理</summary>
    public static EnemyGlobal GetEnemyGlobal()
    {
        var tables = EnsureTables("EnemyGlobal", "<one>");
        if (tables == null) return null;
        return tables.TbEnemyGlobal.Data;
    }

    /// <summary>全局战斗默认值（one 单行表）。读不到时返回 null，由调用方按 0 处理</summary>
    public static CombatGlobal GetCombatGlobal()
    {
        var tables = EnsureTables("CombatGlobal", "<one>");
        if (tables == null) return null;
        return tables.TbCombatGlobal.Data;
    }

    /// <summary>确保表已加载，未就绪时返回 null 并带上"哪张表、哪个 ID"的错误信息</summary>
    private static Tables EnsureTables(string tableName, string configId)
    {
        if (m_state != EInitState.Ready) Initialize();
        if (m_state != EInitState.Ready)
        {
            Debug.LogError($"[DataRepository] 读取 {tableName}（id={FormatId(configId)}）时配置未就绪：{m_lastError}");
            return null;
        }
        return m_tables;
    }

    /// <summary>日志里区分"空字符串"和"null"</summary>
    private static string FormatId(string configId)
    {
        if (configId == null) return "<null>";
        return configId.Length == 0 ? "<空字符串>" : configId;
    }

    // ==================== 旧 Item / Shop 兼容 ====================

    /// <summary>
    /// 加载物品表与商店表。
    /// 第一阶段：Item 仍来自 TSV，Shop 仍来自 TSV；
    /// 迁移批次切换到 Luban 后，本方法只保留 Shop 与 ItemConfigCache 的构建。
    /// </summary>
    private static void LoadItemAndShop()
    {
        ItemTable = LoadItemTable();
        (ShopTable, ShopByKeeper) = LoadShopTable();
        BuildItemCaches();
    }

    /// <summary>
    /// 从 Luban Item 表构建旧 DRItem 兼容视图。
    /// DRItem 只是运行时兼容类型（背包/商店/抽卡 UI 在用），数值来源只有 Excel 一处；
    /// 旧 Resources/DataTables/Item.txt 保留在工程里作为回退对照，但初始化不再读取它。
    /// </summary>
    private static Dictionary<int, DRItem> LoadItemTable()
    {
        var dict = new Dictionary<int, DRItem>();
        foreach (var row in m_tables.TbItem.DataList)
        {
            if (dict.ContainsKey(row.Id))
            {
                throw new Exception($"[DataRepository] Item 表主键重复：id={row.Id}");
            }
            dict[row.Id] = ToDRItem(row);
        }
        return dict;
    }

    /// <summary>Luban Item 行 → 现有 DRItem 视图（字段逐一对应，含义与旧 TSV 列一致）</summary>
    private static DRItem ToDRItem(Item row)
    {
        return new DRItem
        {
            Id = row.Id,
            Type = row.Type,
            WeaponType = (E_WeaponType)row.WeaponType,
            BaseDamage = row.BaseDamage,
            CritRate = row.CritRate,
            Star = row.Star,
            Name = row.Name,
            Description = row.Description,
            SkillDescription = row.SkillDescription,
            ImagePath = row.ImagePath,
            AssetId = row.AssetId,
        };
    }

    private static (Dictionary<int, DRShop> byId, Dictionary<int, List<DRShop>> byKeeper) LoadShopTable()
    {
        var shopText = Resources.Load<TextAsset>("DataTables/Shop");
        if (shopText == null)
        {
            Debug.LogWarning("[DataRepository] Resources/DataTables/Shop 未找到，商店表按空处理");
            return (new Dictionary<int, DRShop>(), new Dictionary<int, List<DRShop>>());
        }
        return ParseShopTable(shopText.text);
    }

    /// <summary>构建 AssetId 索引与 ItemConfig_SO 缓存（图标/Prefab 等 Unity 资源引用）</summary>
    private static void BuildItemCaches()
    {
        ItemConfigCache = new Dictionary<int, ItemConfig_SO>();
        ItemByAssetId = new Dictionary<string, DRItem>();
        foreach (var kv in ItemTable)
        {
            if (string.IsNullOrEmpty(kv.Value.AssetId))
            {
                Debug.LogWarning($"[DataRepository] 物品 {kv.Key} 的 AssetId 为空，跳过 SO 缓存");
                continue;
            }

            ItemByAssetId[kv.Value.AssetId] = kv.Value;
            var so = Resources.Load<ItemConfig_SO>($"ItemConfigs/{kv.Value.AssetId}");
            if (so != null)
            {
                ItemConfigCache[kv.Key] = so;
            }
        }
    }

    /// <summary>
    /// 加载 Luban 生成的 Tables。
    /// 使用 Resources.Load 而不是 GameFramework Resource：GF 处于 EditorResourceMode，
    /// 路径规则不同，且配置文件属于随包资源，不需要走 GF 的加载/卸载流程。
    /// </summary>
    private static void LoadLubanTables()
    {
        var tables = new Tables(LoadLubanJson);

        // 生成代码与实际 JSON 对不上时，这里会最早暴露（缺表 = 生成过程没跑完）
        if (tables.TbItem == null) throw new Exception("Luban 生成物缺少 TbItem，请重新导出");
        if (tables.TbCharacter == null) throw new Exception("Luban 生成物缺少 TbCharacter，请重新导出");
        if (tables.TbPlayer == null) throw new Exception("Luban 生成物缺少 TbPlayer，请重新导出");
        if (tables.TbStamina == null) throw new Exception("Luban 生成物缺少 TbStamina，请重新导出");
        if (tables.TbParry == null) throw new Exception("Luban 生成物缺少 TbParry，请重新导出");
        if (tables.TbWeapon == null) throw new Exception("Luban 生成物缺少 TbWeapon，请重新导出");
        if (tables.TbWeaponUpgrade == null) throw new Exception("Luban 生成物缺少 TbWeaponUpgrade，请重新导出");
        if (tables.TbAttack == null) throw new Exception("Luban 生成物缺少 TbAttack，请重新导出");
        if (tables.TbCombatGlobal == null) throw new Exception("Luban 生成物缺少 TbCombatGlobal，请重新导出");
        if (tables.TbEnemy == null) throw new Exception("Luban 生成物缺少 TbEnemy，请重新导出");
        if (tables.TbEnemyGlobal == null) throw new Exception("Luban 生成物缺少 TbEnemyGlobal，请重新导出");

        m_tables = tables;
    }

    /// <summary>Luban 生成的 Tables 会回调本方法按表名取 JSON 根节点</summary>
    private static JSONNode LoadLubanJson(string name)
    {
        string path = $"{LubanDataRoot}/{name}";
        var asset = Resources.Load<TextAsset>(path);
        if (asset == null)
        {
            throw new FileNotFoundException(
                $"[DataRepository] Resources/{path}.json 不存在。请执行 ACDemo/Config/Export And Validate 重新导出");
        }
        return JSON.Parse(asset.text);
    }

    /// <summary>失败时把半初始化结果全部丢掉</summary>
    private static void ClearAll()
    {
        ItemTable = null;
        ShopTable = null;
        ShopByKeeper = null;
        ItemConfigCache = null;
        ItemByAssetId = null;
        m_tables = null;
    }

    // ==================== 旧公开查询入口（行为保持不变） ====================

    public static ItemConfig_SO GetItemConfig(int itemId)
        => ItemConfigCache != null && ItemConfigCache.TryGetValue(itemId, out var so) ? so : null;

    public static ItemConfig_SO GetItemConfig(string assetId)
    {
        var item = GetItemByAssetId(assetId);
        return item != null ? GetItemConfig(item.Id) : null;
    }

    public static Sprite GetItemIcon(int id)
    {
        if (ItemTable != null && ItemTable.TryGetValue(id, out var item) && !string.IsNullOrEmpty(item.ImagePath))
            return Resources.Load<Sprite>(item.ImagePath);
        return null;
    }

    public static Sprite GetItemIcon(string assetId)
    {
        var item = GetItemByAssetId(assetId);
        return item != null ? GetItemIcon(item.Id) : null;
    }

    public static DRItem GetItemByAssetId(string assetId)
        => ItemByAssetId != null && ItemByAssetId.TryGetValue(assetId, out var item) ? item : null;

    public static List<DRShop> GetShopItems(int shopKeeperId)
        => ShopByKeeper != null && ShopByKeeper.TryGetValue(shopKeeperId, out var list) ? list : new List<DRShop>();

    // ==================== TSV 解析（Item 在 Task 6 前仍是来源，Shop 第一阶段保留） ====================

    private static (Dictionary<int, DRShop> byId, Dictionary<int, List<DRShop>> byKeeper) ParseShopTable(string text)
    {
        var rows = TSVParser.Parse(text);
        var byId = new Dictionary<int, DRShop>();
        var byKeeper = new Dictionary<int, List<DRShop>>();
        foreach (var cols in rows)
        {
            var shop = new DRShop
            {
                Id = int.Parse(cols[0]),
                ShopKeeperId = int.Parse(cols[1]),
                ItemAssetId = cols[2],
                Price = int.Parse(cols[3]),
                Stock = int.Parse(cols[4]),
                Discount = float.Parse(cols[5])
            };
            byId[shop.Id] = shop;

            if (!byKeeper.ContainsKey(shop.ShopKeeperId))
                byKeeper[shop.ShopKeeperId] = new List<DRShop>();
            byKeeper[shop.ShopKeeperId].Add(shop);
        }
        return (byId, byKeeper);
    }
}

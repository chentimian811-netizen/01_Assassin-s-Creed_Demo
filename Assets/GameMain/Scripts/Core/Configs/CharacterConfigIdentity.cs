using UnityEngine;

/// <summary>
/// 角色配置身份：挂在角色根对象上，只保存一个稳定的字符串配置 ID。
///
/// 【为什么单独成组件，而不是在 Health / EnemyController 上加字段】
///   1. Health 不需要理解"这个角色是玩家、普通敌人还是 Boss"，
///      也不需要复制三份配置读取逻辑；
///   2. 同一套 ID 同时服务 Character（生命）、Stamina、Player、Enemy、Parry 多张表，
///      放在根对象上只需声明一次；
///   3. 场景/预制体上只允许覆盖"选哪一行配置"的 ID，具体数值一律回 Excel 改。
///
/// 【ID 规则】
///   - 一旦进入正式配置就不再随资产改名而变化；
///   - 空 ID 视为未配置，Editor 校验会报错；
///   - 不要用 Health.UnitId 当配置 ID：那是运行时按 InstanceID 生成的，每次运行都不同。
/// </summary>
[DisallowMultipleComponent]
public class CharacterConfigIdentity : MonoBehaviour
{
    [Header("配置身份")]
    [Tooltip("对应 Character / Stamina / Player / Enemy / Parry 表的主键。\n" +
             "玩家固定 player_main；普通敌人在敌人迁移批次后使用自己的 ID")]
    [SerializeField] private string configId;

    /// <summary>当前配置 ID（可能为空）</summary>
    public string ConfigId => configId;

    /// <summary>是否已经填过配置 ID</summary>
    public bool HasConfigId => !string.IsNullOrEmpty(configId);

    /// <summary>写入配置 ID。运行时与 Editor 迁移工具都会用到</summary>
    public void SetConfigId(string id)
    {
        configId = id;
    }

    /// <summary>
    /// 沿父链解析配置 ID：组件挂在子物体上时也能找到根对象的身份。
    /// 找不到返回 null，由调用方决定是报错还是回落。
    /// </summary>
    public static string ResolveConfigId(Component owner)
    {
        if (owner == null) return null;
        var identity = owner.GetComponentInParent<CharacterConfigIdentity>();
        return identity != null && identity.HasConfigId ? identity.ConfigId : null;
    }

    /// <summary>沿父链解析身份组件本身（需要区分"没有组件"和"组件里 ID 为空"时用）</summary>
    public static CharacterConfigIdentity Resolve(Component owner)
    {
        return owner != null ? owner.GetComponentInParent<CharacterConfigIdentity>() : null;
    }

    // ==================== 统一的"按身份取表行"入口 ====================
    // 把"沿父链找身份 → 读哪张表 → 缺配置怎么报错"收敛到一处，
    // 避免 Health / PlayerMovement / PlayerDodge / ParrySystem 各写一遍解析逻辑。

    /// <summary>取 Character（生命）行</summary>
    public static Character GetCharacterRow(Component owner)
    {
        string configId = ResolveIdOrLog(owner, "Character");
        return configId == null ? null : DataRepository.GetCharacter(configId);
    }

    /// <summary>取 Player（移动/翻滚/锁定）行</summary>
    public static Player GetPlayerRow(Component owner)
    {
        string configId = ResolveIdOrLog(owner, "Player");
        return configId == null ? null : DataRepository.GetPlayer(configId);
    }

    /// <summary>取 Stamina 行</summary>
    public static Stamina GetStaminaRow(Component owner)
    {
        string configId = ResolveIdOrLog(owner, "Stamina");
        return configId == null ? null : DataRepository.GetStamina(configId);
    }

    /// <summary>取 Parry 行</summary>
    public static Parry GetParryRow(Component owner)
    {
        string configId = ResolveIdOrLog(owner, "Parry");
        return configId == null ? null : DataRepository.GetParry(configId);
    }

    /// <summary>解析配置 ID；缺失时报错并返回 null（不静默退回默认值）</summary>
    private static string ResolveIdOrLog(Component owner, string tableName)
    {
        string configId = ResolveConfigId(owner);
        if (!string.IsNullOrEmpty(configId)) return configId;

        Debug.LogError(
            $"[Config] {Describe(owner)} 缺少 CharacterConfigIdentity 或配置 ID 为空，" +
            $"无法读取 {tableName} 表", owner);
        return null;
    }

    private static string Describe(Component owner)
    {
        return owner == null ? "<null>" : $"{owner.GetType().Name}@{owner.name}";
    }
}

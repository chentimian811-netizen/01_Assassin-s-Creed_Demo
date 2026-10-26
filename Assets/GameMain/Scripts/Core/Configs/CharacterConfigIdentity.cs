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
}

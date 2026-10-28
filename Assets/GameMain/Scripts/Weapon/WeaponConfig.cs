using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 武器资源壳：只保留 Unity 资源引用与装备/动作路由标记。
///
/// 【数值来源】Weapon 表（Gameplay.xlsx 的 Weapon Sheet），按 weaponID 查行。
///   攻击距离、弹药、射击冷却、弹速、散布都只在 Excel 里改，SO 上不再留可编辑副本。
///   资源引用（模型 Prefab、动画覆盖器、招式列表、投射物 Prefab）和
///   isRanged / isShield 这两个决定装备路由与动作路由的结构标记仍留在 SO 上（计划 §3.2）。
///
/// 属性名保持与旧序列化字段一致（含 projectilleSpeed 的历史拼写），
/// 这样 RangedFighter 等调用方不需要跟着改。
/// </summary>
[CreateAssetMenu(menuName ="Weapon/WeaponConfig")]
public class WeaponConfig : ScriptableObject
{
    /// <summary>武器 ID：必须等于 Item.id，同时也是 Weapon 表的行键（装备查找与数值取用都靠它）</summary>
    public int weaponID;

    /// <summary>武器名（主要用于识别与日志）</summary>
    public string weaponName;

    public GameObject weaponPrefab;

    public AnimatorOverrideController animOverride;

    public List<AttackData> attacks;

    //远程武器配置
    [Header("远程武器配置")]
    [Tooltip("是否为远程武器")]
    public bool isRanged = false;

    [Tooltip("是否盾类。走武器通道，但默认碰撞体状态与武器不同（见下）")]
    public bool isShield = false;

    [Tooltip("投射物预制体(箭矢)")]
    public GameObject projectilePrefab;

    // ---- Weapon 表行缓存：只缓存成功结果，缺行时每次都走 DataRepository 的报错路径 ----
    private Weapon m_configRow;
    private bool m_rowMissing;

    /// <summary>当前配置行。首次访问时才查表，避免 SO 加载阶段就依赖 DataRepository</summary>
    private Weapon ConfigRow
    {
        get
        {
            if (m_configRow != null) return m_configRow;
            if (m_rowMissing) return null;

            m_configRow = DataRepository.GetWeapon(weaponID);
            if (m_configRow == null)
            {
                m_rowMissing = true;
            }
            return m_configRow;
        }
    }

    /// <summary>攻击距离（米）。当前工程没有消费方，仅保证数据可读</summary>
    public float attackRange => ConfigRow != null ? ConfigRow.AttackRange : 0f;

    /// <summary>最大弹药量</summary>
    public int maxAmmo => ConfigRow != null ? ConfigRow.MaxAmmo : 0;

    /// <summary>射击冷却时间（秒）</summary>
    public float fireCooldown => ConfigRow != null ? ConfigRow.FireCooldown : 0f;

    /// <summary>弹道速度（沿用历史拼写 projectilleSpeed）</summary>
    public float projectilleSpeed => ConfigRow != null ? ConfigRow.ProjectileSpeed : 0f;

    /// <summary>瞄准时散布角度</summary>
    public float aimSpreadAngle => ConfigRow != null ? ConfigRow.AimSpreadAngle : 0f;

    /// <summary>腰射散布角度</summary>
    public float hipFireSpreadAngle => ConfigRow != null ? ConfigRow.HipFireSpreadAngle : 0f;
}

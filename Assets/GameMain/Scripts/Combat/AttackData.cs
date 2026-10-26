using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 招式资源壳：只保留"动画状态名 + 用哪个命中盒 + 稳定 attackId"。
///
/// 【数值来源】Attack 表（Gameplay.xlsx 的 Attack Sheet），按 attackId 查行。
///   命中窗口、伤害倍率、削韧、弹反标记、卡肉时长与时间缩放都不再存在 SO 上，
///   避免"Excel 改了但 asset 里还有一份旧值"。
///
/// 【为什么 attackId 与资产名解耦】
///   资产名可以随便改（复制变体、加前缀），但表主键一旦进入正式配置就不该变；
///   首次填表直接用当时的资产名，之后改名只影响美观，不影响配表关联。
/// </summary>
[CreateAssetMenu(menuName = "Combat System/Create a new attack")]
public class AttackData : ScriptableObject
{
    [Tooltip("稳定招式 ID，对应 Attack 表主键。首次填表用资产名，之后不要随资产改名变化")]
    [SerializeField] private string attackId;

    [field:SerializeField] public string AnimName {  get; private set; }
    [field: SerializeField] public E_AttackHitbox HitboxToUse  { get; private set; }

    // ---- Attack 表行缓存：只缓存成功结果，缺行时每次走 DataRepository 的报错路径 ----
    private Attack m_configRow;
    private bool m_rowMissing;

    /// <summary>稳定招式 ID（Attack 表主键）</summary>
    public string AttackId => attackId;

    private Attack ConfigRow
    {
        get
        {
            if (m_configRow != null) return m_configRow;
            if (m_rowMissing) return null;

            if (string.IsNullOrEmpty(attackId))
            {
                Debug.LogError($"[AttackData] {name} 没有填写 attackId，无法读取 Attack 表", this);
                m_rowMissing = true;
                return null;
            }

            m_configRow = DataRepository.GetAttack(attackId);
            if (m_configRow == null)
            {
                m_rowMissing = true;
            }
            return m_configRow;
        }
    }

    /// <summary>命中窗口起点（动画归一化时间）</summary>
    public float ImpactStartTime => ConfigRow != null ? ConfigRow.ImpactStartTime : 0f;

    /// <summary>命中窗口终点（动画归一化时间）</summary>
    public float ImpactEndTime => ConfigRow != null ? ConfigRow.ImpactEndTime : 0f;

    /// <summary>相对武器基础伤害的倍率</summary>
    public float DamageMultiplier => ConfigRow != null ? ConfigRow.DamageMultiplier : 0f;

    /// <summary>削韧值（当前工程无消费方）</summary>
    public float PoiseDamage => ConfigRow != null ? ConfigRow.PoiseDamage : 0f;

    /// <summary>是否可被弹反</summary>
    public bool Parryable => ConfigRow != null && ConfigRow.Parryable;

    /// <summary>卡肉时长（秒）；负值表示用 HitStopManager 默认值</summary>
    public float HitStopDuration => ConfigRow != null ? ConfigRow.HitStopDuration : 0f;

    /// <summary>卡肉时间缩放；负值表示用默认值</summary>
    public float HitStopTimeScale => ConfigRow != null ? ConfigRow.HitStopTimeScale : 0f;
}

public enum E_AttackHitbox
{
    LeftHande,
    RightHande,
    LeftFoot,
    RightFoot,
    Weapon,

    /// <summary>
    /// 盾牌自身的 BoxCollider（盾击招式用）。
    ///
    /// ⚠️ 必须放在最后：0~4 已经被现有 .asset 序列化成整数，
    /// 插在中间会让所有老配置的判定盒集体错位。
    /// 与 LeftHande 的区别：LeftHande 开的是左手骨骼上的小球（拳掌判定），
    /// 盾击要开的是盾牌这个盒 —— 两者不在一个物体上。
    /// </summary>
    Shield,
}

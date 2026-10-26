using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Combat System/Create a new attack")]
public class AttackData : ScriptableObject
{
    [field:SerializeField] public string AnimName {  get; private set; }
    [field: SerializeField] public E_AttackHitbox HitboxToUse  { get; private set; }
    [field:SerializeField] public float ImpactStartTime {  get; private set; }
    [field:SerializeField] public float ImpactEndTime {  get; private set; }

    [Header("伤害（让「伤害写进 SO」有落点）")]
    [Tooltip("相对武器基础伤害的倍率。轻击 1.0 / 重击 1.8 / 处决 3.0")]
    [SerializeField] private float damageMultiplier = 1f;

    [Tooltip("削韧值：达到敌人 Poise 阈值即打断动作，用于做「霸体/硬直」手感")]
    [SerializeField] private float poiseDamage = 10f;

    [Tooltip("是否可被弹反（不可弹反的招式用来逼玩家翻滚）")]
    [SerializeField] private bool parryable = true;

    [Header("卡肉（0/-1 = 用 HitStopManager 默认值）")]
    [SerializeField] private float hitStopDuration = 0f;
    [SerializeField] private float hitStopTimeScale = -1f;

    public float DamageMultiplier => damageMultiplier;
    public float PoiseDamage => poiseDamage;
    public bool Parryable => parryable;
    public float HitStopDuration => hitStopDuration;
    public float HitStopTimeScale => hitStopTimeScale;
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

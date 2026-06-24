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
    
    [field:SerializeField] public float Damage { get; private set; } = 5f;

    [Header("攻击音效")]
    [Tooltip("该攻击命中的音效（可选，为空则使用 AudioManager 默认音效）")]
    [SerializeField] private AudioClip hitSound;

    /// <summary>
    /// 获取该攻击的音效
    /// </summary>
    public AudioClip HitSound => hitSound;
}

public enum E_AttackHitbox
{
    LeftHande,
    RightHande,
    LeftFoot,
    RightFoot,
    Sword,
}

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
}

public enum E_AttackHitbox
{
    LeftHande,
    RightHande,
    LeftFoot,
    RightFoot,
    Sword,
}

using System.Collections;
using System.Collections.Generic;
using UnityEngine;


[CreateAssetMenu(menuName ="Weapon/WeaponConfig")]
public class WeaponConfig : ScriptableObject
{
    public int weaponID;

    public string weaponName;

    public E_WeaponType weaponType;

    public GameObject weaponPrefab;

    public int baseDamage;

    public float attackRange;

    public RuntimeAnimatorController animOverride;
}

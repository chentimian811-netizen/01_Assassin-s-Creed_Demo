using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName ="Weapon/WeaponConfig")]
public class WeaponConfig : ScriptableObject
{
    public int weaponID;

    public string weaponName;

    public GameObject weaponPrefab;
    public float attackRange;

    public AnimatorOverrideController animOverride;

    public List<AttackData> attacks;
    //远程武器配置
    [Header("远程武器配置")]
    [Tooltip("是否为远程武器")]
    public bool isRanged = false;

    [Tooltip("投射物预制体(箭矢)")]
    public GameObject projectilePrefab;

    [Tooltip("最大弹药量")]
    public int maxAmmo = 30;

    [Tooltip("射击冷却时间（秒）")]
    public float fireCooldown = 0.5f;
    
    [Tooltip("弹道速度")]
    public float projectilleSpeed = 25f;

    [Tooltip("瞄准时散布角度")]
    public float aimSpreadAngle = 1f;

    [Tooltip("腰射散布角度")]
    public float hipFireSpreadAngle = 8f;
}

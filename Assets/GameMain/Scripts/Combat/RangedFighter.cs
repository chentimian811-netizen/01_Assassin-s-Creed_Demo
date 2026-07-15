using System;
using UnityEngine;

/// <summary>
/// 远程战斗控制器 - 处理弓箭的瞄准、射击、弹药管理
/// 挂载在玩家GameObject上，与MeleeFighter并列
/// </summary>
public class RangedFighter : MonoBehaviour
{
    [Header("射击配置")]
    [Tooltip("箭矢生成点(弓的前端位置)")]
    [SerializeField] private Transform firePoint;
    
    [Header("弹药")]
    [Tooltip("当前的弹药量")]
    [SerializeField] private int currentAmmo = 30;

    [Tooltip("最大弹药量")]
    [SerializeField] private int maxAmmo = 30;

    private bool isAiming = false;
    private float lastFireTime = -9999f;
    private WeaponConfig currentWeapon;
    private ProjectilePool projectilePool;

    //供外部系统监听
    public event Action OnFire;
    public event Action<int,int> OnAmmoChange;

    public bool IsAiming => isAiming;
    public int CurrentAmmo => currentAmmo;
    public int MaxAmmo => maxAmmo;

    /// <summary>
    /// 是否可以射击（弹药>0 且 冷却完毕）
    /// </summary>
    public bool CanFire
    {
        get
        {
            bool hasAmmo = currentAmmo > 0;
            bool cooldownReady = Time.time >= lastFireTime + (currentWeapon?.fireCooldown ?? 0.5f);
            return hasAmmo && cooldownReady;   
        }
    }

    private void Awake()
    {
        projectilePool = GetComponent<ProjectilePool>();
        if(projectilePool == null)
        {
            projectilePool = gameObject.AddComponent<ProjectilePool>();
        }
    }

    /// <summary>
    /// 设置当前武器配置（装备武器时调用）
    /// </summary>
    public void SetWeapon(WeaponConfig config)
    {
        currentWeapon = config;

        if(config != null && config.isRanged)
        {
            maxAmmo = config.maxAmmo;
            currentAmmo = maxAmmo;

            //初始化对象池
            if(config.projectilePrefab != null)
            {
                projectilePool.Initialize(config.projectilePrefab,20);
            }

            //通知UI更新
            OnAmmoChange?.Invoke(currentAmmo,maxAmmo);

            Debug.Log($"装备远程武器: {config.weaponName}, 弹药: {currentAmmo}/{maxAmmo}");
        }
    }

    /// <summary>
    /// 清除武器配置（卸下武器时调用）
    /// </summary>
    public void ClearWeapon()
    {
        currentWeapon = null;
        currentAmmo = 0;
        OnAmmoChange?.Invoke(0,0);
    }

    /// <summary>
    /// 设置瞄准状态
    /// </summary>
    public void SetAiming(bool aiming)
    {
        isAiming = aiming;
    }

    /// <summary>
    /// 尝试射击
    /// </summary>
    public void TryFire(Vector3 aimDirection)
    {
        //检查是否可以射击
        if(!CanFire) return;

        //检查武器配置
        if(currentWeapon == null || !currentWeapon.isRanged) return;

        //计算带散布的方向
        Vector3 fireDirection = CalculateSpread(aimDirection);

        //生成箭矢
        SpawnProjectile(fireDirection);

        //消耗弹药
        currentAmmo--;
        lastFireTime = Time.time;

        //触发事件
        OnFire?.Invoke();
        OnAmmoChange?.Invoke(currentAmmo,maxAmmo);
    }

    /// <summary>
    /// 计算弹道散布（根据是否瞄准选择不同精度）
    /// </summary>
    private Vector3 CalculateSpread(Vector3 baseDirection)
    {
        //根据是否瞄准选择散布角度
        float spreadAngle = isAiming
            ? currentWeapon.aimSpreadAngle //瞄准时；高精度
            : currentWeapon.hipFireSpreadAngle;

        //在圆锥范围内随机偏移
        float randomAngleX = UnityEngine.Random.Range(-spreadAngle,spreadAngle);
        float randomAngleY = UnityEngine.Random.Range(-spreadAngle,spreadAngle);

        //应用旋转散布
        Quaternion spreadRotation = Quaternion.Euler(randomAngleX,randomAngleY,0);
        return spreadRotation * baseDirection;
    }

    /// <summary>
    /// 生成箭矢投射物
    /// </summary>
    private void SpawnProjectile(Vector3 direction)
    {
        if(firePoint == null)
        {
            Debug.LogWarning("RangedFighter: 未设置发射点（firePoint）！");
            return;
        }
        //从对象池中获取箭矢
        Projectile projectile = projectilePool.GetProjectile();
        if(projectile != null)
        {
            projectile.Initialize(
                firePoint.position,
                direction,
                currentWeapon.projectilleSpeed,
                (DataRepository.ItemTable.TryGetValue(currentWeapon.weaponID, out var rItem)? rItem.BaseDamage : 5f),
                gameObject
            );
        }
    }

    /// <summary>
    /// 补充弹药
    /// </summary>
    public void Addmmo(int amount)
    {
        currentAmmo = Mathf.Min(currentAmmo + amount , maxAmmo);
        OnAmmoChange?.Invoke(currentAmmo,maxAmmo);
    }

    /// <summary>
    /// 补满弹药
    /// </summary>
    public void Reload()
    {
        currentAmmo = maxAmmo;
        OnAmmoChange?.Invoke(currentAmmo,maxAmmo);
    }
}

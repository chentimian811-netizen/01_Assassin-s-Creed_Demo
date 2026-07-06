using UnityEngine;

/// <summary>
/// 箭矢投射物 - 处理飞行、碰撞、伤害
/// 使用对象池管理，避免频繁创建销毁
/// 未来扩展：可改为基类，派生出不同弹道类型
/// </summary>
public class Projectile : MonoBehaviour
{
    [Header("配置")]
    [Tooltip("最大的飞行时间")]
    [SerializeField] private float maxLifetime = 5f;

    [Tooltip("可碰撞的层级")]
    [SerializeField] private LayerMask hitLayers;

    [Tooltip("命中特效预制体")]
    [SerializeField] private GameObject hitEffectPrefab;

    
    private Vector3 velocity;   
    private float damage;       
    private GameObject owner;   //发射着(防止自伤)
    private float currentLifetime; 
    private bool isActive;      

    //组件引用
    private Collider col;

    private void Awake()
    {
        col = GetComponent<Collider>();
    }


    /// <summary>
    /// 初始化箭矢（由对象池调用）
    /// </summary>
    public void Initialize(Vector3 position, Vector3 direction, float speed, float dmg, GameObject shooter)
    {
        //设置位置和速度
        transform.position = position;
        velocity = direction.normalized * speed;
        damage = dmg;
        owner = shooter;
        currentLifetime = 0f;
        isActive = true;

        //设置箭矢朝向
        if(velocity != Vector3.zero)
        {
            transform.rotation = Quaternion.LookRotation(velocity);
        }

        //激活碰撞体
        if(col != null)
        {
            col.enabled = true;
        }
        else
        {
            Debug.LogWarning($"Projectile.Initialize: col 为 null！箭矢: {gameObject.name}，请检查预制体上是否有 Collider 组件");
        }
    }

    void Update()
    {
        if(!isActive) return;

        //更新生命周期
        currentLifetime += Time.deltaTime;
        if(currentLifetime >= maxLifetime)
        {
            Deactivate();
            return;
        }

        //直线移动
        transform.position += velocity * Time.deltaTime;
    }

    //碰撞检测
    private void OnTriggerEnter(Collider other)
    {
        Debug.Log($"Projectile.OnTriggerEnter 触发！碰撞对象: {other.name}, Layer: {other.gameObject.layer}, isActive: {isActive}");

        if(!isActive) return;

        //忽略发射着自己
        if(other.gameObject == owner) return;

        //检测是否在可碰撞层级内
        if((hitLayers.value & (1 << other.gameObject.layer)) == 0)
        {
            Debug.Log($"Projectile: 层级不匹配，跳过。hitLayers={hitLayers.value}, 目标Layer={other.gameObject.layer}");
            return;
        }

        //对目标造成伤害
        DealDamage(other);

        //生成命中特效
        SpawnHitEffect(other.ClosestPoint(transform.position));

        //停用箭矢(回收到对象池)
        Deactivate();
    }

    /// <summary>
    /// 对目标造成伤害（触发受击/死亡动画）
    /// </summary>
    private void DealDamage(Collider target)
    {
        MeleeFighter fighter = target.GetComponentInParent<MeleeFighter>();
        Debug.Log($"Projectile.DealDamage: 目标={target.name}, MeleeFighter={(fighter != null ? "找到" : "未找到")}, 伤害={damage}");

        if(fighter != null)
        {
            //获取发射者（玩家）的MeleeFighter，而非箭矢自身
            MeleeFighter attacker = owner != null ? owner.GetComponent<MeleeFighter>() : null;
            Debug.Log($"Projectile.DealDamage: attacker={(attacker != null ? attacker.name : "null")}");

            //调用带攻击者的重载，触发受击/死亡动画
            fighter.TakeDamage(damage, attacker);
        }
    }

    /// <summary>
    /// 生成命中特效
    /// </summary>
    private void SpawnHitEffect(Vector3 hitPoint)
    {
        if(hitEffectPrefab != null)
        {
            GameObject effect = Instantiate(hitEffectPrefab,hitPoint,Quaternion.identity);
            Destroy(effect,2f);
        }
    }

    /// <summary>
    /// 停用箭矢（返回对象池）
    /// </summary>
    public void Deactivate()
    {
        isActive = false;

        //禁用碰撞器
        if(col != null)
        {
            col.enabled = false;
        }

        //通知对象池回收
        ProjectilePool pool = GetComponentInParent<ProjectilePool>();
        if(pool != null)
        {
            pool.ReturnProjectile(this);
        }
        else
        {
            gameObject.SetActive(false);
        }
    }

    public void ResetState()
    {   
        velocity = Vector3.zero;
        damage = 0;
        owner = null;
        currentLifetime = 0f;
        isActive = false;
    }
}

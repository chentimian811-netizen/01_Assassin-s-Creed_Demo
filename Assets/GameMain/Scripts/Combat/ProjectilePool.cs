using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ProjectilePool : MonoBehaviour
{
    [Header("配置")]
    [Tooltip("初始池大小")]
    [SerializeField] private int initialSize = 20;

    //对象池队列
    private Queue<Projectile> pool = new Queue<Projectile>();

    //预制体引用
    private GameObject prefab;

    //池父物体
    private Transform poolParent;

    /// <summary>
    /// 初始化对象池
    /// </summary>
    public void Initialize(GameObject projectilePrefab,int size)
    {
        prefab = projectilePrefab;
        initialSize = size;

        //创建池父物体
        if(poolParent != null)
        {
            GameObject poolObj = new GameObject("ProhectilePool");
            poolParent = poolObj.transform;
            poolParent.SetParent(transform);
        }


        //清空现有池中的对象
        foreach(var projectile in pool)
        {
            if(projectile != null)
            {
                Destroy(projectile.gameObject);
            }
        }
        pool.Clear();

        //预先创建指定数量的实例
        for(int i = 0; i < initialSize; i++)
        {
            CreatNewInstance();
        }
    }

    /// <summary>
    /// 从池中获取一个箭矢实例
    /// </summary>
    public Projectile GetProjectile()
    {
        //如果池为空 动态创建新实例
        if(pool.Count == 0)
        {
            CreatNewInstance();
        }

        //从池中取出
        Projectile projectile = pool.Dequeue();

        //激活GameObject
        projectile.gameObject.SetActive(true);

        return projectile;
    }

     /// <summary>
    /// 将箭矢归还到池中
    /// </summary>
    public void ReturnProjectile(Projectile projectile)
    {
        if(projectile == null) return;

        //重置箭矢转台
        projectile.ResetState();

        //禁用GameObject
        projectile.gameObject.SetActive(false);

        //放回池中
        pool.Enqueue(projectile);
    }

    /// <summary>
    /// 创建新的箭矢实例
    /// </summary>
    private void CreatNewInstance()
    {
        if(prefab == null)
        {
            Debug.LogWarning("ProjectilePool:未设置预制体！");
            return;
        }

        //实例化并设置父物体
        GameObject instance = Instantiate(prefab,poolParent);
        instance.SetActive(false);

        //获取或添加projectile组件
        Projectile projectile = instance.GetComponent<Projectile>();
        if(projectile == null)
        {
            projectile = instance.AddComponent<Projectile>();
        }

        //加入池中
        pool.Enqueue(projectile);
    }
}

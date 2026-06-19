using System.Collections;
using System.Collections.Generic;
using UnityEngine;

//使用枚举区分小地图标记类型
public enum E_MinimapMarkerType
{
    Player,
    Enemy,
    NPC,
    Quest,
}

/// <summary>
/// 小地图通用标记组件
/// 挂载位置：需要在小地图显示的对象上（玩家、敌人、NPC等）
/// 功能：生成标记实例、跟随目标位置、自动注册/注销
/// </summary>
public class MinimapMarker : MonoBehaviour
{
    [Header("引用")]
    [Tooltip("小地图控制器，为空时自动查找场景中的实例")]
    [SerializeField] private MinimapController minimapController;

    [Tooltip("标记预制体，为空时使用默认Quad")]
    [SerializeField] private GameObject markerPrefab;

    [Header("标记设置")]
    [Tooltip("标记类型，用于区分玩家/敌人/NPC")]
    [SerializeField] private E_MinimapMarkerType markerType = E_MinimapMarkerType.Enemy;

    [Header("标记设置")]
    [Tooltip("标记颜色")]
    [SerializeField] private Color markerColor = Color.red;

    [Tooltip("标记Y轴偏移(避免于地面重叠)") ]
    [SerializeField] private float markerYOffset = 9.5f;

    [Tooltip("标记缩放大小")]
    [SerializeField] private float markerSize = 0.2f;

    //运行时生成的标记实例
    private GameObject markerInstance;
    
    //标记的渲染器
    private Renderer markerRenderer;

    /// <summary>
    /// 只读属性：获取标记类型
    /// 供 MinimapController 等外部系统识别标记属于谁
    /// </summary>
    public E_MinimapMarkerType MarkerType => markerType;

    private void Awake()
    {
        if(minimapController == null)
        {
            minimapController = FindObjectOfType<MinimapController>();
        }
    }

    private void OnEnable()
    {
        //创建标记
        CreateMarker();

        //注册到管理器
        minimapController?.RegisterMarker(this);
    }

    private void LateUpdate()
    {
        if(markerInstance == null)return;

        //标记跟随模型的世界位置
        markerInstance.transform.position = transform.position + Vector3.up * markerYOffset;

        //小地图标记不需要旋转，保存固定
        markerInstance.transform.rotation = Quaternion.Euler(90f,0f,0f);
    }

    private void OnDisable()
    {
        //从管理器注销
        minimapController?.UnregisterMarker(this);

        //销毁标记实例
        DestroyMarker();
    }

    //创建标记实例
    private void CreateMarker()
    {
        if(markerPrefab != null)
        {
            markerInstance = Instantiate(markerPrefab);

        }
        else
        {
            //默认使用Quad
            markerInstance = GameObject.CreatePrimitive(PrimitiveType.Quad);

            Collider col = markerInstance.GetComponent<Collider>();
            if(col != null)
            {
                Destroy(col);
            }
        }

        //设置标记名称
        markerInstance.name = $"{gameObject.name}_MinimapMarker";

        //设置缩放
        markerInstance.transform.localScale = Vector3.one * markerSize;

        //标记平躺
        markerInstance.transform.rotation = Quaternion.Euler(90f,0f,0f);

        //设置到Minimap
        int minimapLayer = LayerMask.NameToLayer("Minimap");
        if(minimapLayer >= 0)
        {
            markerInstance.layer = minimapLayer;
        }
        else
        {
            Debug.LogWarning($"MinimapEnemyMarker: 未找到名为 'Minimap' 的Layer，请在Edit > Project Settings > Tags and Layers 中添加");
        }

        //获得渲染器并设置颜色
        markerRenderer = markerInstance.GetComponentInChildren<Renderer>();
        if(markerRenderer != null)
        {
            //使用材质颜色
            markerRenderer.material.color = markerColor;
            
        }
    }

    //销毁标记实例
    private void DestroyMarker()
    {
        if(markerInstance != null)
        {
            Destroy(markerInstance);
            markerInstance = null;
            markerRenderer = null;
        }
    }

    //外部调用：设置标记可见性
    public void SetVisible(bool visible)
    {
        if(markerInstance != null)
        {
            markerInstance.SetActive(visible);
        }
    }

    //外部调用：设置颜色
    public void SetColor(Color color)
    {
        markerColor = color;
        if(markerRenderer != null)
        {
            markerRenderer.material.color = color;
        }
    }
}

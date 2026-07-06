using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 小地图控制器
/// 挂载位置：MinimapCamera 对象上
/// 职责：跟随玩家、控制缩放、统一管理小地图标记的注册/注销
/// </summary>
[RequireComponent(typeof(Camera))]
public class MinimapController : MonoBehaviour
{
    [Header("跟随设置")]
    [Tooltip("跟随目标（玩家）")]
    [SerializeField] private Transform target;

    [Tooltip("是否跟随着目标旋转(默认关闭，保持北向朝上)")]
    [SerializeField] private bool rotateWithTarget = false;

    [Tooltip("摄像机高度")]
    [SerializeField] private float height = 50f;

    [Tooltip("跟随平滑时间（秒），越小越跟手")]
    [SerializeField] private float followSmoothTime = 0.08f;

    [Header("缩放设置")]
    [Tooltip("默认正交大小")]
    [SerializeField] private float defaultOrthoSize = 30f;

    [Tooltip("最小缩放")]
    [SerializeField] private float minOrthoSize = 15f;

    [Tooltip("最大缩放")]
    [SerializeField] private float maxOrthoSize = 60f;

    [Tooltip("缩放速度")]
    [SerializeField] private float zoomSpeed = 5f;

    //摄像机组件
    private Camera minimapCamera;

    //SmoothDamp 所需的速度引用
    private Vector3 followVelocity;

    //已注册的标记列表
    private readonly List<MinimapMarker> markers = new();

    private void Awake()
    {
        minimapCamera = GetComponent<Camera>();
    }

    private void Start()
    {
        if(target == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if(player != null)
            {
                target = player.transform;
            }
            else
            {
                Debug.LogWarning("未找到Player标签对象");
            }
        }

        if(minimapCamera != null)
        {
            minimapCamera.orthographic = true;
            minimapCamera.orthographicSize = defaultOrthoSize;
        }
    }

    private void LateUpdate()
    {
        if(target == null) return;

        //计算目标位置
        Vector3 desirePos = new Vector3(target.position.x,
        target.position.y+height,
        target.position.z);

        //使用SmoothDamp平滑跟随(比lerp跟稳定，不受帧率影响)
        transform.position = Vector3.SmoothDamp(
            transform.position,desirePos,
            ref followVelocity,
            followSmoothTime);

        //旋转处理
        if (rotateWithTarget)
        {
            //跟随玩家的Y轴旋转
            transform.rotation = Quaternion.Euler(90f,target.eulerAngles.y,0);
        }
        else
        {
            //固定北向朝上
            transform.rotation = Quaternion.Euler(90f,0f,0f);
        }
    }

    private void Update()
    {
        //鼠标滚轮缩放
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if(Mathf.Abs(scroll) > 0.01f)
        {
            Zoom(scroll);
        }
    }

    //缩放摄像机
    public void Zoom(float delta)
    {
        if(minimapCamera == null) return;

        float newSize = minimapCamera.orthographicSize - delta * zoomSpeed;
        minimapCamera.orthographicSize = Mathf.Clamp(newSize,minOrthoSize,maxOrthoSize);
    }

    //设置缩放大小
    public void SetZoom(float size)
    {
        if(minimapCamera == null) return;
        minimapCamera.orthographicSize = Mathf.Clamp(size,minOrthoSize,maxOrthoSize);
    }

    //重置为默认修改
    public void ResetZoom()
    {
        SetZoom(defaultOrthoSize);
    }

    //注册标记到管理器
    public void RegisterMarker(MinimapMarker marker)
    {
        if(marker == null || markers.Contains(marker)) return;
        markers.Add(marker);
    }

    //从管理器注销标记
    public void UnregisterMarker(MinimapMarker marker)
    {
        if(marker == null) return;
        markers.Remove(marker);
    }

    //获得当前注册的标记数量
    public int MarkerCount => markers.Count;
}

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityGameFramework.Runtime;


/// <summary>
/// UGUI 界面组深度管理辅助器
/// 每个UIGroup对应一个Canvas 通过排序层级区分前后关系
/// </summary>
public class ACUIGroupHelper : UIGroupHelperBase
{
   public const int DepthFactor = 10000;

   private int m_Depth = 0;
   private Canvas m_CachedCanvas = null;


    /// <summary>
    /// 设置界面组深度
    /// </summary>
    /// <param name="depth"></param>
    /// <exception cref="System.NotImplementedException"></exception>
    public override void SetDepth(int depth)
    {
        // 延迟初始化：Awake 时 Canvas 可能还未创建（由 UIGroupHelperBase 管理生命周期）
        if (m_CachedCanvas == null)
        {
            m_CachedCanvas = gameObject.GetOrAddComponent<Canvas>();
            gameObject.GetOrAddComponent<GraphicRaycaster>();
        }
        m_CachedCanvas.overrideSorting = true;
        m_CachedCanvas.sortingOrder = DepthFactor * depth;
    }

    private void Awake()
    {
        // 设置 Canvas 为全屏拉伸（与父容器一致）
        RectTransform rectTransform = GetComponent<RectTransform>();
        if (rectTransform != null)
        {
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.anchoredPosition = Vector2.zero;
            rectTransform.sizeDelta = Vector2.zero;
        }
    }

}

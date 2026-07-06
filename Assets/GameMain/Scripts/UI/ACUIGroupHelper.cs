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
        m_Depth = depth;
        m_CachedCanvas.overrideSorting = true;
        m_CachedCanvas.sortingOrder = DepthFactor * depth;
    }

    private void Awake()
    {
        m_CachedCanvas = gameObject.GetOrAddComponent<Canvas>();
        gameObject.GetOrAddComponent<GraphicRaycaster>();
    }

    private void Start()
    {
        //设置Canvas为全屏拉伸
        m_CachedCanvas.overrideSorting = true;
        m_CachedCanvas.sortingOrder = DepthFactor * m_Depth;

        RectTransform rectTransform = GetComponent<RectTransform>();
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.anchoredPosition = Vector2.zero;
        rectTransform.sizeDelta = Vector2.zero;
        
    }
}

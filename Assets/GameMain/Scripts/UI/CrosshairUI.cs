using UnityEngine.UI;
using UnityEngine;
using System;

/// <summary>
/// 准心UI - 在屏幕中央显示瞄准准心
/// 只在使用远程武器时显示，瞄准时变色变小
public class CrosshairUI : MonoBehaviour
{
    [Header("UI引用")]
    [Tooltip("准心Image组件")]
    [SerializeField] private Image crosshairImage;

    [Header("样式配置")]
    [Tooltip("瞄准时颜色")]
    [SerializeField] private Color aimColor = Color.red;

    [Tooltip("非瞄准时颜色")]
    [SerializeField] private Color normalColor = Color.white;

    [Tooltip("瞄准时的大小")]
    [SerializeField] private float aimSize = 0.5f;

    [Tooltip("非瞄准时的大小")]
    [SerializeField] private float normalSize = 1f;

    // 组件引用
    private WeaponSwitcher weaponSwitcher;
    private RangedFighter rangedFighter;

    private void Start()
    {
        weaponSwitcher = FindObjectOfType<WeaponSwitcher>();
        rangedFighter = FindObjectOfType<RangedFighter>();

        SetCrosshairVisible(false);
    }

    private void Update()
    {
        if(weaponSwitcher == null || rangedFighter == null) return;

        //只有在远程的时候才显示准心
        bool shouldShow = weaponSwitcher.IsUsingRanged;
        SetCrosshairVisible(shouldShow);

        if (shouldShow)
        {
            UpdateCrosshairSyle();
        }
    }

    /// <summary>
    /// 更新准心样式（颜色、大小）
    /// </summary>
    private void UpdateCrosshairSyle()
    {
        bool isAiming = rangedFighter.IsAiming;

        //根据瞄准切换颜色
        crosshairImage.color = isAiming?aimColor : normalColor;

        //根据瞄准切换大小
        float targetSize = isAiming ? aimSize : normalSize;
        transform.localScale = Vector3.one * targetSize;
    }

    /// <summary>
    /// 设置准心可见性
    /// </summary>
    private void SetCrosshairVisible(bool visible)
    {
        if(crosshairImage != null)
        {
            crosshairImage.enabled = visible;
        }
    }
}

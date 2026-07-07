//------------------------------------------------------------
// ACDemo — 加载界面（GF UIFormLogic 版）
// 放置路径: Assets/GameMain/Scripts/UI/LoadingForm.cs
// 对应 Prefab: Assets/UI/Prefabs/Loading/LoadingForm.prefab
// 分组: Loading（深度 4，盖住一切）
//------------------------------------------------------------

using UnityEngine;
using UnityEngine.UI;
using UnityGameFramework.Runtime;

/// <summary>
/// 加载过渡界面 — 显示场景加载进度条和随机贴士。
/// 通过订阅 LoadProgressEventArgs 实时更新进度。
/// </summary>
public class LoadingForm : UIFormLogic
{
    [Header("UI 引用")]
    [SerializeField] private Slider progressSlider;       // 进度条
    [SerializeField] private Image progressFillImage;      // 备选：填充图（无 Slider 时用 fillAmount）
    [SerializeField] private Text progressText;            // 百分比文本（如 "45%"）
    [SerializeField] private Text tipText;                 // 随机贴士

    [Header("贴士设置")]
    [SerializeField] private string[] tips = new string[]
    {
        "格挡可以大幅减少受到的伤害",
        "翻滚时短暂无敌，利用它躲避致命攻击",
        "探索每个角落，你可能会发现隐藏的宝箱",
        "不同的武器有不同的连招节奏",
        "注意观察敌人的攻击前摇，提前做出反应",
    };

    // ==================== UIFormLogic 生命周期 ====================

    protected override void OnOpen(object userData)
    {
        base.OnOpen(userData);

        // 随机选择一条贴士
        if (tipText != null && tips != null && tips.Length > 0)
        {
            int index = Random.Range(0, tips.Length);
            tipText.text = tips[index];
        }

        // 初始化进度为 0
        SetProgress(0f);

        // 订阅进度更新事件（由 ProcedureLoading 每帧广播）
        GameEntry.Event.Subscribe(LoadProgressEventArgs.EventId, OnLoadProgress);
    }

    protected override void OnClose(bool isShutdown, object userData)
    {
        // 取消订阅，防止内存泄漏
        GameEntry.Event.Unsubscribe(LoadProgressEventArgs.EventId, OnLoadProgress);

        base.OnClose(isShutdown, userData);
    }

    // ==================== 事件回调 ====================

    /// <summary>
    /// 接收场景加载进度事件，更新 UI。
    /// </summary>
    private void OnLoadProgress(object sender, GameFramework.Event.GameEventArgs e)
    {
        if (e is not LoadProgressEventArgs args) return;
        SetProgress(args.Progress);
    }

    // ==================== UI 更新 ====================

    /// <summary>
    /// 更新进度显示（0.0 ~ 1.0）。
    /// </summary>
    private void SetProgress(float progress)
    {
        if (progressSlider != null)
        {
            progressSlider.value = progress;
        }

        if (progressFillImage != null)
        {
            progressFillImage.fillAmount = progress;
        }

        if (progressText != null)
        {
            progressText.text = $"{Mathf.FloorToInt(progress * 100)}%";
        }
    }
}

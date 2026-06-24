using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 暂停面板 - 按 ESC 打开/关闭，提供继续游戏和返回主菜单功能
/// </summary>
public class PausePanel : BasePanel
{
    [SerializeField] private Button resumeButton;    // 继续游戏按钮
    [SerializeField] private Button mainMenuButton;  // 返回主菜单按钮

    protected override void Awake()
    {
        base.Awake();

        // 绑定按钮点击事件
        if (resumeButton != null)
            resumeButton.onClick.AddListener(OnClickResume);

        if (mainMenuButton != null)
            mainMenuButton.onClick.AddListener(OnClickMainMenu);
    }

    /// <summary>
    /// 点击继续游戏 - 通过 PlayerController 恢复游戏状态
    /// </summary>
    private void OnClickResume()
    {
        PlayerController player = FindObjectOfType<PlayerController>();
        if (player != null)
        {
            player.ResumeGame();
        }
    }

    /// <summary>
    /// 点击返回主菜单 - 恢复时间并跳转主菜单
    /// </summary>
    private void OnClickMainMenu()
    {
        // 先恢复时间缩放，再关闭面板
        Time.timeScale = 1f;
        UIManager.Instance.ClosePanel(UIconst.PausePanel);

        // 调用 GameManager 返回主菜单
        if (GameManager.Instance != null)
        {
            GameManager.Instance.ReturnToMainMenu();
        }
    }
}

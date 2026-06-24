using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 游戏获胜面板 - Boss 被击败且玩家到达门时显示
/// 包含重新开始和回到主菜单两个按钮
/// </summary>
public class VictoryPanel : BasePanel
{
    [SerializeField] private Button mainMenuBtn;   // 回到主菜单按钮
    [SerializeField] private Button restartBtn;     // 重新开始按钮
    [SerializeField] private Text titleText;        // 标题文本

    protected override void Awake()
    {
        base.Awake();
        if (mainMenuBtn != null)
            mainMenuBtn.onClick.AddListener(OnMainMenu);
        if (restartBtn != null)
            restartBtn.onClick.AddListener(OnRestart);
    }

    public override void OpenPanel(string name)
    {
        base.OpenPanel(name);
        // 显示鼠标，暂停游戏
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        Time.timeScale = 0f;

        // 播放获胜音效（如果有的话）
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.StopMusic();
        }
    }

    public override void ClosePanel()
    {
        Time.timeScale = 1f;
        base.ClosePanel();
    }

    /// <summary>
    /// 回到主菜单
    /// </summary>
    private void OnMainMenu()
    {
        ClosePanel();  // 先关闭胜利面板
        GameManager.Instance.ReturnToMainMenu();
    }

    /// <summary>
    /// 重新开始游戏
    /// </summary>
    private void OnRestart()
    {
        ClosePanel();  // 先关闭胜利面板
        GameManager.Instance.RespawnPlayer();
    }
}

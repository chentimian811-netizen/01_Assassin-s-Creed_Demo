using UnityEngine;
using UnityEngine.UI;

public class DeathPanel : BasePanel
{
    [SerializeField] private Button mainMenuBtn;
    [SerializeField] private Button restartBtn;
    [SerializeField] private Text titleText;

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
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        Time.timeScale = 0f;

        // 玩家死亡时停止游戏BGM
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

    private void OnMainMenu()
    {
        GameManager.Instance.ReturnToMainMenu();
    }

    private void OnRestart()
    {
        GameManager.Instance.RespawnPlayer();
    }
}

using UnityEngine;

/// <summary>
/// 音频管理器 - 单例模式，管理背景音乐和音效播放
/// </summary>
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("音频源")]
    [Tooltip("背景音乐播放器")]
    [SerializeField] private AudioSource musicSource;

    [Tooltip("音效播放器")]
    [SerializeField] private AudioSource sfxSource;

    [Header("背景音乐")]
    [Tooltip("主菜单背景音乐")]
    [SerializeField] private AudioClip mainMenuBGM;

    [Header("玩家音效")]
    [Tooltip("玩家普通攻击命中音效（第一、二段）")]
    [SerializeField] private AudioClip playerNormalAttackSFX;

    [Tooltip("玩家重攻击命中音效（第三段）")]
    [SerializeField] private AudioClip playerHeavyAttackSFX;

    [Tooltip("玩家攻击挥空音效")]
    [SerializeField] private AudioClip playerAttackMissSFX;

    [Tooltip("玩家死亡音效")]
    [SerializeField] private AudioClip playerDeathSFX;

    [Header("敌人音效")]
    [Tooltip("敌人死亡音效")]
    [SerializeField] private AudioClip enemyDeathSFX;

    [Header("处决音效")]
    [Tooltip("处决/反击音效")]
    [SerializeField] private AudioClip counterAttackSFX;

    private void Awake()
    {
        // 单例模式，跨场景保留
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    /// <summary>
    /// 播放背景音乐
    /// </summary>
    /// <param name="clip">音乐片段，null则使用默认主菜单BGM</param>
    /// <param name="loop">是否循环</param>
    public void PlayMusic(AudioClip clip = null, bool loop = true)
    {
        if (musicSource == null)
        {
            Debug.LogWarning("[AudioManager] musicSource 为 null！请在 Inspector 中拖入 AudioSource");
            return;
        }

        AudioClip targetClip = clip ?? mainMenuBGM;
        if (targetClip == null)
        {
            Debug.LogWarning("[AudioManager] mainMenuBGM 为 null！请在 Inspector 中拖入背景音乐文件");
            return;
        }

        // 如果正在播放相同的音乐，不重复播放
        if (musicSource.clip == targetClip && musicSource.isPlaying)
        {
            Debug.Log("[AudioManager] BGM 正在播放中，跳过重复播放");
            return;
        }

        Debug.Log($"[AudioManager] 播放 BGM: {targetClip.name}");
        musicSource.clip = targetClip;
        musicSource.loop = loop;
        musicSource.Play();
    }

    /// <summary>
    /// 停止背景音乐
    /// </summary>
    public void StopMusic()
    {
        if (musicSource != null)
        {
            musicSource.Stop();
        }
    }

    /// <summary>
    /// 暂停背景音乐
    /// </summary>
    public void PauseMusic()
    {
        if (musicSource != null)
        {
            musicSource.Pause();
        }
    }

    /// <summary>
    /// 恢复背景音乐
    /// </summary>
    public void ResumeMusic()
    {
        if (musicSource != null)
        {
            musicSource.UnPause();
        }
    }

    /// <summary>
    /// 播放玩家普通攻击命中音效（第一、二段）
    /// </summary>
    public void PlayPlayerNormalAttackSFX()
    {
        PlaySFX(playerNormalAttackSFX);
    }

    /// <summary>
    /// 播放玩家重攻击命中音效（第三段）
    /// </summary>
    public void PlayPlayerHeavyAttackSFX()
    {
        PlaySFX(playerHeavyAttackSFX);
    }

    /// <summary>
    /// 播放玩家攻击挥空音效
    /// </summary>
    public void PlayPlayerAttackMissSFX()
    {
        PlaySFX(playerAttackMissSFX);
    }

    /// <summary>
    /// 播放玩家死亡音效
    /// </summary>
    public void PlayPlayerDeathSFX()
    {
        PlaySFX(playerDeathSFX);
    }

    /// <summary>
    /// 播放敌人死亡音效
    /// </summary>
    public void PlayEnemyDeathSFX()
    {
        PlaySFX(enemyDeathSFX);
    }

    /// <summary>
    /// 播放处决/反击音效
    /// </summary>
    public void PlayCounterAttackSFX()
    {
        PlaySFX(counterAttackSFX);
    }

    /// <summary>
    /// 播放指定音效片段（通用方法）
    /// </summary>
    /// <param name="clip">要播放的音效</param>
    public void PlaySFXClip(AudioClip clip)
    {
        PlaySFX(clip);
    }

    /// <summary>
    /// 播放音效内部方法
    /// </summary>
    private void PlaySFX(AudioClip clip)
    {
        if (sfxSource == null)
        {
            Debug.LogWarning("[AudioManager] sfxSource 为 null！请在 Inspector 中拖入 AudioSource");
            return;
        }
        if (clip == null)
        {
            Debug.LogWarning("[AudioManager] 音效片段为 null！");
            return;
        }
        Debug.Log($"[AudioManager] 播放音效: {clip.name}");
        sfxSource.PlayOneShot(clip);
    }

    /// <summary>
    /// 设置背景音乐音量
    /// </summary>
    /// <param name="volume">音量 0-1</param>
    public void SetMusicVolume(float volume)
    {
        if (musicSource != null)
        {
            musicSource.volume = Mathf.Clamp01(volume);
        }
    }

    /// <summary>
    /// 设置音效音量
    /// </summary>
    /// <param name="volume">音量 0-1</param>
    public void SetSFXVolume(float volume)
    {
        if (sfxSource != null)
        {
            sfxSource.volume = Mathf.Clamp01(volume);
        }
    }
}

using System.Collections;
using UnityEngine;

/// <summary>
/// 时间缩放与时停（卡肉）的唯一所有者。
///
/// 【为什么必须独占】
///   工程中原本有 4 处直接写 Time.timeScale：MeleeFighter 卡肉、ProcedureGame 暂停、
///   旧 UIManager 暂停、ProcedureGame.OnEnter 复位。
///   如果卡肉协程保存/恢复 prev 值，就会出现：
///     暂停中触发一次卡肉 → 卡肉结束把 timeScale 恢复成 1 → 暂停被意外解除。
///   因此统一由本组件裁决：暂停优先，卡肉不覆盖暂停。
/// </summary>
[DisallowMultipleComponent]
public class HitStopManager : MonoBehaviour
{
    public static HitStopManager Instance { get; private set; }

    [Header("默认卡肉参数")]
    [Tooltip("顿帧时长（真实时间，不受 timeScale 影响）")]
    [SerializeField] private float defaultDuration = 0.1f;

    [Tooltip("顿帧期间的时间缩放。0 = 完全冻结（Animator/物理/NavMesh 全停），建议 0.05 保留一点动感")]
    [Range(0f, 1f)]
    [SerializeField] private float defaultTimeScale = 0.05f;

    [Tooltip("单次卡肉时长上限，防止连续触发叠成长时间冻结")]
    [SerializeField] private float maxDuration = 0.3f;

    // ---- 时间缩放状态 ----
    private bool isPaused;
    private float pauseTimeScale = 0f;

    // ---- 卡肉状态 ----
    private bool isHitStopping;
    private Coroutine hitStopRoutine;

    /// <summary>是否处于暂停（UI/菜单）</summary>
    public bool IsPaused => isPaused;

    /// <summary>是否处于卡肉</summary>
    public bool IsHitStopping => isHitStopping;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[HitStopManager] 场景中存在多个实例，销毁多余的一个", this);
            Destroy(this);
            return;
        }
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>
    /// 确保场景中存在 HitStopManager。ProcedureGame.OnEnter 调用。
    /// </summary>
    public static HitStopManager EnsureExists()
    {
        if (Instance != null) return Instance;

        var existing = Object.FindObjectOfType<HitStopManager>();
        if (existing != null)
        {
            Instance = existing;
            return Instance;
        }

        var go = new GameObject("[HitStopManager]");
        Object.DontDestroyOnLoad(go);
        Instance = go.AddComponent<HitStopManager>();
        return Instance;
    }

    // ==================== 卡肉 ====================

    /// <summary>播放顿帧。duration/timeScale 传负值表示用默认。</summary>
    public void Play(float duration = -1f, float timeScale = -1f)
    {
        // 暂停中不允许卡肉，否则恢复时会打乱暂停状态
        if (isPaused) return;

        float d = duration >= 0f ? duration : defaultDuration;
        float s = timeScale >= 0f ? timeScale : defaultTimeScale;
        d = Mathf.Min(d, maxDuration);

        if (hitStopRoutine != null) StopCoroutine(hitStopRoutine);
        hitStopRoutine = StartCoroutine(CoHitStop(d, s));
    }

    private IEnumerator CoHitStop(float duration, float timeScale)
    {
        isHitStopping = true;
        ApplyTimeScale(timeScale);

        // 用真实时间等待，因为 timeScale 已被压低
        yield return new WaitForSecondsRealtime(duration);

        isHitStopping = false;
        hitStopRoutine = null;

        // 恢复到"当前应有的状态"（可能期间被暂停了）
        ReapplyCurrentState();
    }

    // ==================== 暂停 ====================

    /// <summary>暂停游戏（打开菜单/死亡演出）。替代各处直接写 Time.timeScale = 0。</summary>
    public void Pause(float timeScale = 0f)
    {
        isPaused = true;
        pauseTimeScale = timeScale;

        // 暂停优先：中断正在进行的卡肉
        if (hitStopRoutine != null)
        {
            StopCoroutine(hitStopRoutine);
            hitStopRoutine = null;
            isHitStopping = false;
        }

        ApplyTimeScale(timeScale);
    }

    /// <summary>恢复游戏。</summary>
    public void Resume()
    {
        isPaused = false;
        ReapplyCurrentState();
    }

    // ==================== 内部 ====================

    /// <summary>按"暂停优先"原则重新应用时间缩放</summary>
    private void ReapplyCurrentState()
    {
        if (isPaused)
        {
            ApplyTimeScale(pauseTimeScale);
        }
        else
        {
            ApplyTimeScale(1f);
        }
    }

    private void ApplyTimeScale(float value)
    {
        Time.timeScale = value;
    }
}

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityGameFramework.Runtime;

/// <summary>
/// 敌人头顶血条层（HUD 组 UIForm）。
///
/// 为什么用屏幕空间 + 池，而不是给每个敌人挂 World Space Canvas：
///   1. 不用改 Enemy.prefab，敌人数量变化不影响预制体；
///   2. 所有血条共用一张 Canvas，draw call 与层级都好控；
///   3. 屏幕外 / 相机背后 / 超距的剔除只需一次坐标换算。
///
/// 显示规则（魂系手感）：
///   受击后 showDuration 秒内可见；锁定目标（LockedEnemy）与软锁目标常显。
/// </summary>
public class EnemyHealthBarsForm : UIFormLogic
{
    [Header("引用")]
    [Tooltip("血条挂载层，留空则用本界面根节点")]
    [SerializeField] private RectTransform barLayer;

    [Tooltip("默认 inactive 的模板节点，运行时克隆")]
    [SerializeField] private EnemyHealthBarWidget widgetTemplate;

    [Header("显示规则")]
    [Tooltip("受击后持续显示的时间（秒）")]
    [SerializeField] private float showDuration = 3f;

    [Tooltip("超过这个距离（米）不显示")]
    [SerializeField] private float maxDistance = 25f;

    [Tooltip("血条相对头部骨骼的偏移")]
    [SerializeField] private Vector3 headOffset = new Vector3(0f, 0.35f, 0f);

    [Header("池")]
    [Tooltip("最多同时绑定多少条血条（场景敌人数量上限）")]
    [SerializeField] private int maxBars = 8;

    private class Entry
    {
        public EnemyController Enemy;
        public Health Health;
        public Transform Head;
        public EnemyHealthBarWidget Widget;
        public float LastDamagedTime = -9999f;
    }

    private readonly List<Entry> m_entries = new List<Entry>();
    private RectTransform m_canvasRect;
    private PlayerController m_player;
    private Camera m_camera;
    private bool m_subscribed;

    protected override void OnOpen(object userData)
    {
        base.OnOpen(userData);

        // 与 MainHUDForm 同理：GF 把 Form 实例 SetParent 到 UI 组 Canvas 后，
        // 根节点可能还停在 0×0，这里显式铺满一次，投影坐标才有意义。
        ForceFillParentCanvas();

        Canvas canvas = GetComponentInParent<Canvas>();
        m_canvasRect = canvas != null ? canvas.transform as RectTransform : transform as RectTransform;
        if (barLayer == null) barLayer = transform as RectTransform;

        m_player = null;
        m_camera = null;

        BuildEntries();

        GameEvents.OnUnitDamaged += HandleUnitDamaged;
        GameEvents.OnEnemyKilled += HandleEnemyKilled;
        m_subscribed = true;
    }

    private void ForceFillParentCanvas()
    {
        RectTransform rect = transform as RectTransform;
        if (rect == null) return;

        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.localScale = Vector3.one;
    }

    protected override void OnClose(bool isShutdown, object userData)
    {
        if (m_subscribed)
        {
            GameEvents.OnUnitDamaged -= HandleUnitDamaged;
            GameEvents.OnEnemyKilled -= HandleEnemyKilled;
            m_subscribed = false;
        }

        base.OnClose(isShutdown, userData);
    }

    /// <summary>场景里的敌人一次性建表（本项目敌人是场景摆放，没有运行时生成）</summary>
    private void BuildEntries()
    {
        m_entries.Clear();

        if (widgetTemplate == null)
        {
            Debug.LogError("[EnemyHealthBarsForm] widgetTemplate 未绑定，敌人血条不会显示", this);
            return;
        }

        widgetTemplate.gameObject.SetActive(false);

        EnemyController[] enemies = FindObjectsOfType<EnemyController>();
        for (int i = 0; i < enemies.Length && m_entries.Count < maxBars; i++)
        {
            EnemyController enemy = enemies[i];
            if (enemy == null || !enemy.isActiveAndEnabled) continue;

            Health health = enemy.GetComponent<Health>();
            if (health == null) continue;

            Animator animator = enemy.GetComponent<Animator>();
            Transform head = animator != null ? animator.GetBoneTransform(HumanBodyBones.Head) : null;

            EnemyHealthBarWidget widget = Instantiate(widgetTemplate, barLayer);
            widget.name = "Bar_" + enemy.name;
            widget.gameObject.SetActive(true);
            widget.SetValue(health.CurrentHealth, health.MaxHealth, true);
            widget.SetVisible(false);

            m_entries.Add(new Entry
            {
                Enemy = enemy,
                Health = health,
                Head = head,
                Widget = widget,
                LastDamagedTime = -9999f,
            });
        }

        Debug.Log($"[EnemyHealthBarsForm] 敌人血条已绑定 {m_entries.Count} 个");
    }

    private void LateUpdate()
    {
        if (m_entries.Count == 0) return;

        if (m_camera == null) m_camera = ResolveCamera();
        if (m_camera == null || m_canvasRect == null) return;

        if (m_player == null) m_player = FindObjectOfType<PlayerController>(true);

        Vector3 cameraPosition = m_camera.transform.position;
        float maxDistanceSqr = maxDistance * maxDistance;
        Vector2 halfCanvas = m_canvasRect.rect.size * 0.5f;

        for (int i = 0; i < m_entries.Count; i++)
        {
            Entry entry = m_entries[i];
            if (entry.Enemy == null || entry.Health == null || entry.Widget == null) continue;

            bool visible = !entry.Health.IsDead
                && (IsLockedTarget(entry.Enemy) || Time.time - entry.LastDamagedTime <= showDuration);

            if (!visible)
            {
                entry.Widget.SetVisible(false);
                continue;
            }

            Vector3 world = entry.Head != null
                ? entry.Head.position + headOffset
                : entry.Enemy.transform.position + Vector3.up * 2f;

            if (Vector3.SqrMagnitude(world - cameraPosition) > maxDistanceSqr)
            {
                entry.Widget.SetVisible(false);
                continue;
            }

            Vector3 screen = m_camera.WorldToScreenPoint(world);
            if (screen.z <= 0f)
            {
                entry.Widget.SetVisible(false);
                continue;
            }

            Vector2 local;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    m_canvasRect, screen, null, out local))
            {
                entry.Widget.SetVisible(false);
                continue;
            }

            // 屏幕外剔除：留 80px 余量，避免贴边时血条突然出现/消失
            if (Mathf.Abs(local.x) > halfCanvas.x + 80f || Mathf.Abs(local.y) > halfCanvas.y + 80f)
            {
                entry.Widget.SetVisible(false);
                continue;
            }

            entry.Widget.SetLocalPosition(local);
            entry.Widget.SetVisible(true);
        }
    }

    private Camera ResolveCamera()
    {
        if (CameraManager.Instance != null && CameraManager.Instance.MainCameraTransform != null)
        {
            Camera cam = CameraManager.Instance.MainCameraTransform.GetComponent<Camera>();
            if (cam != null) return cam;
        }

        return Camera.main;
    }

    private bool IsLockedTarget(EnemyController enemy)
    {
        if (m_player == null || enemy == null) return false;
        return m_player.LockedEnemy == enemy || m_player.TargetEnemy == enemy;
    }

    private void HandleUnitDamaged(string victimUnitId, DamageInfo info)
    {
        for (int i = 0; i < m_entries.Count; i++)
        {
            Entry entry = m_entries[i];
            if (entry.Health == null || entry.Health.UnitId != victimUnitId) continue;

            entry.LastDamagedTime = Time.time;
            entry.Widget.SetValue(entry.Health.CurrentHealth, entry.Health.MaxHealth);
            entry.Widget.ShowImmediate();
            return;
        }
    }

    private void HandleEnemyKilled(string unitId)
    {
        for (int i = 0; i < m_entries.Count; i++)
        {
            Entry entry = m_entries[i];
            if (entry.Health == null || entry.Health.UnitId != unitId) continue;

            entry.LastDamagedTime = -9999f;
            entry.Widget.SetVisible(false);
            return;
        }
    }
}

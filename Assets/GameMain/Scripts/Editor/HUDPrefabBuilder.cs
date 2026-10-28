using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 用 UGUI / UnityEditor API 真正创建 HUD 节点，并存成可编辑的预制体。
///
/// 为什么用生成器而不是手写 YAML：
///   手写 prefab 的 fileID / 组件序列化极易出错，而且以后改布局要重写；
///   生成器只操作「它自己创建的两个子树」，可反复执行，也不会碰你手改的按钮。
///
/// 生成内容：
///   MainHUDForm.prefab
///     ├─ PlayerStatusRoot（左上：HP / MP / 耐力）
///     └─ QuickSlotRoot（左下：十字四槽）
///   EnemyHealthBarForm.prefab
///     └─ BarLayer / WidgetTemplate（敌人头顶血条模板）
///
/// 菜单：
///   ACDemo/UI/HUD/Build Player HUD        仅当子树不存在时创建
///   ACDemo/UI/HUD/Rebuild Player HUD      删掉生成的子树后重建（会覆盖手改）
///   ACDemo/UI/HUD/Build Enemy Health Bars 创建敌人血条预制体
///   ACDemo/UI/HUD/Rebuild Everything      全部重建 + 校验（batchmode 用）
/// </summary>
public static class HUDPrefabBuilder
{
    private const string HudPrefabPath = "Assets/UI/Prefabs/HUD/MainHUDForm.prefab";
    private const string EnemyFormPrefabPath = "Assets/UI/Prefabs/HUD/EnemyHealthBarForm.prefab";
    private const string SpriteDir = "Assets/UI/Resources/Sprites/UI/HUD";

    private const string PlayerPrefabPath = "Assets/GameMain/Entities/Player/Male.prefab";

    // 布局常量：与样张一致（1920x1080 参考分辨率）
    private const float MarginX = 44f;
    private const float MarginTop = 36f;
    private static readonly Vector2 HealthSize = new Vector2(520f, 18f);
    private static readonly Vector2 ManaSize = new Vector2(360f, 12f);
    private static readonly Vector2 StaminaSize = new Vector2(440f, 12f);
    private const float BarGap = 8f;

    private static readonly Vector2 SlotRootPosition = new Vector2(56f, 56f);
    private const float SlotSize = 64f;
    private const float SlotGap = 8f;

    private static readonly Color FrameColor = new Color(0.42f, 0.38f, 0.31f, 1f);
    private static readonly Color GhostColor = new Color(0.85f, 0.64f, 0.25f, 0.95f);
    private static readonly Color HealthColor = new Color(0.65f, 0.17f, 0.17f, 1f);
    private static readonly Color ManaColor = new Color(0.18f, 0.43f, 0.85f, 1f);
    private static readonly Color StaminaColor = new Color(0.30f, 0.60f, 0.30f, 1f);
    private static readonly Color SelectedSlotColor = new Color(0.88f, 0.75f, 0.44f, 1f);

    // ==================== 菜单 ====================

    [MenuItem("ACDemo/UI/HUD/Build Player HUD", priority = 100)]
    public static void BuildPlayerHudMenu()
    {
        BuildPlayerHud(false);
        AssetDatabase.SaveAssets();
    }

    [MenuItem("ACDemo/UI/HUD/Rebuild Player HUD", priority = 101)]
    public static void RebuildPlayerHudMenu()
    {
        BuildPlayerHud(true);
        AssetDatabase.SaveAssets();
        VerifyPlayerHudLayout();
    }

    [MenuItem("ACDemo/UI/HUD/Build Enemy Health Bars", priority = 102)]
    public static void BuildEnemyBarsMenu()
    {
        BuildEnemyBars(false);
        AssetDatabase.SaveAssets();
    }

    [MenuItem("ACDemo/UI/HUD/Rebuild Enemy Health Bars", priority = 103)]
    public static void RebuildEnemyBarsMenu()
    {
        BuildEnemyBars(true);
        AssetDatabase.SaveAssets();
    }

    [MenuItem("ACDemo/UI/HUD/Verify Layout", priority = 110)]
    public static void VerifyLayoutMenu()
    {
        VerifyPlayerHudLayout();
    }

    [MenuItem("ACDemo/UI/HUD/Rebuild Everything", priority = 90)]
    public static void RebuildAll()
    {
        EnsurePlayerMana(PlayerPrefabPath);

        BuildPlayerHud(true);
        BuildEnemyBars(true);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        VerifyPlayerHudLayout();
        Debug.Log("[HUDPrefabBuilder] Rebuild Everything 完成");
    }

    // ==================== 玩家 HUD ====================

    /// <summary>force=false 时，已存在生成子树就跳过，保留手改内容</summary>
    private static void BuildPlayerHud(bool force)
    {
        if (!File.Exists(HudPrefabPath))
        {
            Debug.LogError($"[HUDPrefabBuilder] 找不到 {HudPrefabPath}");
            return;
        }

        GameObject root = PrefabUtility.LoadPrefabContents(HudPrefabPath);
        try
        {
            // 根节点那张全屏 39% 白 Image 会压暗画面并吃掉射线，HUD 不需要它
            Image dim = root.GetComponent<Image>();
            if (dim != null)
            {
                Object.DestroyImmediate(dim, true);
                Debug.Log("[HUDPrefabBuilder] 已移除 MainHUDForm 根节点的全屏 Image");
            }

            Transform oldStatus = root.transform.Find("PlayerStatusRoot");
            Transform oldSlots = root.transform.Find("QuickSlotRoot");

            if (!force && (oldStatus != null || oldSlots != null))
            {
                Debug.LogWarning("[HUDPrefabBuilder] PlayerStatusRoot / QuickSlotRoot 已存在，跳过创建。" +
                                 "要重建请用 ACDemo/UI/HUD/Rebuild Player HUD");
                return;
            }

            if (oldStatus != null) Object.DestroyImmediate(oldStatus.gameObject, true);
            if (oldSlots != null) Object.DestroyImmediate(oldSlots.gameObject, true);

            int uiLayer = LayerMask.NameToLayer("UI");

            RectTransform statusRoot = BuildStatusRoot(root.transform);
            RectTransform slotRoot = BuildQuickSlotRoot(root.transform);

            SetLayerRecursively(statusRoot.gameObject, uiLayer);
            SetLayerRecursively(slotRoot.gameObject, uiLayer);

            PrefabUtility.SaveAsPrefabAsset(root, HudPrefabPath);
            Debug.Log("[HUDPrefabBuilder] MainHUDForm 构建完成（PlayerStatusRoot + QuickSlotRoot）");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static RectTransform BuildStatusRoot(Transform hudRoot)
    {
        RectTransform root = NewRect("PlayerStatusRoot", hudRoot);
        root.anchorMin = new Vector2(0f, 1f);
        root.anchorMax = new Vector2(0f, 1f);
        root.pivot = new Vector2(0f, 1f);
        root.anchoredPosition = new Vector2(MarginX, -MarginTop);
        root.sizeDelta = new Vector2(HealthSize.x, 120f);

        float healthY = 0f;
        float manaY = -(HealthSize.y + BarGap);
        float staminaY = manaY - (ManaSize.y + BarGap);

        HUDBarView health = CreateBar(root, "HealthBar", healthY, HealthSize, HealthColor);
        HUDBarView mana = CreateBar(root, "ManaBar", manaY, ManaSize, ManaColor);
        HUDBarView stamina = CreateBar(root, "StaminaBar", staminaY, StaminaSize, StaminaColor);

        PlayerStatusPanel panel = root.gameObject.AddComponent<PlayerStatusPanel>();
        SetObject(panel, "healthBar", health);
        SetObject(panel, "manaBar", mana);
        SetObject(panel, "staminaBar", stamina);

        return root;
    }

    private static HUDBarView CreateBar(Transform parent, string name, float y, Vector2 size, Color fillColor)
    {
        RectTransform root = NewRect(name, parent);
        root.anchorMin = new Vector2(0f, 1f);
        root.anchorMax = new Vector2(0f, 1f);
        root.pivot = new Vector2(0f, 1f);
        root.anchoredPosition = new Vector2(0f, y);
        root.sizeDelta = size;

        Image frame = NewImage("Frame", root, LoadSprite("hud_bar_frame.png"), FrameColor, Image.Type.Sliced);
        Stretch(frame.rectTransform, 0f);

        Image background = NewImage("Background", root, LoadSprite("hud_bar_fill.png"),
            new Color(0.04f, 0.04f, 0.05f, 0.92f), Image.Type.Simple);
        Stretch(background.rectTransform, 1f);

        Image ghost = NewImage("Ghost", root, LoadSprite("hud_bar_fill.png"), GhostColor, Image.Type.Filled);
        ConfigureFilled(ghost);
        Stretch(ghost.rectTransform, 1f);

        Image fill = NewImage("Fill", root, LoadSprite("hud_bar_fill.png"), fillColor, Image.Type.Filled);
        ConfigureFilled(fill);
        Stretch(fill.rectTransform, 1f);

        HUDBarView view = root.gameObject.AddComponent<HUDBarView>();
        SetObject(view, "fillImage", fill);
        SetObject(view, "ghostImage", ghost);
        return view;
    }

    private static void ConfigureFilled(Image image)
    {
        image.type = Image.Type.Filled;
        image.fillMethod = Image.FillMethod.Horizontal;
        image.fillOrigin = (int)Image.OriginHorizontal.Left;
        image.fillAmount = 1f;
    }

    private static RectTransform BuildQuickSlotRoot(Transform hudRoot)
    {
        RectTransform root = NewRect("QuickSlotRoot", hudRoot);
        root.anchorMin = Vector2.zero;
        root.anchorMax = Vector2.zero;
        root.pivot = Vector2.zero;
        root.anchoredPosition = SlotRootPosition;

        // 3x3 网格：上 / 左 / 右 / 下 四个格子，间隔 8；空出四角与中心
        float half = SlotSize * 0.5f;
        float cell = SlotSize + SlotGap;
        root.sizeDelta = new Vector2(SlotSize * 3f + SlotGap * 2f, SlotSize * 3f + SlotGap * 2f);

        QuickSlotWidget right = CreateSlot(root, "Slot_RightHand", new Vector2(half + 2f * cell, half + cell));
        QuickSlotWidget left = CreateSlot(root, "Slot_LeftHand", new Vector2(half, half + cell));
        QuickSlotWidget spell = CreateSlot(root, "Slot_Spell", new Vector2(half + cell, half + 2f * cell));
        QuickSlotWidget item = CreateSlot(root, "Slot_Item", new Vector2(half + cell, half));

        PlayerQuickSlots slots = root.gameObject.AddComponent<PlayerQuickSlots>();
        SetObject(slots, "rightHandSlot", right);
        SetObject(slots, "leftHandSlot", left);
        SetObject(slots, "itemSlot", item);
        SetObject(slots, "spellSlot", spell);

        return root;
    }

    /// <summary>center 为槽位中心相对 QuickSlotRoot 左下角的位置</summary>
    private static QuickSlotWidget CreateSlot(Transform parent, string name, Vector2 center)
    {
        RectTransform root = NewRect(name, parent);
        root.anchorMin = Vector2.zero;
        root.anchorMax = Vector2.zero;
        root.pivot = new Vector2(0.5f, 0.5f);
        root.anchoredPosition = center;
        root.sizeDelta = new Vector2(SlotSize, SlotSize);

        Image background = NewImage("Background", root, LoadSprite("hud_bar_fill.png"),
            new Color(0.04f, 0.04f, 0.05f, 0.90f), Image.Type.Simple);
        Stretch(background.rectTransform, 0f);

        Image frame = NewImage("Frame", root, LoadSprite("hud_slot_frame.png"), FrameColor, Image.Type.Sliced);
        Stretch(frame.rectTransform, 0f);

        Image icon = NewImage("Icon", root, null, Color.white, Image.Type.Simple);
        Stretch(icon.rectTransform, 8f);
        icon.preserveAspect = true;
        icon.enabled = false;

        Image selected = NewImage("Selected", root, LoadSprite("hud_slot_selected.png"),
            SelectedSlotColor, Image.Type.Sliced);
        Stretch(selected.rectTransform, -2f);
        selected.gameObject.SetActive(false);

        Text amount = NewText("Amount", root, string.Empty, 16, TextAnchor.LowerRight);
        RectTransform amountRect = amount.rectTransform;
        amountRect.anchorMin = Vector2.zero;
        amountRect.anchorMax = Vector2.one;
        amountRect.offsetMin = new Vector2(4f, 2f);
        amountRect.offsetMax = new Vector2(-4f, -2f);
        amount.color = new Color(0.92f, 0.89f, 0.80f, 1f);

        QuickSlotWidget widget = root.gameObject.AddComponent<QuickSlotWidget>();
        SetObject(widget, "iconImage", icon);
        SetObject(widget, "frameImage", frame);
        SetObject(widget, "selectedRoot", selected.gameObject);
        SetObject(widget, "amountText", amount);
        return widget;
    }

    // ==================== 敌人血条预制体 ====================

    private static void BuildEnemyBars(bool force)
    {
        if (File.Exists(EnemyFormPrefabPath) && !force)
        {
            Debug.LogWarning("[HUDPrefabBuilder] EnemyHealthBarForm.prefab 已存在，跳过。" +
                             "要重建请用 ACDemo/UI/HUD/Rebuild Enemy Health Bars");
            return;
        }

        int uiLayer = LayerMask.NameToLayer("UI");

        GameObject root = new GameObject("EnemyHealthBarForm", typeof(RectTransform));
        RectTransform rootRect = root.GetComponent<RectTransform>();
        rootRect.anchorMin = Vector2.zero;
        rootRect.anchorMax = Vector2.one;
        rootRect.offsetMin = Vector2.zero;
        rootRect.offsetMax = Vector2.zero;
        rootRect.localScale = Vector3.one;
        root.layer = uiLayer;

        RectTransform layer = NewRect("BarLayer", root.transform);
        Stretch(layer, 0f);

        EnemyHealthBarWidget template = CreateEnemyBarWidget(layer, "WidgetTemplate");
        template.gameObject.SetActive(false);

        EnemyHealthBarsForm form = root.AddComponent<EnemyHealthBarsForm>();
        SetObject(form, "barLayer", layer);
        SetObject(form, "widgetTemplate", template);

        SetLayerRecursively(root, uiLayer);

        PrefabUtility.SaveAsPrefabAsset(root, EnemyFormPrefabPath);
        Object.DestroyImmediate(root);

        Debug.Log("[HUDPrefabBuilder] EnemyHealthBarForm.prefab 构建完成");
    }

    private static EnemyHealthBarWidget CreateEnemyBarWidget(Transform parent, string name)
    {
        RectTransform root = NewRect(name, parent);
        root.anchorMin = new Vector2(0.5f, 0.5f);
        root.anchorMax = new Vector2(0.5f, 0.5f);
        root.pivot = new Vector2(0.5f, 0.5f);
        root.anchoredPosition = Vector2.zero;
        root.sizeDelta = new Vector2(150f, 8f);

        CanvasGroup group = root.gameObject.AddComponent<CanvasGroup>();
        group.alpha = 0f;
        group.blocksRaycasts = false;
        group.interactable = false;

        Image frame = NewImage("Frame", root, LoadSprite("hud_bar_frame.png"),
            new Color(0.30f, 0.27f, 0.22f, 0.95f), Image.Type.Sliced);
        Stretch(frame.rectTransform, 0f);

        Image background = NewImage("Background", root, LoadSprite("hud_bar_fill.png"),
            new Color(0.03f, 0.03f, 0.03f, 0.85f), Image.Type.Simple);
        Stretch(background.rectTransform, 1f);

        Image ghost = NewImage("Ghost", root, LoadSprite("hud_bar_fill.png"), GhostColor, Image.Type.Filled);
        ConfigureFilled(ghost);
        Stretch(ghost.rectTransform, 1f);

        Image fill = NewImage("Fill", root, LoadSprite("hud_bar_fill.png"), HealthColor, Image.Type.Filled);
        ConfigureFilled(fill);
        Stretch(fill.rectTransform, 1f);

        EnemyHealthBarWidget widget = root.gameObject.AddComponent<EnemyHealthBarWidget>();
        SetObject(widget, "fillImage", fill);
        SetObject(widget, "ghostImage", ghost);
        SetObject(widget, "canvasGroup", group);
        return widget;
    }

    // ==================== 玩家组件 ====================

    /// <summary>给玩家预制体补 PlayerMana（已有则跳过，不产生无意义改动）</summary>
    private static void EnsurePlayerMana(string prefabPath)
    {
        if (!File.Exists(prefabPath)) return;

        GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
        bool changed = false;
        try
        {
            if (root.GetComponent<PlayerController>() != null && root.GetComponent<PlayerMana>() == null)
            {
                root.AddComponent<PlayerMana>();
                changed = true;
            }
        }
        finally
        {
            if (changed)
            {
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                Debug.Log($"[HUDPrefabBuilder] 已给 {prefabPath} 添加 PlayerMana");
            }

            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    // ==================== 布局校验 ====================

    /// <summary>
    /// 在 1920x1080 的临时 Canvas 里实例化 HUD，回读每个关键节点的世界矩形，
    /// 与设计值逐项比对。日志里出现 [HUD Verify] 的行就是证据。
    /// </summary>
    public static void VerifyPlayerHudLayout()
    {
        GameObject canvasGo = new GameObject("__HudVerifyCanvas", typeof(RectTransform), typeof(Canvas));
        RectTransform canvasRect = canvasGo.GetComponent<RectTransform>();
        Canvas canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvasRect.sizeDelta = new Vector2(1920f, 1080f);
        canvasRect.pivot = new Vector2(0.5f, 0.5f);
        canvasRect.position = Vector3.zero;

        GameObject hud = PrefabUtility.InstantiatePrefab(
            AssetDatabase.LoadAssetAtPath<GameObject>(HudPrefabPath), canvasGo.transform) as GameObject;

        if (hud == null)
        {
            Debug.LogError("[HUD Verify] 实例化 MainHUDForm 失败");
            Object.DestroyImmediate(canvasGo);
            return;
        }

        Transform status = hud.transform.Find("PlayerStatusRoot");
        Transform slots = hud.transform.Find("QuickSlotRoot");

        CheckRect("HealthBar", status != null ? status.Find("HealthBar") : null, 44f, 36f, 520f, 18f);
        CheckRect("ManaBar", status != null ? status.Find("ManaBar") : null, 44f, 62f, 360f, 12f);
        CheckRect("StaminaBar", status != null ? status.Find("StaminaBar") : null, 44f, 82f, 440f, 12f);

        CheckRect("Slot_LeftHand", slots != null ? slots.Find("Slot_LeftHand") : null, 56f, 888f, 64f, 64f);
        CheckRect("Slot_RightHand", slots != null ? slots.Find("Slot_RightHand") : null, 200f, 888f, 64f, 64f);
        CheckRect("Slot_Spell", slots != null ? slots.Find("Slot_Spell") : null, 128f, 816f, 64f, 64f);
        CheckRect("Slot_Item", slots != null ? slots.Find("Slot_Item") : null, 128f, 960f, 64f, 64f);

        Object.DestroyImmediate(canvasGo);
    }

    private static void CheckRect(string label, Transform target, float left, float top, float width, float height)
    {
        if (target == null)
        {
            Debug.LogError($"[HUD Verify] {label}: 节点不存在");
            return;
        }

        RectTransform rect = target as RectTransform;
        Vector3[] corners = new Vector3[4];
        rect.GetWorldCorners(corners);

        // 画布中心在原点：(-960,-540) ~ (960,540)
        float actualLeft = corners[0].x + 960f;
        float actualTop = 540f - corners[1].y;
        float actualWidth = corners[2].x - corners[0].x;
        float actualHeight = corners[1].y - corners[0].y;

        bool ok = Mathf.Abs(actualLeft - left) < 0.5f
                  && Mathf.Abs(actualTop - top) < 0.5f
                  && Mathf.Abs(actualWidth - width) < 0.5f
                  && Mathf.Abs(actualHeight - height) < 0.5f;

        string message = $"[HUD Verify] {label}: left={actualLeft:F1} top={actualTop:F1} " +
                         $"w={actualWidth:F1} h={actualHeight:F1}（期望 {left}/{top}/{width}/{height}）";

        if (ok) Debug.Log(message);
        else Debug.LogError(message + " ← 不一致");
    }

    // ==================== 通用工具 ====================

    private static Sprite LoadSprite(string fileName)
    {
        string path = SpriteDir + "/" + fileName;
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (sprite == null)
        {
            Debug.LogError($"[HUDPrefabBuilder] 找不到贴图：{path}（先导入 Assets/UI/Resources/Sprites/UI/HUD）");
        }
        return sprite;
    }

    private static RectTransform NewRect(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.localScale = Vector3.one;
        rect.localRotation = Quaternion.identity;
        return rect;
    }

    private static Image NewImage(string name, Transform parent, Sprite sprite, Color color, Image.Type type)
    {
        RectTransform rect = NewRect(name, parent);
        Image image = rect.gameObject.AddComponent<Image>();
        image.sprite = sprite;
        image.color = color;
        image.type = type;
        image.raycastTarget = false;
        return image;
    }

    private static Text NewText(string name, Transform parent, string content, int fontSize, TextAnchor anchor)
    {
        RectTransform rect = NewRect(name, parent);
        Text text = rect.gameObject.AddComponent<Text>();
        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font == null) font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        text.font = font;
        text.fontSize = fontSize;
        text.alignment = anchor;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.raycastTarget = false;
        text.text = content;
        return text;
    }

    private static void Stretch(RectTransform rect, float inset)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(inset, inset);
        rect.offsetMax = new Vector2(-inset, -inset);
    }

    private static void SetObject(Object target, string fieldName, Object value)
    {
        if (target == null) return;

        SerializedObject serializedObject = new SerializedObject(target);
        SerializedProperty property = serializedObject.FindProperty(fieldName);
        if (property == null)
        {
            Debug.LogError($"[HUDPrefabBuilder] 字段不存在：{target.GetType().Name}.{fieldName}");
            return;
        }

        property.objectReferenceValue = value;
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetLayerRecursively(GameObject go, int layer)
    {
        if (layer < 0) return;

        go.layer = layer;
        foreach (Transform child in go.transform)
        {
            SetLayerRecursively(child.gameObject, layer);
        }
    }
}

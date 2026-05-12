using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class PickupPopupData
{
    public int weaponId;
    public string weaponName;
    public string iconPath;
    public int starCount;
    public Action onEquip;
    public Action onAddToBag;
    public Action onClose;
}

public class PickupPopup : BasePanel
{
    [Header("UI °ó¶¨")]
    [SerializeField] private Image iconImage;
    [SerializeField] private Text titleText;
    [SerializeField] private Button equipBtn;
    [SerializeField] private Button bagBtn;
    [SerializeField] private Button closeBtn;
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private Transform starContainer;

    [Header("¶¯»­")]
    [SerializeField] private float fadeDuration = 0.2f;

    private List<Image> starImages = new List<Image>();
    private Action onEquipCallback;
    private Action onAddToBagCallback;
    private Action onCloseCallback;
    private bool isClosing = false;
    private bool isShowing = false;

    private static Dictionary<string, Sprite> spriteCache = new Dictionary<string, Sprite>();
    private static Dictionary<int, Sprite> weaponIconCache = new Dictionary<int, Sprite>();

    protected override void Awake()
    {
        base.Awake();
        CacheStarImages();
        BindButtons();
        PreloadWeaponIcons();
    }

    void CacheStarImages()
    {
        starImages.Clear();
        if (starContainer == null) return;
        for (int i = 0; i < starContainer.childCount; i++)
        {
            Image img = starContainer.GetChild(i).GetComponent<Image>();
            if (img != null) starImages.Add(img);
        }
    }

    void BindButtons()
    {
        if (equipBtn != null) equipBtn.onClick.AddListener(OnEquipClick);
        if (bagBtn != null) bagBtn.onClick.AddListener(OnBagClick);
        if (closeBtn != null) closeBtn.onClick.AddListener(OnCloseClick);
    }

    public static void PreloadWeaponIcons()
    {
        if (weaponIconCache.Count > 0) return;
        PackageTables table = GameManager.Instance.GetPackageTable();
        if (table == null || table.DataList == null) return;
        foreach (PackageTableItem item in table.DataList)
        {
            if (item.type != GameConst.PackageTypeWeapon) continue;
            if (string.IsNullOrEmpty(item.imagePath)) continue;
            Sprite sprite = LoadSpriteCached(item.imagePath);
            if (sprite != null)
                weaponIconCache[item.id] = sprite;
        }
    }

    public static void ClearCache()
    {
        spriteCache.Clear();
        weaponIconCache.Clear();
    }

    static Sprite LoadSpriteCached(string path)
    {
        if (string.IsNullOrEmpty(path)) return null;
        Sprite cached;
        if (spriteCache.TryGetValue(path, out cached))
            return cached;
        Texture2D tex = Resources.Load<Texture2D>(path);
        if (tex == null) return null;
        Sprite sprite = Sprite.Create(tex,
            new Rect(0, 0, tex.width, tex.height),
            new Vector2(0.5f, 0.5f));
        spriteCache[path] = sprite;
        return sprite;
    }

    public void ShowPopup(PickupPopupData data)
    {
        if (isClosing || isShowing) return;
        isShowing = true;

        onEquipCallback = data.onEquip;
        onAddToBagCallback = data.onAddToBag;
        onCloseCallback = data.onClose;

        titleText.text = data.weaponName;

        Sprite icon;
        if (weaponIconCache.TryGetValue(data.weaponId, out icon))
        {
            iconImage.sprite = icon;
            iconImage.gameObject.SetActive(true);
        }
        else
        {
            iconImage.gameObject.SetActive(false);
        }

        RefreshStars(data.starCount);
        gameObject.SetActive(true);
        StartCoroutine(FadeIn());
    }

    void RefreshStars(int count)
    {
        for (int i = 0; i < starImages.Count; i++)
            starImages[i].enabled = i < count;
    }

    void OnEquipClick()
    {
        if (isClosing) return;
        onEquipCallback?.Invoke();
        ClosePopup();
    }

    void OnBagClick()
    {
        if (isClosing) return;
        onAddToBagCallback?.Invoke();
        ClosePopup();
    }

    void OnCloseClick()
    {
        if (isClosing) return;
        onCloseCallback?.Invoke();
        ClosePopup();
    }

    void ClosePopup()
    {
        if (isClosing) return;
        isClosing = true;
        StartCoroutine(FadeOutAndDestroy());
    }

    IEnumerator FadeIn()
    {
        if (canvasGroup == null) { isShowing = false; yield break; }
        canvasGroup.alpha = 0f;
        float elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            canvasGroup.alpha = Mathf.Lerp(0f, 1f, elapsed / fadeDuration);
            yield return null;
        }
        canvasGroup.alpha = 1f;
        isShowing = false;
    }

    IEnumerator FadeOutAndDestroy()
    {
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 1f;
            float elapsed = 0f;
            while (elapsed < fadeDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                canvasGroup.alpha = Mathf.Lerp(1f, 0f, elapsed / fadeDuration);
                yield return null;
            }
            canvasGroup.alpha = 0f;
        }

        onEquipCallback = null;
        onAddToBagCallback = null;
        onCloseCallback = null;
        isClosing = false;

        UIManager.Instance.ClosePanel(UIconst.PickupPopup);
    }

    void OnDisable()
    {
        if (isClosing || isShowing)
        {
            onEquipCallback = null;
            onAddToBagCallback = null;
            onCloseCallback = null;
            isClosing = false;
            isShowing = false;
        }
    }
}
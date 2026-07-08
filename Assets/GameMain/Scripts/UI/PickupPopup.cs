using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class PickupPopupData
{
    public int weaponId;
    public string weaponName;
}

public class PickupPopup : BasePanel
{
    [Header("UI 绑定")]
    [SerializeField] private Image iconImage;
    [SerializeField] private Text titleText;
    [SerializeField] private CanvasGroup canvasGroup;

    [Header("动画")]
    [SerializeField] private float fadeDuration = 0.2f;

    private bool isClosing = false;
    private bool isShowing = false;
    private static Dictionary<int, Sprite> weaponIconCache = new Dictionary<int, Sprite>();

    protected override void Awake()
    {
        base.Awake();
        PreloadWeaponIcons();
    }

    public static void PreloadWeaponIcons()
    {
        if (weaponIconCache.Count > 0) return;
        foreach (var kv in DataRepository.ItemTable)
        {
            if (kv.Value.Type != GameConst.PackageTypeWeapon) continue;
            var icon = DataRepository.GetItemIcon(kv.Key);
            if (icon != null)
                weaponIconCache[kv.Key] = icon;
        }
    }

    public static void ClearCache()
    {
        weaponIconCache.Clear();
    }

    public void ShowPopup(PickupPopupData data)
    {
        if (isClosing || isShowing) return;
        isShowing = true;

        titleText.text = data.weaponName;

        if (weaponIconCache.TryGetValue(data.weaponId, out Sprite icon))
        {
            iconImage.sprite = icon;
            iconImage.gameObject.SetActive(true);
        }
        else
            iconImage.gameObject.SetActive(false);

        gameObject.SetActive(true);
        StartCoroutine(FadeIn());
    }


    public void ClosePopup()
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

        isClosing = false;
        UIManager.Instance.ClosePanel(UIconst.PickupPopup);
    }

    void OnDisable()
    {
        if (isClosing || isShowing)
        {
            isClosing = false;
            isShowing = false;
        }
    }
}

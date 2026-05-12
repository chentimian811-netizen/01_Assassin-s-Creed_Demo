using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class ToastMessage : MonoBehaviour
{
    [SerializeField] private Text messageText;
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private float displayDuration = 2f;
    [SerializeField] private float fadeDuration = 0.3f;

    static ToastMessage instance;

    public static void Show(string message)
    {
        if (instance == null)
        {
            GameObject prefab = Resources.Load<GameObject>("Prefabs/Panels/ToastMessage");
            if (prefab == null)
            {
                Debug.LogWarning($"[Toast] Prefab not found at Resources/Prefabs/Panels/ToastMessage. Message: {message}");
                return;
            }
            instance = Instantiate(prefab).GetComponent<ToastMessage>();
            if (instance == null)
            {
                Debug.LogWarning($"[Toast] ToastMessage component missing on prefab. Message: {message}");
                return;
            }
            DontDestroyOnLoad(instance.gameObject);
        }
        instance.ShowMessage(message);
    }

    void ShowMessage(string message)
    {
        StopAllCoroutines();
        messageText.text = message;
        gameObject.SetActive(true);
        StartCoroutine(ShowCoroutine());
    }

    IEnumerator ShowCoroutine()
    {
        yield return StartCoroutine(Fade(0f, 1f));
        yield return new WaitForSecondsRealtime(displayDuration);
        yield return StartCoroutine(Fade(1f, 0f));
        gameObject.SetActive(false);
    }

    IEnumerator Fade(float from, float to)
    {
        if (canvasGroup == null) yield break;
        float elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            canvasGroup.alpha = Mathf.Lerp(from, to, elapsed / fadeDuration);
            yield return null;
        }
        canvasGroup.alpha = to;
    }
}
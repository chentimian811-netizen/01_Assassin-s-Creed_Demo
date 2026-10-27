using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 左下角单个快捷槽（四槽之一）的视图。
/// 只负责显示：图标、数量、选中高亮；不关心这个槽代表手、道具还是法术。
/// </summary>
public class QuickSlotWidget : MonoBehaviour
{
    [Header("引用")]
    [Tooltip("物品/武器图标")]
    [SerializeField] private Image iconImage;

    [Tooltip("槽位外框，选中时改色")]
    [SerializeField] private Image frameImage;

    [Tooltip("选中高亮层（金色描边），默认隐藏")]
    [SerializeField] private GameObject selectedRoot;

    [Tooltip("数量文本：空槽或数量<=1 时隐藏")]
    [SerializeField] private Text amountText;

    [Header("颜色")]
    [SerializeField] private Color normalFrameColor = new Color(0.42f, 0.38f, 0.31f, 1f);
    [SerializeField] private Color selectedFrameColor = new Color(0.88f, 0.75f, 0.44f, 1f);

    /// <summary>放入一件物品；count<=1 时不显示数量</summary>
    public void SetItem(Sprite sprite, int count = 0)
    {
        if (iconImage != null)
        {
            iconImage.sprite = sprite;
            iconImage.enabled = sprite != null;
            iconImage.color = Color.white;
        }

        SetCount(count);
    }

    /// <summary>空槽：不显示图标与数量</summary>
    public void SetEmpty()
    {
        if (iconImage != null)
        {
            iconImage.sprite = null;
            iconImage.enabled = false;
        }

        SetCount(0);
    }

    /// <summary>选中态：高亮层 + 外框变金色</summary>
    public void SetSelected(bool selected)
    {
        if (selectedRoot != null && selectedRoot.activeSelf != selected)
        {
            selectedRoot.SetActive(selected);
        }

        if (frameImage != null)
        {
            frameImage.color = selected ? selectedFrameColor : normalFrameColor;
        }
    }

    private void SetCount(int count)
    {
        if (amountText == null) return;
        amountText.text = count > 1 ? count.ToString() : string.Empty;
    }
}

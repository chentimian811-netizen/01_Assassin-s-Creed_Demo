using System;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;
using static PackageLocalData;


public class LotteryCell : MonoBehaviour
{
    private Transform UIImage;

    private Transform UIStars;

    private Transform UINew;

    private PackageLocalItem packageLocalItem;

    private PackageTableItem packageTableItem;

    private LotteryPanel uiParent;

    private void Awake()
    {
        InitUI();
    }

    void InitUI()
    {
        UIImage = transform.Find("Center/Image");
        UIStars = transform.Find("Bottom/StartLevel");
        UINew = transform.Find("Top/New");

        UINew.gameObject.SetActive(false);
    }

    public void Refresh(PackageLocalItem pckageLocalItem, LotteryPanel uiParent)
    {
        //数据初始化
        this.packageLocalItem = pckageLocalItem;
        this.packageTableItem = GameManager.Instance.GetPackageItemById(this.packageLocalItem.id);
        this.uiParent = uiParent;

        //刷新UI信息
        RefreshImage();
        RefreshStars();

    }

    private void RefreshImage()
    {
        // 检查 packageTableItem 和 imagePath 是否有效
        if (this.packageTableItem == null)
        {
            Debug.LogError("packageTableItem 为空");
            return;
        }

        if (string.IsNullOrEmpty(this.packageTableItem.imagePath))
        {
            Debug.LogError("imagePath 为空，id: " + this.packageTableItem.id);
            return;
        }

        // 安全加载图片
        Texture2D t = (Texture2D)Resources.Load(this.packageTableItem.imagePath);
        if (t != null)
        {
            Sprite temp = Sprite.Create(t, new Rect(0, 0, t.width, t.height), new Vector2(0f, 0f));
            UIImage.GetComponent<Image>().sprite = temp;
        }
        else
        {
            Debug.LogError("加载图片失败: " + this.packageTableItem.imagePath);
        }
    }

    public void RefreshStars()
    {
        for (int i = 0;i < UIStars.childCount; i++)
        {
            Transform star = UIStars.GetChild(i);
            if(this.packageTableItem.star > i)
            {
                star.gameObject.SetActive(true);
            }
            else
            {
                star.gameObject.SetActive(false);
            }

        }
    }
}

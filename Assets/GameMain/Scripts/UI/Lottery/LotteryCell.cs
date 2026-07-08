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
        this.uiParent = uiParent;

        //刷新UI信息
        RefreshImage();
        RefreshStars();

    }

    private void RefreshImage()
    {
        DataRepository.ItemTable.TryGetValue(packageLocalItem.id, out var item);
        if (item == null)
        {
            Debug.LogError("找不到物品配置，id: " + packageLocalItem.id);
            return;
        }

        var icon = DataRepository.GetItemIcon(item.Id);
        if (icon != null)
            UIImage.GetComponent<Image>().sprite = icon;
        else
            Debug.LogError("加载图片失败，id: " + packageLocalItem.id);
    }

    public void RefreshStars()
    {
        DataRepository.ItemTable.TryGetValue(packageLocalItem.id, out var item);
        int star = item?.Star ?? 0;
        for (int i = 0; i < UIStars.childCount; i++)
            UIStars.GetChild(i).gameObject.SetActive(i < star);
    }
}

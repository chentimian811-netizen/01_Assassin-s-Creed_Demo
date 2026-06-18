using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

public class ShopCell : MonoBehaviour,IPointerClickHandler
{
    private Transform UIIcon;
    private Transform UIQuantity;
    private Transform UIName;
    private Transform UIPrice;
    private ShopItemDisplay displayData;
    private ShopPanel UIParent;


    private void Awake()
    {
        InitUIName();
    }

    private void InitUIName()
    {
        UIIcon = transform.Find("Object_Icon/Icon");           // 图标
        UIQuantity = transform.Find("Number");                 // 数量
        UIName = transform.Find("Obj_Name_price/Name");        // 名称
        UIPrice = transform.Find("Obj_Name_price/Price");      // 价格
    }

    public void Refresh(ShopItemDisplay data,ShopPanel parent)
    {
        this.displayData = data;
        this.UIParent = parent;

        PackageTableItem tableItem = GameManager.Instance.GetPackageItemById(data.itemData.itemID);

        RefreshIcon(tableItem);//刷新图标

        //刷新名称
        if(UIName != null)
        {
            UIName.GetComponent<TextMeshProUGUI>().text = 
                tableItem != null ? tableItem.name : "未知";
        }

        //刷新价格
        if(UIPrice != null)
        {
            UIPrice.GetComponent<TextMeshProUGUI>().text = data.finalPrice.ToString();
        }

        // 显示数量
        if(UIQuantity != null)
        {
            if(data.currentStock == -1)
            {
                UIQuantity.gameObject.SetActive(false);
            }
            else
            {
                UIQuantity.gameObject.SetActive(true);
                UIQuantity.GetComponent<TextMeshProUGUI>().text = "x" + data.currentStock;
            }
        }

    }

    //刷新图标 从Resource加载图片
    private void RefreshIcon(PackageTableItem tableItem)
    {
        if(tableItem == null || string.IsNullOrEmpty(tableItem.imagePath))return;
        Sprite icon = Resources.Load<Sprite>(tableItem.imagePath);
        if(icon != null && UIIcon != null)
        {
            UIIcon.GetComponent<Image>().sprite = icon;
        }
    }

    //接口实现 玩家点击此格子时触发
    public void OnPointerClick(PointerEventData eventData)
    {
        UIParent.OnCellClicked(this);
    }

    //设置选中/取消选中状态
    public void SetSelected(bool selected)
    {
        
    }

    //获取当前各自的展示数据
    public ShopItemDisplay GetDisplayData()
    {
        return displayData;
    }

}

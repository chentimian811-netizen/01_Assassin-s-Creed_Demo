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
    private Transform UISelect;

    private ShopItemDisplay displayData;
    private ShopPanel uiParent;


    private void Awake()
    {
        InitUIName();
    }

    private void InitUIName()
    {
        UIIcon = transform.Find("Top/Icon");
        UIQuantity = transform.Find("Quantity");
        UISelect = transform.Find("Select");

        UISelect.gameObject.SetActive(false);
    }

    public void Refresh(ShopItemDisplay data,ShopPanel parent)
    {
        this.displayData = data;
        this.uiParent = parent;

        PackageTableItem tableItem = GameManager.Instance.GetPackageItemById(data.itemData.itemID);

        RefreshIcon(tableItem);

        // 显示数量：库存模式显示stock，出售模式显示1
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

        //默认取消选中
        UISelect.gameObject.SetActive(false);
    }

    //刷新图标 从Resource加载图片
    private void RefreshIcon(PackageTableItem tableItem)
    {
        if(tableItem == null || string.IsNullOrEmpty(tableItem.imagePath))return;
        Sprite icon = Resources.Load<Sprite>(tableItem.imagePath);
        if(icon != null)
        {
            UIIcon.GetComponent<Image>().sprite = icon;
        }
    }

    //接口实现 玩家点击此格子时触发
    public void OnPointerClick(PointerEventData eventData)
    {
        uiParent.OnCellClicked(this);
    }

    //设置选中/取消选中状态
    public void SetSelected(bool selected)
    {
        UISelect.gameObject.SetActive(selected);
    }

    //获取当前各自的展示数据
    public ShopItemDisplay GetDisplayData()
    {
        return displayData;
    }

}

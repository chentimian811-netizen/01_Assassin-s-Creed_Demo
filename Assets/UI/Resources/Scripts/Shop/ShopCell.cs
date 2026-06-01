using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class ShopCell : MonoBehaviour,IPointerClickHandler
{
    private Transform UIIcon;
    private Transform UIName;
    private Transform UIDiscount;
    private Transform UIPrice;
    private Transform UIStock;
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
        UIName = transform.Find("Top/Name");
        UIDiscount = transform.Find("Top/Discount");
        UIPrice = transform.Find("Bottom/Price");
        UIStock = transform.Find("Bottom/Stock");
        UISelect = transform.Find("Select");

        //默认隐藏选中的高亮和折扣标签
        UISelect.gameObject.SetActive(false);
        UIDiscount.gameObject.SetActive(false);
    }

    public void Refresh(ShopItemDisplay data,ShopPanel parent)
    {
        this.displayData = data;
        this.uiParent = parent;

        PackageTableItem tableItem = GameManager.Instance.GetPackageItemById(data.itemData.itemID);

        RefreshIcon(tableItem);

        UIName.GetComponent<Text>().text = tableItem.name;

        UIPrice.GetComponent<Text>().text = "Money" + data.finalPrice;


        if(data.currentStock == -1)
        {
            UIStock.GetComponent<Text>().text = "库存:∞";
        }
        else
        {
            UIStock.GetComponent<Text>().text = "库存" + data.currentStock;
        }

        if(data.itemData.discount < 1f)
        {
            UIDiscount.gameObject.SetActive(true);
            //折扣转为“几折”：0.8 ——> 8折
            int discountDisplay = Mathf.RoundToInt(data.itemData.discount * 10);
            UIDiscount.GetComponent<Text>().text = discountDisplay + "折";
        }
        else
        {
            UIDiscount.gameObject.SetActive(false);
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

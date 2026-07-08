using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using static PackageLocalData;

public class PackageCell : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
{
    private Transform UIIcon;
    private Transform UIHead;
    private Transform UINew;
    private Transform UISelect;
    private Transform UILevel;
    private Transform UIStars;
    private Transform UIDeleteSelect;
    private Transform UISelectAni;
    private Transform UIMouseOverAni;
    private Text UINumText;

    private PackageLocalItem packageLocalData;
    private PackagePanel uiParent;

    private void Awake()
    {
        InitUIName();
    }

    private void InitUIName()
    {
        UIIcon = transform.Find("Top/Icon");
        UIHead = transform.Find("Top/Head");
        UINew = transform.Find("Top/New");
        UILevel = transform.Find("Button/LevelText");
        UIStars = transform.Find("Button/StartLevel");
        UISelect = transform.Find("Select");
        UIDeleteSelect = transform.Find("DeleteSelect");

        UIMouseOverAni = transform.Find("MouseOverAni");
        UISelectAni = transform.Find("SelectAni");

        UINumText = transform.Find("Button/Count")?.GetComponent<Text>();
        UIDeleteSelect.gameObject.SetActive(false);
        UIMouseOverAni.gameObject.SetActive(false);
        UISelectAni.gameObject.SetActive(false);
    }

    public void Refresh(PackageLocalItem packageLocalData, PackagePanel uiParent)
    {
        this.packageLocalData = packageLocalData;
        this.uiParent = uiParent;

        DataRepository.ItemTable.TryGetValue(packageLocalData.id, out var item);
        // 检查 packageTablesItem 是否为 null
        if (item == null)
        {
            Debug.LogError("找不到物品配置，id: " + packageLocalData.id);
            return;
        }

        UILevel.GetComponent<Text>().text = "Lv." + this.packageLocalData.level.ToString();
        UINew.gameObject.SetActive(this.packageLocalData.isNew);

        var icon = DataRepository.GetItemIcon(item.Id);
        if (icon != null)
            UIIcon.GetComponent<Image>().sprite = icon;

        if (UINumText != null)
            UINumText.text = "x" + this.packageLocalData.num.ToString();

        RefreshStars(item.Star);
    }


    public void RefreshStars(int star)
    {
        for (int i = 0; i < UIStars.childCount; i++)
        {
           UIStars.GetChild(i).gameObject.SetActive(i < star);
        }
    }

    public void RefreshDeleteState()
    {
        if (this.uiParent.deleteChooseUid.Contains(this.packageLocalData.uid))
        {
            this.UIDeleteSelect.gameObject.SetActive(true);
        }
        else
        {
            this.UIDeleteSelect.gameObject.SetActive(false);
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        Debug.Log("OnPointerClick: " + eventData.ToString());
        if (this.uiParent.curMode == PackageMode.delete)
        {
            this.uiParent.AddChooseDeleteUid(this.packageLocalData.uid);
        }
        if (this.uiParent.ChooseUid == this.packageLocalData.uid)
            return;
        //根据点击设置最新的UID —> 进而刷新详情面板
        this.uiParent.ChooseUid = this.packageLocalData.uid;

        UISelectAni.gameObject.SetActive(true);
        UISelectAni.GetComponent<Animator>().SetTrigger("In");
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        Debug.Log("OnPointerEnter: " + eventData.ToString());
        UIMouseOverAni.gameObject.SetActive(true);
        UIMouseOverAni.GetComponent<Animator>().SetTrigger("In");
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        Debug.Log("OnPointerExit: " + eventData.ToString());
        UIMouseOverAni.GetComponent<Animator>().SetTrigger("Out");
    }

    public string GetUid()
    {
        return packageLocalData.uid;
    }

}
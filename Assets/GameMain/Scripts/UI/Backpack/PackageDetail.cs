using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using static PackageLocalData;

public class PackageDetail : MonoBehaviour
{
    private Transform UIStars;
    private Transform UIDescription;
    private Transform UIIcon;
    private Transform UITitle;
    private Transform UILeveText;
    private Transform UISkillDescription;
    private PackageLocalItem packageLocalData;
    private void Awake()
    {
        InitUIName();
    }
    private void InitUIName()
    {
        UIStars = transform.Find("Center/StartLevel");
        UIDescription = transform.Find("Center/Description");
        UIIcon = transform.Find("Center/Icon");
        UITitle = transform.Find("Top/Bg/Title");
        UILeveText = transform.Find("Button/LevelPanel/LevelText");
        UISkillDescription = transform.Find("Button/SkillDescription");
    }

    public void Refresh(PackageLocalItem packageLocalData, PackagePanel uiParent)
    {
        this.packageLocalData = packageLocalData;

        DataRepository.ItemTable.TryGetValue(packageLocalData.id, out var item);
        if(item == null)
        {
            Debug.LogError("找不到物品配置，id: " + packageLocalData.id);
            return;
        }
        
        if (UILeveText != null)
            UILeveText.GetComponent<Text>().text
                = string.Format("Lv.{0}/40", this.packageLocalData.level.ToString());

        if (UIDescription != null)
            UIDescription.GetComponent<Text>().text = item.Description;

        if (UISkillDescription != null)
            UISkillDescription.GetComponent<Text>().text = item.SkillDescription;

        if (UITitle != null)
            UITitle.GetComponent<Text>().text = item.Name;

        var icon = DataRepository.GetItemIcon(item.Id);
        if (icon != null && UIIcon != null)
            UIIcon.GetComponent<Image>().sprite = icon;

        RefreshStars(item.Star);
    
    }
    public void RefreshStars(int star)
    {
        for (int i = 0; i < UIStars.childCount; i++)
            UIStars.GetChild(i).gameObject.SetActive(i < star);
    }
}

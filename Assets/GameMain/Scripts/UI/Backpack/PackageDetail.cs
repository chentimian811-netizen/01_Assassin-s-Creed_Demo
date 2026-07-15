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

    private Transform UIWeaponType;
    private Transform UIBaseDamageTitle;
    private Transform UIBaseDamageText;
    private Transform UICritDamageTitle;
    private Transform UICritRateText;
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
        UILeveText = transform.Find("Button/LevelText");
        UISkillDescription = transform.Find("Button/SkillDescription");

        UIWeaponType = transform.Find("Center/WeaponType");
        UIBaseDamageTitle = transform.Find("Center/BaseDamageTitle");
        UIBaseDamageText = transform.Find("Center/BaseDamageText");
        UICritDamageTitle = transform.Find("Center/CritDamageTitle");
        UICritRateText = transform.Find("Center/CritRateText");
    }

    public void Refresh(PackageLocalItem packageLocalData, PackagePanel uiParent)
    {
        this.packageLocalData = packageLocalData;

        DataRepository.ItemTable.TryGetValue(packageLocalData.id, out var item);
        if(item == null)
        {
            Debug.LogError("找不到物品配置,id: " + packageLocalData.id);
            return;
        }
        
        if (UILeveText != null)
            UILeveText.GetComponent<Text>().text
                = $"Lv.{packageLocalData.level}/{WeaponUpgradeSystem.GetMaxLevel()}";

        if (UIDescription != null)
            UIDescription.GetComponent<Text>().text = item.Description;

        if (UISkillDescription != null)
            UISkillDescription.GetComponent<Text>().text = item.SkillDescription;

        if (UITitle != null)
            UITitle.GetComponent<Text>().text = item.Name;

        var icon = DataRepository.GetItemIcon(item.Id);
        
        if (icon != null && UIIcon != null)
            UIIcon.GetComponent<Image>().sprite = icon;

        if(UIWeaponType != null)
            UIWeaponType.GetComponent<Text>().text = item.WeaponType.ToString();
        
        if (UIBaseDamageText != null)
            UIBaseDamageText.GetComponent<Text>().text
            = WeaponUpgradeSystem.CalculateDamage(item.BaseDamage, packageLocalData.level).ToString();

        if (UICritRateText != null)
            UICritRateText.GetComponent<Text>().text = $"{WeaponUpgradeSystem.CalculateCritRate(item.CritRate, packageLocalData.level) * 100:F0}%";

        RefreshStars(item.Star);
    
    }
    public void RefreshStars(int star)
    {
        for (int i = 0; i < UIStars.childCount; i++)
            UIStars.GetChild(i).gameObject.SetActive(i < star);
    }
}

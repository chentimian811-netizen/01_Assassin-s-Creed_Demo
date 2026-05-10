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

    private PackageTableItem packageTablesItem;

    private PackagePanel uiParent;


    private void Awake()
    {
        InitUIName();
        Test();
    }

    private void Test()
    {
        Refresh(GameManager.Instance.GetPackageLocalData()[0], null);
    }

    private void InitUIName()
    {
        UIStars = transform.Find("Center/StartLevel");
        UIDescription = transform.Find("Center/Description");
        UIIcon = transform.Find("Center/Icon");
        UITitle = transform.Find("Top/Bg/Title");
        UILeveText = transform.Find("Button/LevelPanel/LevelText");
        UISkillDescription = transform.Find("Button/Description");
    }

    public void Refresh(PackageLocalItem packageLocalData,PackagePanel uiParent)
    {
        //初始化：动态数据 静态数据 父物品逻辑
        this.packageLocalData = packageLocalData;
        this.packageTablesItem = GameManager.Instance.GetPackageItemById(packageLocalData.id);
        this.uiParent = uiParent;

        //等级
        UILeveText.GetComponent<Text>().text = string.Format("Lv.{0}/40", this.packageLocalData.level.ToString());
        //简短描述
        UIDescription.GetComponent<Text>().text = this.packageTablesItem.description;
        //详细描述
        UISkillDescription.GetComponent<Text>().text = this.packageTablesItem.skillDescription;
        //物品名称
        UITitle.GetComponent<Text>().name = this.packageTablesItem.name;

        //图片加载  
        Texture2D t = (Texture2D)Resources.Load(this.packageTablesItem.imagePath);
        Sprite temp  = Sprite.Create(t,new Rect(0, 0, t.width, t.height), new Vector2(0, 0));
        UIIcon.GetComponent<Image>().sprite = temp;

        //星级处理
        RefreshStars();

    }

    public void RefreshStars()
    {
        for(int i = 0;i < UIStars.childCount; i++)
        {
            Transform star = UIStars.GetChild(i);
            if (this.packageTablesItem.star > i)
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

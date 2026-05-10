using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using static PackageLocalData;

public class LotteryPanel : BasePanel
{
    private Transform UIClose;

    private Transform UICenter;

    private Transform UILottery10;

    private Transform UILottery1;

    private GameObject LotteryCellPrefab;


    protected override void Awake()
    {
        base.Awake();
        InitUI();
        InitPrefab();
    }

    private void InitUI()
    {
        UIClose = transform.Find("TopRight/Close");
        UICenter = transform.Find("Center");
        UILottery10 = transform.Find("Bottom/Lottery10");
        UILottery1 = transform.Find("Bottom/Lottery1");

        UILottery10.GetComponent<Button>().onClick.AddListener(OnLottery10Btn);
        UILottery1.GetComponent<Button>().onClick.AddListener(OnLottery1Btn);

        UIClose.GetComponent<Button>().onClick.AddListener(OnClose); 
    }
    
    private void InitPrefab()
    {
        LotteryCellPrefab = Resources.Load("Prefabs/Panels/Lottery/LotteryItem") as GameObject;
    }


    private void OnLottery10Btn()
    {
        Debug.Log("抽卡10次");
        List<PackageLocalItem> packageLocalItems = GameManager.Instance.GetLotteryRandom10();
        for (int i = 0;i < UICenter.childCount;i++)
        {
           Destroy(UICenter.GetChild(i).gameObject);
        }

        foreach(PackageLocalItem item in packageLocalItems)
        {
            Transform LotteryCellTran = Instantiate(LotteryCellPrefab.transform, UICenter) as Transform;
            //对卡片做信息展示刷新
            LottertCell lottertCell = LotteryCellTran.GetComponent<LottertCell>();
            lottertCell.Refresh(item, this);
        }

    }

    private void OnLottery1Btn()
    {
        Debug.Log("抽卡1次");
        for (int i = 0;i < UICenter.childCount;i++)
        {
            Destroy(UICenter.GetChild(i).gameObject);
        }
        //抽卡获得一张新的物品
        PackageLocalItem item = GameManager.Instance.GetLotteryRandom1();

        Transform LotteryCellTran = Instantiate(LotteryCellPrefab.transform, UICenter) as Transform;

        //对卡片做信息展示刷新
        LottertCell lottertCell = LotteryCellTran.GetComponent<LottertCell>();
        lottertCell.Refresh(item, this);
    }

    private void OnClose()
    {
        Debug.Log("关闭抽卡界面");
        ClosePanel();
        UIManager.Instance.OpenPanel(UIconst.MainPanel);

    }
}

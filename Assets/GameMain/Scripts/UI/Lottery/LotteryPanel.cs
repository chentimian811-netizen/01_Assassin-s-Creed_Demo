using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using static PackageLocalData;

public class LotteryPanel : BasePanel
{   
    [SerializeField] int singlePullCost = 200;
    [SerializeField] int multiPullCost = 180;

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
        if(!GameManager.Instance.TryGetLotteryRandom10(multiPullCost,out var items))
        {
            ToastMessage.Show("金币不足！");
            return;
        }
        for(int i = 0; i < UICenter.childCount; i++)
        {
            Destroy(UICenter.GetChild(i).gameObject);
        }
        foreach(PackageLocalItem item in items)
        {
            Transform cellTran = Instantiate(LotteryCellPrefab.transform, UICenter) as Transform;
            cellTran.GetComponent<LotteryCell>().Refresh(item, this);
        }

    }

    private void OnLottery1Btn()
    {
        Debug.Log("抽卡1次");
        if(!GameManager.Instance.TryGetLotteryRandom1(singlePullCost,out var item))
        {
            ToastMessage.Show("金币不足！");
            return;
        }
        for(int i = 0; i < UICenter.childCount; i++)
        {
            Destroy(UICenter.GetChild(i).gameObject);
        }
        Transform cellTran = Instantiate(LotteryCellPrefab.transform, UICenter) as Transform;
        cellTran.GetComponent<LotteryCell>().Refresh(item, this);
        
    }

    private void OnClose()
    {
        Debug.Log("关闭抽卡界面");
        UIManager.Instance.ClosePanel(UIconst.LotteryPanel);

        var mainPanel = UIManager.Instance.GetPanel(UIconst.MainPanel);
        if(mainPanel != null)
        {
            mainPanel.gameObject.SetActive(true);
        }
        
    }
}

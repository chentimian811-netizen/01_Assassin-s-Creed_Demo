using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class HUDBtn : MonoBehaviour
{
    [SerializeField] private Button packageBtn;
    [SerializeField] private Button lotteryBtn;

    private void Awake()
    {
        if(packageBtn != null)
        {
            packageBtn.onClick.AddListener(OnOpenPackage);
        }
        if(lotteryBtn != null)
        {
            lotteryBtn.onClick.AddListener(OnOpenLottery);
        }
        
    }

    private void OnDestroy()
    {
        if(packageBtn != null)
        {
            packageBtn.onClick.RemoveListener(OnOpenPackage);
        }
        if(lotteryBtn != null)
        {
            lotteryBtn.onClick.RemoveListener(OnOpenLottery);
        }
    }

    private void OnOpenPackage()
    {
        UIManager.Instance.OpenPanel(UIconst.PackagePanel);
    }
    private void OnOpenLottery()
    {
        UIManager.Instance.OpenPanel(UIconst.LotteryPanel);
    }
}

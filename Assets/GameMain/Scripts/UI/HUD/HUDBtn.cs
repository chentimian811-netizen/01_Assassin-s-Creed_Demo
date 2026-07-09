using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class HUDBtn : MonoBehaviour
{
    [SerializeField] private Button packageBtn;
    [SerializeField] private Button lotteryBtn;

    [SerializeField] private GameObject[] hudRoots;

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

    private void Update()
    {
        bool anyPanelOpen = UIManager.Instance .panelDict.Count > 0;
        bool shouldShow = !anyPanelOpen;
        if(packageBtn !=null) packageBtn.gameObject .SetActive(shouldShow);
        if(lotteryBtn != null) lotteryBtn.gameObject.SetActive(shouldShow);

        foreach(var root in hudRoots)
        {
            if(root != null) root.SetActive(!anyPanelOpen);
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

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
        // 背包已迁移到 GF UIForm：走 UIRouter（本组件将在 GF-1.6 删除）
        UIRouter.Open(UIPaths.PackageForm, "Popup");
    }
    private void OnOpenLottery()
    {
        // 抽卡已迁移到 GF UIForm：走 UIRouter（本组件将在 GF-1.6 删除）
        UIRouter.Open(UIPaths.LotteryForm, "Popup");
    }
}

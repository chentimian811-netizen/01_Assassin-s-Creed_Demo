using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public class MainPanel : BasePanel
{
    private Transform UILottery;

    private Transform UIPacakge;

    private Transform UIQuitBtn;


    protected override void Awake()
    {
        base.Awake();
        InitUI();
    }

    private void InitUI()
    {
        UILottery = transform.Find("Top/LotteryBtn");
        UIPacakge = transform.Find("TopRight/PackageBtn");
        UIQuitBtn = transform.Find("BottomLeft/QuitBtn");

        UILottery.GetComponent<Button>().onClick.AddListener(OnBtnLottery);
        UIPacakge.GetComponent<Button>().onClick.AddListener(OnBtnPackage);
        UIQuitBtn.GetComponent<Button>().onClick.AddListener(OnQuitGame);
    }


    private void OnBtnPackage()
    {
        Debug.Log("打开背包界面");
        UIManager.Instance.OpenPanel(UIconst.PackagePanel);
        ClosePanel();
    }

    private void OnBtnLottery()
    {
        Debug.Log("打开抽卡界面");
        UIManager.Instance.OpenPanel(UIconst.LotteryPanel);
        ClosePanel();
    }

    private void OnQuitGame()
    {
        Debug.Log("退出游戏");
        EditorApplication.isPlaying = false;
        Application.Quit();
    }
}

using System;
using System.Collections;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
#endif
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

    private void OnEnable()
    {
        // 面板打开时播放背景音乐
        if (AudioManager.Instance != null)
        {
            Debug.Log("[MainPanel] 找到 AudioManager，准备播放 BGM");
            AudioManager.Instance.PlayMusic();
        }
        else
        {
            Debug.LogWarning("[MainPanel] AudioManager.Instance 为 null！请检查场景中是否有 AudioManager 对象");
        }
    }

    private void InitUI()
    {
        // 查找按钮
        UILottery = transform.Find("Top/LotteryBtn");
        UIPacakge = transform.Find("TopRight/PackageBtn");
        UIQuitBtn = transform.Find("BottomLeft/QuitBtn");

        // 调试：检查按钮是否找到
        if (UILottery == null) Debug.LogError("找不到 LotteryBtn");
        if (UIPacakge == null) Debug.LogError("找不到 PackageBtn");
        if (UIQuitBtn == null) Debug.LogError("找不到 QuitBtn");

        // 绑定点击事件
        if (UILottery != null)
        {
            Button lotteryBtn = UILottery.GetComponent<Button>();
            if (lotteryBtn != null)
            {
                lotteryBtn.onClick.AddListener(OnBtnLottery);
                Debug.Log("LotteryBtn 事件绑定成功");
            }
            else
            {
                Debug.LogError("LotteryBtn 没有 Button 组件");
            }
        }

        if (UIPacakge != null)
        {
            Button packageBtn = UIPacakge.GetComponent<Button>();
            if (packageBtn != null)
            {
                packageBtn.onClick.AddListener(OnBtnPackage);
                Debug.Log("PackageBtn 事件绑定成功");
            }
            else
            {
                Debug.LogError("PackageBtn 没有 Button 组件");
            }
        }

        if (UIQuitBtn != null)
        {
            Button quitBtn = UIQuitBtn.GetComponent<Button>();
            if (quitBtn != null)
            {
                quitBtn.onClick.AddListener(OnQuitGame);
                Debug.Log("QuitBtn 事件绑定成功");
            }
            else
            {
                Debug.LogError("QuitBtn 没有 Button 组件");
            }
        }
    }

    private void OnBtnPackage()
    {
        Debug.Log("点击了背包按钮");
        UIManager.Instance.OpenPanel(UIconst.PackagePanel);
        ClosePanel();
    }

    private void OnBtnLottery()
    {
        Debug.Log("点击了抽卡按钮");
        UIManager.Instance.OpenPanel(UIconst.LotteryPanel);
        ClosePanel();
    }

    private void OnQuitGame()
    {
        Debug.Log("退出游戏");
        #if UNITY_EDITOR
        EditorApplication.isPlaying = false;
        #endif
        Application.Quit();
    }
}
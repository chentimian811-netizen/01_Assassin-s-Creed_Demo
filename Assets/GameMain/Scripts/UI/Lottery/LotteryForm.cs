//------------------------------------------------------------
// ACDemo — 抽卡界面（GF UIFormLogic 版）
// 放置路径: Assets/GameMain/Scripts/UI/Lottery/LotteryForm.cs
// 对应 Prefab: Assets/UI/Prefabs/Lottery/LotteryForm.prefab
// 分组: Popup（模态：锁光标 + 暂停，统一由 GameUIForm 处理）
// 来源: 由旧 LotteryPanel(BasePanel + UIManager) 迁移。抽卡业务（GameManager.TryGetLotteryRandom1/10）
//       与预制体结构未改动，只替换「生命周期 + 关闭方式」：
//         旧实例每次新建 → GF 复用实例，抽卡结果要在 OnOpenForm 里清掉
//         旧 UIManager.ClosePanel → CloseSelf()
//------------------------------------------------------------

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using static PackageLocalData;

public class LotteryForm : GameUIForm
{
    [SerializeField] int singlePullCost = 200;
    [SerializeField] int multiPullCost = 180;

    private Transform UIClose;

    private Transform UICenter;

    private Transform UILottery10;

    private Transform UILottery1;

    private GameObject LotteryCellPrefab;

    // Awake 在实例化时跑一次：找子节点 + 绑按钮（GF 复用时不会再调用）
    private void Awake()
    {
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

    /// <summary>
    /// 每次打开都跑。旧实现每次都是新实例，抽卡结果区域天然是空的；
    /// GF 复用实例，不主动清理的话上次抽到的卡还会留在屏幕上。
    /// </summary>
    protected override void OnOpenForm(object userData)
    {
        ClearResultCells();
    }

    /// <summary>
    /// 每次关闭都跑：清掉抽卡结果，避免下次打开先闪一下旧内容。
    /// </summary>
    protected override void OnCloseForm()
    {
        ClearResultCells();
    }

    /// <summary>
    /// 清空 Center 下的抽卡结果。用 Destroy 而不是 DestroyImmediate：
    /// 延迟到帧末销毁，不会在同一帧里破坏 Center 的子物体遍历。
    /// </summary>
    private void ClearResultCells()
    {
        if (UICenter == null) return;

        for (int i = UICenter.childCount - 1; i >= 0; i--)
        {
            Destroy(UICenter.GetChild(i).gameObject);
        }
    }

    private void OnLottery10Btn()
    {
        Debug.Log("抽卡10次");
        if (!GameManager.Instance.TryGetLotteryRandom10(multiPullCost, out var items))
        {
            ToastMessage.Show("金币不足！");
            return;
        }

        ClearResultCells();
        foreach (PackageLocalItem item in items)
        {
            Transform cellTran = Instantiate(LotteryCellPrefab.transform, UICenter) as Transform;
            cellTran.GetComponent<LotteryCell>().Refresh(item, this);
        }
    }

    private void OnLottery1Btn()
    {
        Debug.Log("抽卡1次");
        if (!GameManager.Instance.TryGetLotteryRandom1(singlePullCost, out var item))
        {
            ToastMessage.Show("金币不足！");
            return;
        }

        ClearResultCells();
        Transform cellTran = Instantiate(LotteryCellPrefab.transform, UICenter) as Transform;
        cellTran.GetComponent<LotteryCell>().Refresh(item, this);
    }

    private void OnClose()
    {
        // 旧实现是 UIManager.ClosePanel（内部 Destroy(gameObject)），GF 池化后必须走路由
        CloseSelf();
    }
}

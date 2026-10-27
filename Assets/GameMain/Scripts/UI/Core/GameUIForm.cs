using System;
using UnityEngine;
using UnityGameFramework.Runtime;

/// <summary>
/// 模态界面基类：在 GF <see cref="UIFormLogic"/> 之上补两件事 ——
///   1. 打开时加光标锁、必要时暂停游戏；关闭时按引用计数反向释放；
///   2. 把「只跑一次」和「每次打开都要跑」的初始化拆成两个钩子，适配 GF 的界面池化。
///
/// 【为什么一定要拆生命周期】
///   旧面板把刷新逻辑写在 Start() 里：GF 会复用界面实例，Start 只在实例第一次激活时
///   跑一次，第二次打开界面就停在旧数据上（还残留上次的选中项、删除模式、页签）。
///   所以约定：
///     - <see cref="OnInit"/> / OnInitForm：只跑一次，用来 transform.Find、绑按钮监听；
///     - <see cref="OnOpen"/> / OnOpenForm：每次打开都跑，用来刷新数据、重置临时状态、订阅事件；
///     - <see cref="OnClose"/> / OnCloseForm：每次关闭都跑，用来退订、停协程。
///
/// 【异常与锁的配对】
///   子类的 OnOpenForm 抛异常时，GF 只会打一条错误日志然后继续；如果那时已经加了锁，
///   界面却没能正常显示，锁就永久泄漏。所以这里对子类回调包 try/catch，并且用
///   m_LocksAcquired 记录是否真的持锁，保证 OnClose 一定能把锁/暂停释放干净。
/// </summary>
public abstract class GameUIForm : UIFormLogic
{
    /// <summary>是否持锁。防止 OnOpen/OnClose 被异常路径打乱后重复加锁或重复释放</summary>
    private bool m_LocksAcquired;

    /// <summary>
    /// 是否模态：true 时打开会暂停游戏（走 HitStopManager，绝不直接写 Time.timeScale）。
    /// 需要「只锁光标但不暂停」的界面把它覆写成 false。
    /// </summary>
    protected virtual bool IsModal => true;

    /// <summary>
    /// 光标锁 / 暂停计数的 key。默认用类型名，保证同类界面共用一把锁；
    /// 需要区分同一类界面的多个实例时再覆写。
    /// </summary>
    protected virtual string CursorKey => GetType().Name;

    /// <summary>
    /// 打开期间缓存的 formId。关闭时要靠它去清路由记录 ——
    /// 进入 OnClose 之后 GF 可能已经把 UIForm.SerialId 复位成 0。
    /// </summary>
    private int m_FormId;

    /// <summary>当前 formId；界面未打开时为 0。关闭自己时用这个</summary>
    protected int FormId => m_FormId > 0 ? m_FormId : (UIForm != null ? UIForm.SerialId : 0);


    /// <summary>
    /// 是否把根节点强制铺满父 Canvas。
    ///
    /// 【为什么需要】GF 的 DefaultUIFormHelper 用 SetParent 把实例挂到界面组 Canvas 下，
    /// 而实例在「无父级」状态下 rect 是 0×0，实测根节点会停在 0×0，
    /// 于是所有子节点都被算到画布外（MainHUDForm 踩过同一个坑，见其 ForceFillParentCanvas）。
    /// 全屏模态面板一律铺满；将来做「固定尺寸小窗口」的界面时把它覆写成 false。
    /// </summary>
    protected virtual bool FillParentCanvas => true;

    protected sealed override void OnInit(object userData)
    {
        base.OnInit(userData);

        try
        {
            OnInitForm(userData);
        }
        catch (Exception exception)
        {
            Debug.LogError($"[{GetType().Name}] OnInitForm 抛出异常：{exception}");
        }
    }

    protected sealed override void OnOpen(object userData)
    {
        base.OnOpen(userData);

        // 记到路由里。GF 的 OpenUIForm 是异步的（返回到调用方时实例还没创建完），
        // 所以这里拿到的才是真实 formId / 真实资源路径 / 真实分组；
        // 也让绕过 UIRouter 直接 OpenUIForm 的界面同样能被 CloseSelf 关掉。
        m_FormId = UIForm != null ? UIForm.SerialId : 0;
        UIRouter.EnsureRegistered(
            m_FormId,
            UIForm != null ? UIForm.UIFormAssetName : null,
            UIForm != null && UIForm.UIGroup != null ? UIForm.UIGroup.Name : null);

        AcquireLocks();

        // 先摆正根节点再刷数据：子节点布局依赖父级 rect 已经是屏幕尺寸
        if (FillParentCanvas) StretchToParentCanvas();

        try
        {
            OnOpenForm(userData);
        }
        catch (Exception exception)
        {
            // 界面数据没刷出来，但至少不能把光标/暂停锁死
            Debug.LogError($"[{GetType().Name}] OnOpenForm 抛出异常：{exception}");
        }
    }

    protected sealed override void OnClose(bool isShutdown, object userData)
    {
        // 先清路由记录：OnCloseForm 里若有「是否还开着」的判断，此时应已得到 false
        UIRouter.Unregister(m_FormId);
        m_FormId = 0;

        try
        {
            OnCloseForm();
        }
        catch (Exception exception)
        {
            Debug.LogError($"[{GetType().Name}] OnCloseForm 抛出异常：{exception}");
        }

        ReleaseLocks();

        base.OnClose(isShutdown, userData);
    }

    /// <summary>
    /// 只跑一次的初始化：transform.Find 子节点、绑按钮监听、建缓存。
    /// 池化复用后不会再调用。
    /// </summary>
    protected virtual void OnInitForm(object userData) { }

    /// <summary>
    /// 每次打开都跑：刷新数据、重置临时状态、订阅事件。不要在这里 transform.Find。
    /// </summary>
    protected abstract void OnOpenForm(object userData);

    /// <summary>
    /// 每次关闭都跑：退订事件、StopAllCoroutines、清选中项。禁止 Destroy(gameObject)。
    /// </summary>
    protected virtual void OnCloseForm() { }

    /// <summary>
    /// 关闭自己。子类不要直接 Destroy，也不要用 GameEntry.UI.CloseUIForm(FormId)，
    /// 否则 UIRouter 的「已打开」记录会变成脏数据。
    /// </summary>
    protected void CloseSelf()
    {
        int formId = FormId;
        if (formId <= 0)
        {
            Debug.LogWarning($"[{GetType().Name}] CloseSelf 时 FormId 无效（界面未打开？），已忽略");
            return;
        }

        UIRouter.Close(formId);
    }

    private void AcquireLocks()
    {
        if (m_LocksAcquired) return;
        m_LocksAcquired = true;

        UICursorService.Lock(CursorKey);
        if (IsModal) UIPauseService.Push(CursorKey);
    }

    private void ReleaseLocks()
    {
        if (!m_LocksAcquired) return;
        m_LocksAcquired = false;

        if (IsModal) UIPauseService.Pop(CursorKey);
        UICursorService.Unlock(CursorKey);
    }

    /// <summary>
    /// 把根节点拉伸到父 Canvas 的大小（等价于 Inspector 里锚点四向对齐 + 偏移全 0）。
    /// </summary>
    private void StretchToParentCanvas()
    {
        RectTransform rect = transform as RectTransform;
        if (rect == null) return;

        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.localScale = Vector3.one;
    }
}

//------------------------------------------------------------
// ACDemo — GameEntry 便利属性扩展
// GameEntry 本体由 StarForce (GameFramework) 提供
// 本文件通过 partial 类添加静态属性快捷访问各组件
//------------------------------------------------------------

using UnityGameFramework.Runtime;

/// <summary>
/// 游戏入口（partial 扩展 — 便利属性）。
///
/// 【为什么是懒加载】GF 的组件挂在 GameFramework 预制体的子物体上（UI、Event、Procedure…），
/// 本类既没有挂在任何物体上，也没有可靠的 Start 时机；一旦有人先访问 GameEntry.UI，
/// 旧的 Start 初始化就可能还没跑，直接拿到 null（HUD 打不开就是这么发生的）。
/// 改成首次访问时向 UnityGameFramework.Runtime.GameEntry 的组件注册表要，
/// 谁先访问都能拿到，也不依赖脚本执行顺序。
/// </summary>
public partial class GameEntry : UnityEngine.MonoBehaviour
{
    private static BaseComponent s_Base;
    private static EventComponent s_Event;
    private static ProcedureComponent s_Procedure;
    private static SceneComponent s_Scene;
    private static UIComponent s_UI;
    private static ResourceComponent s_Resource;
    private static FsmComponent s_Fsm;
    private static SoundComponent s_Sound;
    private static DataTableComponent s_DataTable;
    private static SettingComponent s_Setting;
    private static EntityComponent s_Entity;
    private static ObjectPoolComponent s_ObjectPool;
    private static DownloadComponent s_Download;
    private static WebRequestComponent s_WebRequest;

    /// <summary>获取游戏基础组件</summary>
    public static BaseComponent Base => Resolve(ref s_Base);

    /// <summary>获取事件组件</summary>
    public static EventComponent Event => Resolve(ref s_Event);

    /// <summary>获取流程组件</summary>
    public static ProcedureComponent Procedure => Resolve(ref s_Procedure);

    /// <summary>获取场景组件</summary>
    public static SceneComponent Scene => Resolve(ref s_Scene);

    /// <summary>获取界面组件</summary>
    public static UIComponent UI => Resolve(ref s_UI);

    /// <summary>获取资源组件</summary>
    public static ResourceComponent Resource => Resolve(ref s_Resource);

    /// <summary>获取有限状态机组件</summary>
    public static FsmComponent Fsm => Resolve(ref s_Fsm);

    /// <summary>获取声音组件</summary>
    public static SoundComponent Sound => Resolve(ref s_Sound);

    /// <summary>获取数据表组件</summary>
    public static DataTableComponent DataTable => Resolve(ref s_DataTable);

    /// <summary>获取配置组件</summary>
    public static SettingComponent Setting => Resolve(ref s_Setting);

    /// <summary>获取实体组件</summary>
    public static EntityComponent Entity => Resolve(ref s_Entity);

    /// <summary>获取对象池组件</summary>
    public static ObjectPoolComponent ObjectPool => Resolve(ref s_ObjectPool);

    /// <summary>获取下载组件</summary>
    public static DownloadComponent Download => Resolve(ref s_Download);

    /// <summary>获取网络组件</summary>
    public static WebRequestComponent WebRequest => Resolve(ref s_WebRequest);

    /// <summary>
    /// 从 GF 的组件注册表取组件；第一次访问时缓存下来。
    /// 组件还没注册时返回 null，调用方按原有逻辑判空即可。
    /// </summary>
    private static T Resolve<T>(ref T cache) where T : GameFrameworkComponent
    {
        if (cache == null)
        {
            cache = UnityGameFramework.Runtime.GameEntry.GetComponent<T>();
        }

        return cache;
    }
}

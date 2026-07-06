//------------------------------------------------------------
// ACDemo — GameEntry 便利属性扩展
// GameEntry 本体由 StarForce (GameFramework) 提供
// 本文件通过 partial 类添加静态属性快捷访问各组件
//------------------------------------------------------------

using UnityGameFramework.Runtime;

/// <summary>
/// 游戏入口（partial 扩展 — 便利属性）。
/// </summary>
public partial class GameEntry : UnityEngine.MonoBehaviour
{
    /// <summary>
    /// 获取游戏基础组件。
    /// </summary>
    public static BaseComponent Base { get; private set; }

    /// <summary>
    /// 获取事件组件。
    /// </summary>
    public static EventComponent Event { get; private set; }

    /// <summary>
    /// 获取流程组件。
    /// </summary>
    public static ProcedureComponent Procedure { get; private set; }

    /// <summary>
    /// 获取场景组件。
    /// </summary>
    public static SceneComponent Scene { get; private set; }

    /// <summary>
    /// 获取界面组件。
    /// </summary>
    public static UIComponent UI { get; private set; }

    /// <summary>
    /// 获取资源组件。
    /// </summary>
    public static ResourceComponent Resource { get; private set; }

    /// <summary>
    /// 获取有限状态机组件。
    /// </summary>
    public static FsmComponent Fsm { get; private set; }

    /// <summary>
    /// 获取声音组件。
    /// </summary>
    public static SoundComponent Sound { get; private set; }

    /// <summary>
    /// 获取数据表组件。
    /// </summary>
    public static DataTableComponent DataTable { get; private set; }

    /// <summary>
    /// 获取配置组件。
    /// </summary>
    public static SettingComponent Setting { get; private set; }

    /// <summary>
    /// 获取实体组件。
    /// </summary>
    public static EntityComponent Entity { get; private set; }

    /// <summary>
    /// 获取对象池组件。
    /// </summary>
    public static ObjectPoolComponent ObjectPool { get; private set; }

    /// <summary>
    /// 获取下载组件。
    /// </summary>
    public static DownloadComponent Download { get; private set; }

    /// <summary>
    /// 获取网络组件。
    /// </summary>
    public static WebRequestComponent WebRequest { get; private set; }

    /// <summary>
    /// 初始化内置组件引用。
    /// 在 GameEntry 本体 Start() 中调用，将 GF 框架组件缓存到静态属性。
    /// </summary>
    private static void InitBuiltinComponents()
    {
        Base = UnityGameFramework.Runtime.GameEntry.GetComponent<BaseComponent>();
        Event = UnityGameFramework.Runtime.GameEntry.GetComponent<EventComponent>();
        Procedure = UnityGameFramework.Runtime.GameEntry.GetComponent<ProcedureComponent>();
        Scene = UnityGameFramework.Runtime.GameEntry.GetComponent<SceneComponent>();
        UI = UnityGameFramework.Runtime.GameEntry.GetComponent<UIComponent>();
        Resource = UnityGameFramework.Runtime.GameEntry.GetComponent<ResourceComponent>();
        Fsm = UnityGameFramework.Runtime.GameEntry.GetComponent<FsmComponent>();
        Sound = UnityGameFramework.Runtime.GameEntry.GetComponent<SoundComponent>();
        DataTable = UnityGameFramework.Runtime.GameEntry.GetComponent<DataTableComponent>();
        Setting = UnityGameFramework.Runtime.GameEntry.GetComponent<SettingComponent>();
        Entity = UnityGameFramework.Runtime.GameEntry.GetComponent<EntityComponent>();
        ObjectPool = UnityGameFramework.Runtime.GameEntry.GetComponent<ObjectPoolComponent>();
        Download = UnityGameFramework.Runtime.GameEntry.GetComponent<DownloadComponent>();
        WebRequest = UnityGameFramework.Runtime.GameEntry.GetComponent<WebRequestComponent>();
    }

    private void Start()
    {
        InitBuiltinComponents();
    }
}

//------------------------------------------------------------
// ACDemo — 启动开关（开发期直达 / 正式流程）
// 放置路径: Assets/GameMain/Scripts/Base/GameStartupConfig.cs
// 对应资源: Assets/GameMain/Resources/Config/GameStartupConfig.asset
//------------------------------------------------------------

using UnityEngine;

/// <summary>
/// 启动开关：决定 <see cref="ProcedureLaunch"/> 是直接进游戏，还是走主菜单。
///
/// 【为什么做成 Resources 下的 SO，而不是写在 ProcedureLaunch 里的常量】
///   1. 开发期要「不改代码切流程」：Inspector 勾一下 skipMainMenu 即可，
///      不用为了验证主菜单再去改一行代码、再改回来；
///   2. GF-4 要有正式的「主菜单 → Loading → 关卡」链路，两条路径要能共存，
///      开关留在资产里最省事；
///   3. 资产缺失时不崩：<see cref="EnsureLoaded"/> 退回开发期默认值只打一条 Warning，
///      新克隆的仓库就算 Resources 没同步过来也不会卡在启动界面。
///
/// 【约定】
///   - 只放「启动时读一次」的开关；运行期会变的状态不要塞进来（那是存档/设置的事）；
///   - 读取统一走 <see cref="Instance"/>，不要各处 Resources.Load。
/// </summary>
[CreateAssetMenu(menuName = "ACDemo/GameStartupConfig", fileName = "GameStartupConfig")]
public class GameStartupConfig : ScriptableObject
{
    /// <summary>Resources 下的相对路径（不含扩展名）</summary>
    public const string ResourcePath = "Config/GameStartupConfig";

    /// <summary>已加载的实例；null 表示还没加载过</summary>
    private static GameStartupConfig s_Instance;

    [Header("流程")]
    [Tooltip("true = 跳过主菜单，直接进 ProcedureGame（开发期默认）；false = MainMenu → Loading → Game")]
    [SerializeField] private bool m_SkipMainMenu = true;

    [Header("开发期场景")]
    [Tooltip("开发期直达 / 返回主菜单时要加载的场景路径（GF-4 之后由主菜单与关卡路径接管）")]
    [SerializeField] private string m_DevScenePath = ScenePaths.TestScene;

    /// <summary>是否跳过主菜单直接进游戏</summary>
    public bool SkipMainMenu => m_SkipMainMenu;

    /// <summary>开发期使用的场景路径（空值时回落到 TestScene）</summary>
    public string DevScenePath =>
        string.IsNullOrEmpty(m_DevScenePath) ? ScenePaths.TestScene : m_DevScenePath;

    /// <summary>全局唯一入口：第一次访问时自动加载</summary>
    public static GameStartupConfig Instance
    {
        get
        {
            EnsureLoaded();
            return s_Instance;
        }
    }

    /// <summary>
    /// 加载启动配置（幂等）。找不到资产时用运行时默认值兜底 —— 默认值是「开发期直达」，
    /// 也就是 GF-3 之前的行为，保证任何情况下都能进 Play 调试。
    /// </summary>
    public static void EnsureLoaded()
    {
        if (s_Instance != null) return;

        // 注意：Resources 路径不带 "Resources/" 前缀，也不带扩展名
        s_Instance = Resources.Load<GameStartupConfig>(ResourcePath);
        if (s_Instance != null) return;

        Debug.LogWarning(
            $"[GameStartupConfig] 未找到 Resources/{ResourcePath}.asset，" +
            "改用运行时默认值（skipMainMenu=true，直达 ProcedureGame）");

        s_Instance = CreateInstance<GameStartupConfig>();
        s_Instance.name = "GameStartupConfig (RuntimeDefault)";
    }

    /// <summary>
    /// 关闭 Domain Reload 时静态字段会跨 Play 会话存活，进入 Play 前必须清空，
    /// 否则改动资产后要重启编辑器才生效。
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        s_Instance = null;
    }
}

//------------------------------------------------------------
// ACDemo — 场景资源路径常量
// EditorResourceMode 下 GF 使用 AssetDatabase 路径（Assets/ 开头，含 .unity 后缀）
//------------------------------------------------------------

/// <summary>
/// 场景资源路径常量（避免拼写错误）。
/// 当前仅保留 TestScene；后续自建主菜单/关卡场景后改回对应路径即可。
/// </summary>
public static class ScenePaths
{
    /// <summary>主菜单场景（暂指向 TestScene，待自建后替换）</summary>
    public const string MainMenu = "Assets/GameMain/Scenes/TestScene.unity";

    /// <summary>游戏关卡场景（暂指向 TestScene，待自建后替换）</summary>
    public const string MainScene = "Assets/GameMain/Scenes/TestScene.unity";

    /// <summary>当前唯一可用场景</summary>
    public const string TestScene = "Assets/GameMain/Scenes/TestScene.unity";
}

//------------------------------------------------------------
// ACDemo — 场景资源路径常量
// EditorResourceMode 下 GF 使用 AssetDatabase 路径（Assets/ 开头，含 .unity 后缀）
//------------------------------------------------------------

/// <summary>
/// 场景资源路径常量（避免拼写错误）。
/// </summary>
public static class ScenePaths
{
    /// <summary>主菜单场景（Build Index 0）</summary>
    public const string MainMenu = "Assets/GameMain/Scenes/MainMenu.unity";

    /// <summary>游戏关卡场景（Build Index 1）</summary>
    public const string MainScene = "Assets/GameMain/Scenes/MainScene.unity";
}

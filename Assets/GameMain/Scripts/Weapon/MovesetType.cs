/// <summary>
/// 动作集类型：由左右手装备组合解析出的结果（唯一判定点见 MovesetResolver）。
/// </summary>
public enum E_MovesetType
{
    /// <summary>空手 / 双盾 / 非法组合</summary>
    Unarmed,
    /// <summary>右手单手近战 + 左手空</summary>
    OneHandedSword,
    /// <summary>右手单手近战 + 左手盾</summary>
    SwordAndShield,
}

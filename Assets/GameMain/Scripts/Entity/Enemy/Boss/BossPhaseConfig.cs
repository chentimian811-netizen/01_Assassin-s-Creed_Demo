using UnityEngine;

/// <summary>
/// Boss 阶段配置。P6 实装阈值与招式切换。
/// </summary>
[CreateAssetMenu(menuName = "ACDemo/Configs/Boss Phase Config", fileName = "BossPhaseConfig")]
public class BossPhaseConfig : ScriptableObject
{
    [Tooltip("进入该阶段的血量比例阈值（0-1）")]
    public float healthThreshold = 0.5f;

    [Tooltip("阶段序号，从 0 开始")]
    public int phaseIndex = 1;
}

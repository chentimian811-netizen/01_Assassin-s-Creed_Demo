using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 动作集数据（对应 CBTFM 的 SO_WeaponPreset）。
/// 覆盖器先留空：步骤 8 配好 .overrideController 后再挂。
/// </summary>
[CreateAssetMenu(menuName = "Combat System/MovesetConfig")]
public class MovesetConfig : ScriptableObject
{
    [Tooltip("本资产对应的动作集类型")]
    public E_MovesetType movesetType;

    [Header("移动动作集覆盖器（待机/走/跑/蹲/空中）。null = 用基础控制器自带")]
    public AnimatorOverrideController locomotionOverride;

    [Header("右手连招 → Layer1 动作槽位名")]
    public List<AttackData> rightHandAttacks = new List<AttackData>();

    [Header("左手连招（盾击 / 弹反）。留空 = 左手无独立攻击")]
    public List<AttackData> leftHandAttacks = new List<AttackData>();

    [Header("演出动作槽位名")]
    public string blockStartSlot = "Block_Start";
    public string blockLoopSlot = "Block_Loop";
    public string blockEndSlot = "Block_End";

    [Header("移动档位（不同动作集步伐节奏不同时覆盖 PlayerAnimator 默认值）")]
    public float walkMoveTier = 2f;
    public float runMoveTier = 5f;
}

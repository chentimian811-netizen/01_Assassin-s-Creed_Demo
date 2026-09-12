using UnityEngine;

[CreateAssetMenu(menuName = "ACDemo/Configs/Stamina Config", fileName = "StaminaConfig")]
public class StaminaConfig : ScriptableObject
{
    [Header("上限")]
    public float maxStamina = 100f;

    [Header("恢复")]
    [Tooltip("每次消耗后，等待这么久才开始恢复")]
    public float regenDelayAfterUse = 1f;
    public float regenPerSecond = 15f;
    [Tooltip("力竭后的锁定时间，期间不恢复")]
    public float exhaustedLockDuration = 2f;

    [Header("消耗（对齐毕业设计方案 §4.1）")]
    public float lightAttackCost = 12f;
    public float heavyAttackCost = 25f;
    public float dodgeCost = 20f;
    public float parryFailCost = 15f;
    public float sprintCostPerSecond = 8f;

    [Header("格挡")]
    public float blockCostPerSecond = 10f;

    [Header("跳跃/下落（可选，暂按 0 处理）")]
    public float jumpCost = 0f;
}

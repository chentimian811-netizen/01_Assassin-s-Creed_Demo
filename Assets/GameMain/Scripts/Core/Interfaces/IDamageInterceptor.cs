using UnityEngine;

/// <summary>
/// 伤害拦截层：由【受击方】实现，在 DamageRouter 真正扣血之前被询问一次。
///
/// 【为什么挂在受击方】
///   攻击方（MeleeFighter / Projectile / 环境脚本）只负责"把伤害打出去"；
///   "这一下该不该吃"属于受击方自己的状态（格挡 / 弹反 / 将来的护盾、减伤光环）。
///   放在受击方之后，近战、投射物、环境伤害都走同一道闸门 ——
///   否则每新增一条伤害通道就要补一遍 block 判断，漏一条就是"某类攻击无视格挡"。
///
/// 【为什么不写进 Health】
///   Health 是纯结算组件（血量 / 无敌来源 / 致死），不认识"格挡"这种玩法概念。
///   拦截层是它前面的可插拔钩子，询问顺序与判空统一由 DamageRouter 负责，
///   这样将来加护盾只加一个实现，不动 Health、也不动任何攻击方。
///
/// 【约定】
///   - 只有 DamageRouter 会调用本接口，任何地方都不要自己遍历拦截器；
///   - 返回 true = 本次伤害已被处理（可改写 Amount，改成 0 即完全免伤）；
///     返回 false = 放行，DamageRouter 继续询问下一个拦截器（目前每单位最多一个）。
/// </summary>
public interface IDamageInterceptor
{
    /// <summary>
    /// 拦截一次即将结算的伤害。
    /// </summary>
    /// <param name="info">
    /// 伤害包。结构体按引用传入，允许改写 <see cref="DamageInfo.Amount"/>；
    /// 改成 0 或负数表示本次伤害完全不结算（不会进受击流程、不会播卡肉）。
    /// </param>
    /// <returns>true = 已处理，不再询问其它拦截器；false = 放行</returns>
    bool TryIntercept(ref DamageInfo info);
}

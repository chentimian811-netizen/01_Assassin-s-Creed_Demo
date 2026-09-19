---
name: acdemo-combat-damage
description: 本项目的战斗伤害通道与受击事件约定（DamageInfo / DamageRouter / IDamageable / GameEvents + UnitId）。当改动近战、远程、血量、受击硬直、弹反格挡、卡肉顿帧或时间缩放时使用。
---

# 战斗与伤害通道

## 触发条件

- 新增伤害来源（武器、弹道、技能、陷阱）
- 改血量、受击反馈、死亡、硬直
- 改弹反 / 格挡 / i-Frame
- 改顿帧、暂停、`Time.timeScale` 相关逻辑

## 前置条件

- 目标身上有 `Health`（实现 `IDamageable`），且 `Health.UnitId` 全局唯一
- 伤害统一走 `DamageRouter`，不要绕过它直接扣血
- `HitStopManager` 是 `Time.timeScale` 的唯一所有者

## 规则与步骤

1. 构造 `DamageInfo`（含 `Source`、`Amount`、`poiseDamage`、`critical` 等），交给 `DamageRouter` 投递。
2. 受击方复用 `Health`：扣血 clamp 到 `MaxHealth`；玩家侧广播 `GameEvents.RaisePlayerHealthChanged`，单位侧广播 `RaiseUnitDamaged(unitId, info)`。
3. **定向受击必须带 `UnitId`**：`OnDamageTaken` 只广播"有人受伤"（音效/飘字/镜头用），不知道受害者。任何"我被打/我要死"的判断必须订阅 `OnUnitDamaged` 并比对 `unitId == Health.UnitId`。历史上 4 只同名 Enemy 曾一起倒下。
4. 事件订阅成对：`OnEnable`/`OnOpen` 订阅 ↔ `OnDisable`/`OnClose` 退订；**禁止 lambda / 匿名方法订阅**（无法退订，必泄漏）。`GameEvents` 由 `ResetStatics` 在进入 Play 前清空订阅者（关闭 Domain Reload 时的僵尸订阅）。
5. 卡肉/暂停：调 `HitStopManager.Instance` 的 `Pause()` / `Resume()` / 卡肉协程。暂停优先，卡肉不覆盖暂停。**任何地方都不要直接写 `Time.timeScale`**。
6. 弹反：实现 `IParryTarget`，成功/失败广播 `RaiseParrySuccess` / `RaiseParryFailed`。注意 `ParrySystem` 尚未接线（`GetBlockInput` 内的调用被注释，P3 待办）。
7. 耐力消耗：调 `PlayerStamina.TryConsume(cost)`，配置来自 `StaminaConfig`（`dodgeCost` 已用，`heavyAttackCost` 暂无调用方）。
8. 远程：`RangedFighter` + `Projectile` + `ProjectilePool`（走池，不要每发 `Instantiate`）；武器需 `WeaponConfig.isRanged = true`。
9. 近战命中盒：装备到玩家的武器模型必须落在 `Playehitbox(8)`，且移除残留 `Rigidbody`（否则 Trigger 打不到敌人 `CharacterController`）——由 `WeaponManager.EquipWeapon` 保证。

## 验证方式

- Play 中攻击敌人：只有被命中的那一只进入受击/死亡状态，其余敌人不受影响。
- 连续攻击 + 打开 modal 面板：暂停不会被卡肉意外解除。
- 玩家血条数值与 `Health.CurrentHealth` 一致；耐力条随翻滚/攻击下降。

## 失败处理

- 全体敌人一起死 → 漏了 `UnitId` 比对，退回订阅 `OnDamageTaken` 了。
- 暂停后画面仍在动 / 暂停无法解除 → 有人绕过 `HitStopManager` 写了 `timeScale`。
- 退出 Play 报 `MissingReferenceException` → `GameEvents` 有 lambda 订阅或漏退订。
- 伤害数值刚好是 5 → 命中了 `DamageRouter.FallbackBaseDamage` 兜底，说明伤害来源没被正确解析。
- 剑砍不中但脚能中 → 武器模型带了 `Rigidbody` 或层不在 `Playehitbox(8)`。

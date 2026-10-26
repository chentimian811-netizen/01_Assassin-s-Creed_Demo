# 数值迁移基准（Task 0）

> 本文件是 Lüban 迁移的**对照基准**：迁移后每一项默认行为都必须与这里的值一致，
> 除非用户明确批准改数值。正式实施时若已再次调整过数值，以 Unity 资产为准并更新本文件。
>
> - 盘点日期：2026-10-27
> - 分支：`feature/luban-so-config`（基线提交 `2889e11 修改角色和敌人预制体数值`）
> - 场景：`Assets/GameMain/Scenes/TestScene.unity`
> - 盘点方式：用一个临时的 Unity Editor 脚本把 Prefab / 场景实例 / 配置资产的
>   序列化值导出成文本（输出到 `Logs/config-baseline-dump.txt`，该目录被 gitignore）。
>   该脚本已在 Task 13 删除，本文件保留的就是它的结论。
>   **没有**用“只读 YAML”推断：YAML 缺字段时 Unity 使用 C# 字段初始值，把缺字段当 0 会填错表。
> - 代码侧常量：直接读脚本，逐条标注文件与行号。

---

## 0. 与实施计划附录 §15 的差异（必须先知道）

计划里的“当前有效值基准”是 2026-10-26 的只读盘点，本次复盘的结论如下：

| 项 | 计划 §15 写的 | 本次实测 | 说明 |
|---|---|---|---|
| `BowConfig.attackRange` | `2.5` | **`5`** | 计划附录已过期，填表用 5 |
| 敌人 `Health.maxHealth` | 1000 | 1000 | 一致（Prefab 与场景实例都是 1000） |
| 敌人 `weaponID` | 10001 | 10001 | 一致 |
| `PlayerLockOn.lockRotateSpeed` | 视为“锁定转向速度”生效值 | **声明了但没有任何消费方** | 真正生效的是 `PlayerAnimator.lockRotateSpeed = 8`（第 24 行，第 129 行使用） |
| 敌人 `CharacterController` 高度 / `NavMeshAgent.baseOffset` | 未记录 | `2.15` / `0.3` | 属于引擎组件参数，按计划 §3.3 **不迁移** |
| `MeleeFighter.hitStopDuration`（玩家） | 未记录 | `0.09`（敌人为 0） | 只在“命中时拿不到 AttackData”时兜底；计划 §5.14 未列入，**本次不迁移**，仅记录 |
| `ShieldConfig.attackRange` | 1.2 | 1.2 | 一致 |
| 其余 | — | 与 §15 一致 | — |

其它需要单独决策、**不在本次擅自修复**的既有异常（计划 §2.7）：

1. `BowConfig.weaponID = 1111`，但 Item 表没有 1111；Item `10004 Hunting Bow` 的 `WeaponType` 又是 `1`(Dagger)。
   这是 Task 7 的决策闸门，未批准前不迁移弓的 weaponID、不改物品类型。
2. `ParryConfig` 资产 0 个、`BossPhaseConfig` 资产 0 个、场景/Prefab 中也没有
   `ParrySystem` / `BossPhaseController` 实例 → 弹反与 Boss 只能做“表结构 + 读取链路”，
   Play 验收明确记为**未覆盖**。
3. `Item.txt` 第 9 行（10007 Axe）`Description` 列已经是 `SharpAxe��`（历史乱码），
   仅记录，不在数值迁移里改写文案。

---

## 1. 玩家（Player）

`Male.prefab` 与 TestScene 实例 `Character/Male` **逐项一致**，场景没有覆盖这些数值。

### 1.1 Health

| 字段 | 值 | 备注 |
|---|---|---|
| `maxHealth` | 100 | 类默认 25 不生效 |
| `refillOnEnable` | true | 留在 Prefab（计划 §3.3） |
| `unitId` | 空字符串 | Awake 时自动生成，玩家固定为 `player` |

### 1.2 PlayerMovement（序列化字段）

| 字段 | 值 |
|---|---|
| `crouchSpeed` | 1.12 |
| `walkSpeed` | 2.33 |
| `runSpeed` | 3.43 |
| `gravity` | -9.8 |
| `maxHeight` | **1**（类默认 1.5） |
| `walkRootMotionRefSpeed` | 2.33 |
| `runRootMotionRefSpeed` | 3.43 |
| `minAnimSpeed` | 0.85 |
| `maxAnimSpeed` | 1.25 |
| `rootMotionEpsilon` | 0.0005 |

### 1.3 玩家手感代码常量（非序列化，`PlayerMovement.cs`）

| 字段 | 值 | 行号 |
|---|---|---|
| `fallMultiplier` | 1.5 | PlayerMovement.cs:38 |
| `jumpCD` | 0.15 | PlayerMovement.cs:39 |
| `groundCheckOffset` | 0.5 | PlayerMovement.cs:40 |
| `fallHeight` | 0.5 | PlayerMovement.cs:41 |
| `CACHE_SIZE` | 3 | PlayerMovement.cs:60（内部缓存，**不迁移**） |

### 1.4 其它玩家组件

| 组件 / 字段 | 值 | 位置 |
|---|---|---|
| `PlayerController.SprintHoldThreshold` | 0.2（`const`） | PlayerController.cs:93 |
| `PlayerLockOn.lockDistance` | 15 | PlayerLockOn.cs:17 |
| `PlayerLockOn.lockRotateSpeed` | 8（**无消费方**） | PlayerLockOn.cs:16 |
| `PlayerAnimator.lockRotateSpeed` | 8（真正生效） | PlayerAnimator.cs:24 / 129 |
| `PlayerAnimator.turnGainDegPerRad` | 200 | 序列化 |
| `PlayerAnimator` 动画移动档位 | idle 0 / walk 2 / run 5 / crouch 1.1214 | 序列化，按计划 §3.2 留在组件 |
| `PlayerCombat.enableCounterAttack` | false | 序列化 |

### 1.5 PlayerDodge

| 字段 | 值 |
|---|---|
| `staminaConfig` | **空**（运行时从 `PlayerStamina.Config` 取同一份 SO） |
| `dodgeDuration` | 0.9 |
| `dodgeCooldown` | 0.2 |
| `dodgeDistance` | 3.5 |
| `moveStartDelay` | 0 |
| `moveDuration` | 0.45 |
| `moveCurve` | 5 个关键帧（**留在 Prefab，不迁移**） |
| `iframeStartDelay` | 0.05 |
| `iframeDuration` | 0.4 |
| `turnToDodgeDirTime` | 0.1 |
| `allowNeutralDodge` | true |

### 1.6 MeleeFighter / RangedFighter（玩家）

| 字段 | 值 | 备注 |
|---|---|---|
| `weaponID` | 10001 | |
| `attacks` | Sword_Attack1/2/3 | 资源引用，留在 Prefab |
| `hitStopDuration` | 0.09 | 仅 AttackData 缺失时兜底，本次不迁移 |
| `hitStopTimeScale` | 0 | 同上 |
| `autoFitWeaponHitbox` | true | |
| `logHitboxBinding` | true | |
| `RangedFighter.currentAmmo` / `maxAmmo` | 30 / 30 | 运行时状态，不迁移 |

---

## 2. 耐力（`StaminaConfig_Default.asset`）

| 字段 | 值 |
|---|---|
| `maxStamina` | 120 |
| `regenDelayAfterUse` | 0.8 |
| `regenPerSecond` | 35 |
| `exhaustedLockDuration` | 1 |
| `lightAttackCost` | 8 |
| `heavyAttackCost` | 20（**当前无调用方**） |
| `dodgeCost` | 10 |
| `parryFailCost` | 15（当前无调用方） |
| `sprintCostPerSecond` | 8（当前无调用方） |
| `blockCostPerSecond` | 6（当前无调用方） |
| `jumpCost` | 0（当前无调用方） |

> 翻滚实际扣 10，不是 StaminaConfig 类默认值 20：`PlayerDodge.staminaConfig` 为空时会回落到
> `PlayerStamina.Config` 指向的 `StaminaConfig_Default`。

---

## 3. 弹反（未启用功能）

- `ParryConfig` 资产：**0 个**；`ParrySystem` 实例：**0 个**（`Male.prefab` 上没有该组件）。
- 以下只是 `ParryConfig.cs` 的**类默认值**，不代表当前生效配置：

| 字段 | 类默认值 |
|---|---|
| `parryWindow` | 0.15 |
| `enemyStaggerDuration` | 1.5 |
| `riposteWindow` | 2 |
| `parryInvulnDuration` | 0.3 |
| `blockDamageReduction` | 0.5 |
| `blockStillKnockback` | true |

---

## 4. 普通敌人（`Enemy.prefab` 与 TestScene `Character/Enemy` 一致）

### 4.1 Health

| 字段 | 值 |
|---|---|
| `maxHealth` | 1000 |
| `refillOnEnable` | true |
| `unitId` | 空（运行时自动生成 `unit_{InstanceID}`） |

### 4.2 EnemyController

| 字段 | 值 |
|---|---|
| `Fov` | 180 |
| `AlertRange` | 20 |
| `minSeparationDistance` | 1.2 |
| `separationForce` | 5 |
| `performanceLockTimeout` | 10 |

### 4.3 各 FSM 状态

| 组件 | 字段 | 值 |
|---|---|---|
| `PatrolState` | `patrolSpeed` | 2 |
| | `patrolForwardAnimSpeed` | 0.2 |
| | `arrivalDistance` | 0.5 |
| | Exit 里 `NavAgent.speed` | **3.5（硬编码）**，PatrolState.cs:145 |
| `AttackStates` | `attackDistance` | 1 |
| | 起手额外容差 | **0.3（硬编码）**，AttackStates.cs:35 |
| `CombatMovementStates` | `circlingSpeed` | 20 |
| | `distanceToSand` | 3 |
| | `adjustDistanceThreshold` | 1 |
| | `idleTimeRange` | (2, 5) |
| | `circlingTimeRange` | (3, 6) |
| | `keepAttackingDeadTargetSeconds` | 4 |
| `RetreatAfterAttackState` | `backwardWalkSpeed` | 1.5 |
| | `distanceToRetreat` | 3 |
| | 转向速度 | **500（硬编码）**，RetreatAfterAttackState.cs:35 |
| `GettingHitState` | `stunnTime` | 0.5 |
| `DeadState` | `destroyDelay` | 4 |

### 4.4 敌人 MeleeFighter

`weaponID = 10001`，`attacks = Slash / OutwardSlash`，`preplacedHitbox = mixamorig:Sword_joint`，
`autoFitWeaponHitbox = false`，`logHitboxBinding = false`，`hitStopDuration = 0`。

### 4.5 留在场景里的接线（不迁移）

- `PatrolPoute.patrolPionts`：4 个 `PatrolPoints`（只在场景里赋值，Prefab 里是 null），`patrolMode = Random(2)`。
- `Setting/EnemyPatrplPoints_1/PatrolPoints*` 上的 `PatrolPiont.waitTime` 全为 0。
- `VisionSensor.enemy`、`MinimapMarker`、`NavMeshAgent`、`CharacterController`。

---

## 5. EnemyManager

| 字段 | 值 | 来源 |
|---|---|---|
| `timeRangeBetWeenAttacks` | (1, 4) | 序列化 |
| `notAttackingTimer` 初值 | 2 | EnemyManager.cs:26 |
| 最近目标刷新周期 | 0.1 | EnemyManager.cs:103 |
| `EnemyLayer` | 128 | 序列化 |
| `Player` | 场景里指向 `Male` | 场景引用，不迁移 |

---

## 6. 武器（`Assets/Resources/WeaponConfigs/`）

| 资产 | weaponID | weaponName | attackRange | isRanged | isShield | 招式 |
|---|---|---|---|---|---|---|
| `WeaponConfig.asset` | 10001 | Sword | 1.5 | false | false | Sword_Attack1/2/3 |
| `AxeConfig.asset` | 10007 | Axe | 2 | false | false | Axe_Attack1/2/3 |
| `BowConfig.asset` | **1111** | Hunting Bow | **5** | true | false | 无 |
| `ShieldConfig.asset` | 10008 | Round Shield | 1.2 | false | true | 无 |

四个资产的 `maxAmmo`(30) / `fireCooldown`(0.5) / `projectilleSpeed`(25) / `aimSpreadAngle`(1) /
`hipFireSpreadAngle`(8) **全部等于类默认值**，即没有任何资产单独配过这些数值。

- 资源引用留在 SO：`weaponPrefab`（SwordPrefab/AxePrefab/BowPrefab/ShieldPrefab）、
  `animOverride`（仅 Axe 有 `Axe.overrideController`）、`attacks`、`projectilePrefab`（Arrow）。
- `attackRange` 当前**没有消费方**，迁移后只能做数据等价验证。
- `ShieldConfig.animOverride = null`；`BowConfig.animOverride` 指向 `PlayerMove.controller`（不是 override）。

---

## 7. 招式（9 个 AttackData）

全部满足：`damageMultiplier = 1`、`poiseDamage = 10`、`parryable = true`、
`hitStopDuration = 0`、`hitStopTimeScale = -1`。

| 资产 | AnimName | Hitbox | ImpactStart ~ ImpactEnd |
|---|---|---|---|
| `Attacks/Sword/Sword_Attack1` | Attack_Light_1 | Weapon(4) | 0.30 ~ 0.65 |
| `Attacks/Sword/Sword_Attack2` | Attack_Light_2 | Weapon(4) | 0.20 ~ 0.65 |
| `Attacks/Sword/Sword_Attack3` | Attack_Light_3 | Shield(5) | 0.35 ~ 0.65 |
| `Attacks/Axe/Axe_Attack1` | Attack_Light_1 | Weapon(4) | 0.30 ~ 0.55 |
| `Attacks/Axe/Axe_Attack2` | Attack_Light_2 | Weapon(4) | 0.15 ~ 0.38 |
| `Attacks/Axe/Axe_Attack3` | Attack_Light_3 | RightFoot(3) | 0.13 ~ 0.50 |
| `Entity/Enemy/Attacks/Slash` | Slash | Weapon(4) | 0.22 ~ 0.50 |
| `Entity/Enemy/Attacks/OutwardSlash` | OutwardSlash | Weapon(4) | 0.32 ~ 0.55 |
| `Entity/Enemy/Attacks/Kick` | Kick | RightFoot(3) | 0.36 ~ 0.56 |

> `Kick` 当前没有被任何 `WeaponConfig.attacks` 或 `MeleeFighter.attacks` 引用，
> 但按计划 Task 8 仍纳入数据完整性检查。

---

## 8. 物品表（`Resources/DataTables/Item.txt`）

8 行，主键 10001~10008 保持原值：

| Id | Type | WeaponType | BaseDamage | CritRate | Star | Name | AssetId |
|---:|---:|---:|---:|---:|---:|---|---|
| 10001 | 1 | 0 Sword | 15 | 0.05 | 5 | Sever | Item_Sever |
| 10002 | 1 | 3 Bow | 12 | 0.08 | 4 | Demon Breaker | Item_DemonBreaker |
| 10003 | 1 | 1 Dagger | 14 | 0.08 | 5 | Ascending Dragon | Item_AscendingDragon |
| 10004 | 1 | 1 Dagger（应疑为 Bow，见 §0） | 10 | 0.15 | 3 | Hunting Bow | Item_HuntingBow |
| 10005 | 2 | -1 | 0 | 0 | 5 | Sandwich | Item_Sandwich |
| 10006 | 2 | -1 | 0 | 0 | 3 | Salmon caviar | Item_Salmon caviar |
| 10007 | 1 | 2 Axe | 13 | 0.05 | 3 | Axe | Item_Axe |
| 10008 | 3 | 5 Shield | 0 | 0 | 3 | Round Shield | Item_RoundShield |

`Shop.txt` 第一阶段**不迁移**，继续走 `TSVParser.ParseShopTable`。

`ItemConfig_SO` 资产只有 1 个（`ItemConfigs/Item_RoundShield.asset`，itemId 10008），
其余物品的 `ItemConfigCache` 命中为 0 —— 这是现状，不是本次迁移造成的。

---

## 9. Play 验证清单状态（Task 0 步骤 8）

基准的全部行为验证项都需要在 Unity Editor 里人工 Play，本次执行侧**无法自动完成**，故：

| 计划 §9 分组 | 状态 |
|---|---|
| 9.1 通用启动 | 未覆盖（待用户 Play） |
| 9.2 武器与招式 | 未覆盖（待用户 Play） |
| 9.3 玩家 | 未覆盖（待用户 Play） |
| 9.4 普通敌人 | 未覆盖（待用户 Play） |
| 9.5 Boss | 未覆盖（工程无 Boss 实例） |
| 9.6 卡肉与暂停 | 未覆盖（待用户 Play） |

迁移过程中每完成一个批次，都要在下面追加一行“批次 / 验证项 / 结果 / 执行人”，
不允许把 Editor 侧校验写成 Play 通过。

# 动作集系统设计与实施计划（Moveset System）

> 类魂战斗动作集 / 左右手双持 —— 简化版埃尔登法环动作框架
>
> **📋 施工入口**：按顺序动手请看 **`Moveset_Implementation_Steps.md`**（分步实施计划，可执行版）。
> 本文回答"为什么这样设计"，那份回答"现在按什么顺序改哪个文件、怎么验证"。

| 项目 | 内容 |
|---|---|
| 文档版本 | v1.3（订正玩家资产为 `Male.prefab`；新增 §2.1.1 实施进度总览、§12.1 真实骨架） |
| 目标项目 | `01_Assassin's Creed_Demo`（Unity 2022.3.62f2c1） |
| 唯一场景 | `Assets/GameMain/Scenes/TestScene.unity` |
| **玩家资产** | **`Assets/GameMain/Entities/Player/Male.prefab`**（骨架 = `Assets/Fantasy knight/Prefab/Skin_1_4.prefab`，`hand_l` / `lowerarm_l` 命名）。⚠️ `Player.prefab` 是**弃用的旧资产**，见 §12.1 |
| 参考框架 | `C:\Unity\CBTFM`（Combat System，第三方战斗框架，仅作模式借鉴） |
| 参考作品 | Elden Ring / Dark Souls III（FromSoftware） |
| 文档状态 | 已批准 → **阶段 0 已完成**（0.1 已验收 / 0.2 代码就绪），**下一步阶段 1（双槽地基）**。<br>进度快照看 **§2.1.1** |

---

## 0. 术语表（全文统一，避免歧义）

| 中文 | 英文 | 定义 |
|---|---|---|
| 动作集 | Moveset | 一套完整的角色动作：移动（待机/走/跑/蹲/空中）+ 攻击 + 防御 + 演出。**由左右手装备组合决定** |
| 动作槽位 | Animation Slot | 动作集内的具名位置，如 `Attack_Light_1`。**槽位名全局统一，内容因动作集而异** |
| 覆盖器 | AnimatorOverrideController | Unity 的动画替换资产。同名状态沿用基础控制器，列出的才替换 |
| 基础控制器 | Base Controller | `PlayerMove.controller`。状态机结构唯一，永不复制 |
| 预设 | WeaponPreset | 一个动作集对应的数据资产：覆盖器 + 招式表 + 移动参数（借鉴 CBTFM） |
| 手位 | Hand Slot | 左手槽 / 右手槽。**注意：本文的"手位"是持握位置，不是"双手握持(2H)"** |
| 持握方式 | Grip | 单手 / 双手握同一把武器。**本期明确不做** |
| 输入缓冲 | Input Buffer | 在动作不可取消阶段按下的输入被暂存，窗口开启后消费 |

> ⚠️ **术语澄清**：需求原文"双手持物品"应理解为**左右手各持一件装备**（左手盾 + 右手剑），
> 而非 FromSoftware 语境的"双手握持同一把武器（Two-Handing）"。本期只做前者，后者不做。

---

## 1. 背景与问题

### 1.1 需求原文

> 分空手和拿刀两种状态和动画。双手持物品，两边手都能持武器。
> 比如一只手盾一只手剑，就是盾剑的动作；或者两只手都是盾，就是拳的动作。
> 不要双手剑了，可以单手剑——单手剑就切换单手剑的攻击动作。

### 1.2 需求背后的真实规则

需求描述的三种情况，归纳出一条规则：

| 左手 | 右手 | 期望动作集 |
|---|---|---|
| 空 | 空 | 拳（空手） |
| 盾 | 盾 | 拳（空手） |
| 空 | 剑 | 单手剑 |
| 盾 | 剑 | 盾剑 |

**关键洞察**：动作集不是"右手武器决定的"，也不是"每件武器自带一套动画"，
而是 **f(左手装备, 右手装备)** 的函数。这是埃尔登法环的真实做法
（`Weapon Moveset Category` 取右手 + `Weapon Override Category` 取左手组合出结果）。

如果按"每件武器自带一套动画"实现，会出现致命矛盾：
**同一把剑，配盾和不配盾要播两套完全不同的动作**，武器资产无法自洽。

### 1.3 现状差距

| 现状 | 位置 | 差距 | 状态 |
|---|---|---|---|
| `PlayerMove.controller` 单套动作 | `Assets/GameMain/Scripts/Entity/Player/Animations/` | 无动作集概念 | 阶段 3 |
| `CombatMode`(bool) 参数已声明但无 C# 写入 | `PlayerMove.controller` | 未接线；本期不使用（见 §3.3） | 决策：保持 |
| `WeaponConfig.animOverride` 直接替换整个 controller | `MeleeFighter.SetWeaponConfig` | 语义错误：攻击 clip 覆盖器被当成整个控制器用 | 阶段 2.4 |
| 两个武器槽指向**同一个**挂点 | **`Male.prefab`**（`hand_l`，fileID `1176333465599525210`） | 无左手位，双持无从谈起 | 阶段 1.3 |
| `WeaponSlot.allowedType` 为单值枚举 | `WeaponSlot.cs` | 一个槽只能接受一种武器类型 | 阶段 1.2 |
| 连招链是"整角色一条" | `MeleeFighter` 的 `List<AttackData> attacks` | 无法表达"左手攻击链 ≠ 右手攻击链" | 阶段 4.1 |
| 敌人**没有可用命中盒** | `MeleeFighter.TryBindPreplacedWeapon()` | —— | ✅ **阶段 0.1 已修**（方案 C，见 §10） |
| 盾的碰撞体常驻开启 | `Male.prefab` 的 `SkelMesh_RoundShield` | 盾走的是手工挂载，`ApplyInitialColliderState` 覆盖不到它 | ⚠️ 阶段 0.2，**实测待补** |
| 盾模型没有可用的预制体 | 只有裸 FBX，无碰撞体、无预制体包装 | `WeaponConfig.weaponPrefab` 无资产可指 | ⚠️ 新增缺口，见 §12.2 |
| 格挡 / 弹反未实现 | `PlayerController.GetBlockInput()` 为空，`ParrySystem` 未接线 | 盾无防御价值 | 阶段 5 |
| 精力未与格挡/受击挂钩 | `StaminaConfig.heavyAttackCost` 无调用方 | 防御无代价，战斗节奏不成立 | 阶段 5 |

> ⚠️ **路径订正**：原文此表把玩家资产写成 `Player.prefab:4313~4326`。
> **那是弃用的旧资产**，真正的玩家是 `Male.prefab`（见 §12.1）。原文其余引用已在 v1.3 一并订正。

---

## 2. 设计目标与范围

### 2.1 本期目标（In Scope）

1. **左右手独立装备**：左手槽 + 右手槽，同一件装备不可同时占用两手。
2. **动作集由组合决定**：三种动作集 `Unarmed` / `OneHandedSword` / `SwordAndShield`，切换链路完整可验证。
3. **动作槽位命名标准化**：Layer1 状态重命名为中立名，两套动作集共用同一份 C# 代码。
4. **左手攻击通道**：左手（盾击）有独立连招链与独立输入。
5. **举盾**：播格挡动画（本期只做表现，不做判定）。

### 2.1.1 实施进度总览（v1.3 快照）

> 这一节是全文状态的单一入口。**下方各章节的设计文字保留原始形态**（作为当初的决策依据），
> 实际是否落地、落地到什么程度，一律以本表和 §10 的实施记录为准。

| 阶段 | 状态 | 产出 | 卡在哪 |
|---|---|---|---|
| **0.1** 敌人命中盒 | ✅ **已验收** | `Enemy.prefab`（`mixamorig:Sword_joint` 挂 `BoxCollider`）、`MeleeFighter.cs` | — |
| **0.2** 盾碰撞体默认关闭 | ⚠️ 代码已就绪，**实测待补** | `WeaponConfig.isShield`、`WeaponManager.ApplyInitialColliderState` | 盾还没走装备链路，`ApplyInitialColliderState` 覆盖不到手工挂的盾 |
| **1.1** `E_WeaponType` 加 `Shield` | ✅ **已完成** | `WeaponType.cs` | — |
| **1.2** `WeaponSlot.allowedTypes` 改数组 | ⬜ 未开始 | `WeaponSlot.cs` | — |
| **1.3** 左手挂点 + 两槽改名 | ⬜ 未开始 | `Male.prefab` | 骨架/挂点 fileID 已勘定，见 §12.1 |
| **1.4** 双持约束 + 目标槽优先 | ⬜ 未开始 | `WeaponManager.cs` | — |
| **1.5** `Item.txt` 编码 + 盾行 | ⬜ 未开始 | `Resources/DataTables/Item.txt` | 原文件是 **GBK**，转换需逐行校对 |
| **2** 动作集路由骨架 | ⬜ 未开始 | `MovesetConfig` / `MovesetResolver` / `PlayerAnimationSet` | — |
| **3** 三个覆盖器落地 | ⬜ 未开始 | `.overrideController` ×3 | 依赖 3.0 状态重命名 |
| **4** 左手攻击通道 | ⬜ 未开始 | `MeleeFighter.cs` / `AttackData.cs` / 输入 | — |
| **5** 举盾 + 格挡判定 + 精力 | ⬜ 未开始 | 控制器 + `ParrySystem` | — |
| **6** 弹反 + 处决 | ⬜ 未开始 | — | 资产现成 |

**已完成落地的文件清单（阶段 0 + 1.1）**：

| 文件 | 改动 | 状态 |
|---|---|---|
| `Assets/GameMain/Entities/Enemy/Enemy.prefab` | `mixamorig:Sword_joint` 加 `BoxCollider`（IsTrigger / `enabled: 0`）+ `Hitbox` 标签 + `Enemyhitbox(9)` 层；`MeleeFighter` 的 `preplacedHitbox` 显式指向它 | ✅ |
| `Assets/GameMain/Scripts/Combat/MeleeFighter.cs` | 命中盒三级查找 + 失败可见化 + Gizmos；`Weapon` 分支不再误开手部盒；自动定尺（默认关闭的实验特性） | ✅ |
| `Assets/Art/Models/Characters/Enemy/Paladin WProp J Nordstrom.fbx.meta` | `isReadable: 0 → 1` | ✅ |
| `Assets/GameMain/Scripts/Weapon/WeaponType.cs` | 末尾追加 `Shield`（值 = 5） | ✅ 阶段 1.1 |
| `Assets/GameMain/Scripts/Weapon/WeaponConfig.cs` | 新增 `isShield` | ✅ |
| `Assets/GameMain/Scripts/Entity/Player/WeaponManager.cs` | 新增 `ApplyInitialColliderState`，`EquipWeapon` / `SwitchToSlot` 两处调用 | ✅ |
| `Assets/GameMain/Entities/Player/Male.prefab` | 手工挂载圆盾到 `hand_l`：Layer 8 / Tag `Hitbox` / `BoxCollider`(IsTrigger) / 移除空 Animator | ✅（用户操作） |

### 2.2 明确不做（Out of Scope）

| 项 | 原因 |
|---|---|
| 双手握持同一把武器（2H） | 需求明确排除；且需要额外的握法状态机 |
| 双剑 / 左手剑 | 内容量控制；架构已预留（加一个枚举值即可） |
| 盾反处决（Parry → Riposte） | 资产现成，但依赖弹反窗口；放第二阶段 |
| 背刺（Backstab） | 同上 |
| 弓箭 / 法杖动作集 | 保持现状，不纳入本期矩阵 |
| 敌人连招升级 | 敌人 FSM 是单动作的，独立议题 |
| 格挡判定与破防（Guard Break） | 本期只播动画；判定放第二阶段（见 §10 阶段 5） |

### 2.3 非目标（Non-Goals）

- 不替换 GameFramework UI 体系
- 不改动 `DamageRouter` 唯一伤害通道（`Health.ForceKill` 为兜底出口）
- 不改动 `Assets/GameFramework/`、`Assets/ThirdParty/` 及所有第三方资源包

---

## 3. 核心设计原则

### 3.1 【宪法】动作集 = f(左手, 右手)

全系统只有**一个**判定点，收敛为纯函数：

```csharp
public static E_MovesetType Resolve(WeaponConfig right, WeaponConfig left)
{
    bool rightSword  = IsCategory(right, E_WeaponType.Sword);
    bool leftShield  = IsCategory(left, E_WeaponType.Shield);

    if (!rightSword)  return E_MovesetType.Unarmed;          // 空手 / 双盾 / 非法组合
    return leftShield ? E_MovesetType.SwordAndShield
                      : E_MovesetType.OneHandedSword;
}
```

**任何地方都不得出现第二个"当前是什么动作集"的判断。** 后续加双剑、双手斧，只改这里。

### 3.2 【宪法】单一真相来源

```csharp
// ✅ 唯一状态：装备了什么
WeaponConfig rightHandConfig, leftHandConfig;

// ✅ bool 只是推导出来的只读属性，绝不单独存储
public bool IsArmed => rightHandConfig != null && !rightHandConfig.isUnarmed;
```

❌ **禁止**：`public bool isCombatMode;` + `animator.SetBool("CombatMode", isCombatMode);`

会产生不同步的三个具体场景：
1. 战斗中死亡 → 复活清空槽位 → bool 忘清 → 空手还在挥剑
2. 读档恢复装备 → bool 初始化为 false → 拿着剑在打拳
3. 商店/背包 UI 直接改数据 → 不经过切换逻辑 → bool 不同步

### 3.3 【决策】`CombatMode` 参数本期不接线

`PlayerMove.controller` 里已有 `CombatMode`(bool) 参数。**保持不接线**，理由：

- 本方案里"是不是空手"由动作集覆盖器决定，不由 bool 决定
- 一旦接线就产生第二个真相来源（违反 §3.2），会在"死亡清空槽位""读档""商店换装"三处必然不同步
- 保留成本为 0（未使用的参数不影响运行时性能）

### 3.4 【原则】基础控制器只做一次，动作集只做覆盖器

```
PlayerMove.controller      ← 状态机结构唯一，永不复制、永不改结构
  ├ StandState / SquatState / AirState / LockOn   ← 结构固定
  └ Layer1: Attack_Light_1/2/3, Attack_Impact, ... ← 槽位名固定

Unarmed.overrideController        ← 只替换 clip
OneHandedSword.overrideController
SwordAndShield.overrideController
```

**为什么不做两套独立 controller**：`PlayerDodge.cs:395` 通过遍历
`runtimeAnimatorController.animationClips` 计算翻滚时长，一式两份必然有一份算错。
覆盖器方案天然避开这个问题。

**红利**：`PlayerAnimator.cs` 里的移动档位
（`idleMoveTier=0 / walkMoveTier=2 / runMoveTier=5 / crouchMoveTier=1.1214`）
两套动作集**共用，一行不改**，因为它们共用同一棵 BlendTree。

### 3.5 【宪法】动作槽位名全局统一

```
❌ 现状：Melee_Attack_1  （武器专属命名）
✅ 目标：Attack_Light_1  （中立槽位名）
```

同一槽位名在所有动作集里都存在，内容不同：

| 槽位名 | `Unarmed` 的内容 | `SwordAndShield` 的内容 |
|---|---|---|
| `Attack_Light_1` | 右直拳 | `Attack_Forward_Slash01` |
| `Attack_Light_2` | 左勾拳 | `Attack_Forward_Slash02` |
| `Shield_Bash_1` | 左直拳 | `Shield_Forward_Bash` |

**收益**：`MeleeFighter.Attack()` 里的 `animator.CrossFade(attack.AnimName, 0.2f, 1)`
**一行都不用改**，就能驱动两套动作。

> 例外：`Melee_CounterAttack` / `Melee_CounterVictim` / `Melee_FallBackDeath` 是演出动作，
> 两态共用，**不重命名**。

---

## 4. 前置阻塞项（必须先解决）

### 4.1 ✅ 已解除（阶段 0.1）：敌人没有可用的命中盒

**现象**：敌人攻击不掉玩家血。

**根因**：敌人招式资产 `HitboxToUse = Weapon(4)`，而
`MeleeFighter.TryBindPreplacedWeapon()` 查找的是**名字完全等于 `Sword`** 且带 `BoxCollider`
的子物体；`Enemy.prefab` 里叫 `Paladin_J_Nordstrom_Sword` 且没有 `BoxCollider`
→ `WeaponCollider` 恒为 `null`。

**影响**：**"剑砍中敌人""盾挡住敌人"全部无法验证**。这是整个战斗系统的黑洞。

> **✅ 状态：已按方案 C 解除，验收通过**（敌人攻击玩家 → 掉血）。
> 实施记录、自动定尺的未完成项、以及两个踩过的坑，都在 §10 阶段 0.1 里。
> 下方四条路线的评估保留原文，作为当初的决策依据。

**方案（四条路线）**：

| 方案 | 做法 | 工作量 | 备注 |
|---|---|---|---|
| A | 给 `Paladin_J_Nordstrom_Sword` 加 `BoxCollider`，并重命名为 `Sword` | 最小（5 分钟） | 贴合现有 `TryBindPreplacedWeapon()` 的查找逻辑 |
| B | 在 `MeleeFighter` 上暴露 `[SerializeField] BoxCollider preplacedHitbox` 直接指定 | 小 | 消掉"靠名字找物体"的脆弱点 |
| **C** | 把 `BoxCollider` 挂到敌人已有的 **`mixamorig:Sword_joint`** 骨骼上（`Enemy.prefab:2309`） | 小 | 碰撞体随动画走，位置天然正确 |
| **D** | **改成 socket 扫掠胶囊（`Physics.CapsuleCastAll`）** | **大** | 见下方 §4.1.1，这是 CBTFM 的做法，长期最优 |

**推荐**：**本期用 C 解除阻塞**（半天内可验证），**把 D 记录为架构演进方向**（§11.15）。

**理由**：C 贴合现有代码形态，能立刻打通"敌人砍到我"的验证链路；
D 是更好的架构，但它需要重写整条判定通道（`MeleeFighter` 的 `OnTriggerEnter`
+ `EnableHitbox`/`DisableAllHitxboxes` + `E_AttackHitbox` + 武器/盾的 Layer 设置），
属于"用架构升级换取更准判定"，**不应该和动作集系统挤在同一期做**。

**验收**：敌人攻击玩家，玩家血量下降，`GameEvents.OnUnitDamaged` 触发。

#### 4.1.1 为什么 D（扫掠胶囊）是更好的架构

CBTFM 的判定**完全不读武器上的 `Collider`**：

```csharp
// CollisionManagerComponent.cs:222-231（节选）
RaycastHit[] hits = Physics.CapsuleCastAll
(
    previousStart, previousEnd,   // 上一帧的武器两端 socket 位置
    m_TraceInfo.m_TraceRadius,    // TraceInfo 里的半径（数据化）
    centerDelta.normalized,       // 本帧位移方向
    distance + 0.01f,             // 本帧位移距离（扫掠，不是点检测）
    m_HitLayer.value,
    QueryTriggerInteraction.Collide
);
```

武器只需要**两个空物体 socket**（`StartSocket` / `EndSocket`），
判定形状是"上一帧位置 → 这一帧位置"的扫掠胶囊。

| 维度 | 我方现状（Trigger + BoxCollider） | CBTFM（扫掠胶囊） |
|---|---|---|
| 快挥时穿透 | ❌ 会漏判（Trigger 是离散检测） | ✅ 扫掠，抗穿透 |
| 静止帧命中 | 取决于碰撞体是否重叠 | ✅ 代码里人为给 0.01 位移兜底 |
| 需要 Rigidbody | ⚠️ 有历史坑（`WeaponManager` 里销毁残留 Rigidbody 的注释） | ✅ 完全不需要 |
| 与拾取物理打架 | ⚠️ 会 | ✅ 不会 |
| 每窗口每目标一次 | 靠 Trigger 的天然去重 | 靠 `HashSet` 去重（语义更明确） |
| 判定形状可调 | 改 Collider 尺寸 | 改一个 `float radius` |
| 判定层过滤 | Layer + Tag | Layer + **GameplayTag 黑名单** |
| 调试可视化 | 无 | Gizmos 画胶囊 + 每帧扫掠轨迹 + 命中点法线 |

**对我方的具体收益**：`§4.2` 那个"盾的碰撞体走路时刮到敌人"的问题**在 D 架构下不存在**——
因为判定不是靠碰撞体常驻开启，而是靠两个 socket 之间的一次显式扫掠查询。

> ⚠️ **CBTFM 在这块的已知缺陷（移植时必须修）**：
> `HitableObject()` 里直接 `hitObject.gameObject.Get<Unit>().OwnerTags`（`:166`），
> **命中任何没有 `Unit` 组件的碰撞体会直接 NRE**。判定层里混入场景静态物就会炸。
> 我方若实现 D，**必须加空值保护**，并明确"每窗口单次命中 / 多次命中"做成配置项
> （CBTFM 是硬编码"每窗口每目标一次"，多段攻击必须在动画上开多个窗口）。

### 4.2 🔴 P0：盾牌碰撞体的误伤风险

> **✅ 状态：代码已完成（阶段 0.2）** —— `WeaponManager.ApplyInitialColliderState` 在装备/切槽位时
> 统一把盾的碰撞体设为 `enabled = false`。**但"走动不误伤"这条验收待盾内容到位后才能实测**
> （项目内目前无盾 Prefab / 无 `ShieldConfig.asset`）。详见 §10 阶段 0.2。

`WeaponManager.EquipWeapon` 会把武器模型整个设为 `Playehitbox(8)` 层。
盾挂到左臂后：

- ✅ 盾击动画中盾的碰撞体命中敌人 → 走 `OnTriggerEnter` → 敌人掉血（正确）
- ❌ 若盾的 collider **全程 `enabled = true`**，走路时会刮到敌人 → "走路掉血"

**方案**：
1. 盾的 `BoxCollider` 初始 `enabled = false`
2. 通过 `AttackData.HitboxToUse = E_AttackHitbox.Shield`（新增枚举值）
   由 `MeleeFighter.EnableHitbox / DisableAllHitxboxes` 精确开窗
3. `AttackData.ImpactStartTime / ImpactEndTime` 必须按盾击动画实测填写

**验收**：站着不动、走动时攻击敌人无效；盾击命中窗口内才生效。

---

## 5. 数据模型

### 5.1 枚举扩展

**实际现状**（`Assets/GameMain/Scripts/Weapon/WeaponType.cs`，v1.3 核对）：

```csharp
public enum E_WeaponType
{
    Sword,      // 0
    Dagger,     // 1
    Axe,        // 2
    Bow,        // 3
    Staff,      // 4
}
```

> ⚠️ **文档原文在此处写错过一次**：原方案把枚举列成 `Unarmed, Sword, Dagger, Axe, Bow, Staff`（6 值，含 `Unarmed`）。
> **实际代码里没有 `Unarmed`** —— `Sword` 就是 0。这个偏差会导致"追加值为 6"的错误结论，
> 已订正。空手的表达方式见 §2.5 的 `WeaponConfig.isUnarmed`，**不占用 `E_WeaponType` 的值**。

**目标**（阶段 1.1）：只在**末尾**追加 `Shield`，保证已有整数索引不变：

```csharp
public enum E_WeaponType
{
    Sword,      // 0
    Dagger,     // 1
    Axe,        // 2
    Bow,        // 3
    Staff,      // 4
    Shield,     // 5  ← 新增：盾。不是武器但走武器通道
}
```

> ⚠️ **连带影响**：`Resources/DataTables/Item.txt` 的 `WeaponType` 列是整数。
> 实测现有数据：`10001=0(Sword)`、`10002=1(Dagger)`、`10003=1(Dagger)`、`10004=3(Bow)`、`10007=2(Axe)`、食物行=`-1`。
> **新增枚举值必须追加在末尾**（= 5），否则语义整体偏移。详见 §6.1 的数据订正。

```csharp
// Assets/GameMain/Scripts/Weapon/MovesetType.cs（新建）
/// <summary>动作集类型：由左右手组合解析出的结果</summary>
public enum E_MovesetType
{
    Unarmed,            // 空手 / 双盾 / 非法组合
    OneHandedSword,     // 右手剑 + 左手空
    SwordAndShield,     // 右手剑 + 左手盾
}
```

### 5.2 `WeaponConfig` 扩展

> **实施状态（v1.3）**：下表中**只有 `isShield` 已落地**（阶段 0.2 需要它来区分盾与武器）。
> 其余 5 个字段随阶段 1 / 阶段 5 一起加，不必提前。

| 字段 | 落地阶段 | 状态 |
|---|---|---|
| `isShield` | 0.2 | ✅ **已实现** |
| `movesetCategory` | 1.1（配合 `E_WeaponType.Shield`） | ⬜ |
| `isUnarmed` | 2.5（空手配置注入） | ⬜ |
| `guardAbsorption` / `guardStaminaCost` / `guardBreakPoise` | 5.2 / 5.3 | ⬜ |

```csharp
// Assets/GameMain/Scripts/Weapon/WeaponConfig.cs（在现有类上追加）
[Header("动作集归属")]
[Tooltip("武器分类。盾请填 Shield")]
public E_WeaponType movesetCategory = E_WeaponType.Sword;

[Tooltip("是否为空手占位配置。空手不产生模型，不参与 id/等级通道")]
public bool isUnarmed = false;

[Tooltip("是否盾类。盾的默认碰撞体状态与武器不同")]   // ← ✅ 唯一已落地的字段
public bool isShield = false;

[Header("盾牌参数（isShield = true 时生效）")]
[Range(0f, 1f)] public float guardAbsorption = 0.7f;   // 伤害吸收率
public float guardStaminaCost = 12f;                   // 每次格挡的精力消耗
public float guardBreakPoise = 30f;                    // 触发破防的累计削韧阈值
```

> **决策：保留 `WeaponConfig` 类名，不改名为 `EquipmentConfig`。**
> 改名需要同步 `Assets/Resources/WeaponConfigs/*.asset` 的脚本引用，
> 成本高、收益低。盾作为特殊 `WeaponConfig` 处理，**写入 `CLAUDE.md` 已知边界**。

### 5.3 `MovesetConfig`（新建，对应 CBTFM 的 `SO_WeaponPreset`）

```csharp
// Assets/GameMain/Scripts/Weapon/MovesetConfig.cs
[CreateAssetMenu(menuName = "Combat System/MovesetConfig")]
public class MovesetConfig : ScriptableObject
{
    public E_MovesetType movesetType;

    [Header("移动动作集覆盖器（待机/走/跑/蹲/空中）。null = 用控制器自带")]
    public AnimatorOverrideController locomotionOverride;

    [Header("右手连招 → Layer1 动作槽位名")]
    public List<AttackData> rightHandAttacks = new List<AttackData>();

    [Header("左手连招（盾击 / 弹反）。留空 = 左手无独立攻击")]
    public List<AttackData> leftHandAttacks = new List<AttackData>();

    [Header("演出动作槽位名")]
    public string blockStartSlot = "Block_Start";
    public string blockLoopSlot  = "Block_Loop";
    public string blockEndSlot   = "Block_End";

    [Header("移动档位（不同动作集步伐节奏不同时覆盖 PlayerAnimator 默认值）")]
    public float walkMoveTier = 2f;
    public float runMoveTier  = 5f;
}
```

### 5.4 三个动作集资产

| 资产 | `movesetType` | 覆盖器 | 右手连招 | 左手连招 |
|---|---|---|---|---|
| `UnarmedMoveset.asset` | `Unarmed` | `Unarmed_Override` | 拳 1/2/3 | 拳（同右手，或留空） |
| `OneHandedSwordMoveset.asset` | `OneHandedSword` | `OneHandedSword_Override` | 斩 1/2/3 | 留空 |
| `SwordAndShieldMoveset.asset` | `SwordAndShield` | `SwordAndShield_Override` | 斩 1/2/3 | 盾击 1/2/3 |

---

## 6. 装备系统改造

### 6.1 数据表订正

**当前 `Item.txt` 的三个问题**：

| 问题 | 证据 | 处理 |
|---|---|---|
| 文件编码是 **GBK 不是 UTF-8** | 首字节 `35 09 49 64`（`#\tId`），直接按 UTF-8 读会失败 | 转为 UTF-8（是否带 BOM 与 C# 约定无关，但需与 `TSVParser` 对齐后统一） |
| `WeaponType` 列与枚举**对不上** | `10002 Demon Breaker` = `1`(Dagger) 但名字像剑；`10003 Ascending Dragon` = `1` 但像长枪 | 逐行核对后订正 |
| 盾牌无法表达 | 需要新增行，且 `Type` 列（1=武器 2=食物）不足以区分盾 | 新增 `Type = 3` (Shield) |

**新增盾牌行（示例，ID 待定）**：

```text
	10008	3	5	0	0	3	Round Shield	...	...	Sprites/UI/Weapon/shield_01	Item_RoundShield
列序：                ↑Type=3   ↑WeaponType=5(Shield)   ↑BaseDamage=0
```

> ⚠️ `WeaponType` 填 **5**（不是 6）—— 因为实际枚举没有 `Unarmed`，`Shield` 追加后是第 6 个值、**下标 5**。
> 本文早先把枚举列成含 `Unarmed` 的 6 值，那个版本会让这里填错。已订正，详见 §5.1。

> ✅ **编码问题已排除**：原文说"`Item.txt` 是 GBK，按 UTF-8 读会失败"。
> **实测不成立** —— 该文件主体是纯 ASCII（首字节 `23 09 49 64` = `#\tId`），
> 只有个别中文标点。`TSVParser` 按 UTF-8 读不会失败，**不需要编码转换**。

> 盾的 `BaseDamage = 0`，伤害全部来自其 `AttackData.DamageMultiplier`（盾击有伤害，但基础值为 0）。

### 6.2 `WeaponSlot` 改造

```csharp
// Assets/GameMain/Scripts/Entity/Player/WeaponSlot.cs
[System.Serializable]
public class WeaponSlot
{
    public string slotName;                    // "RightHand" / "LeftHand"
    public Transform holdPoint;                // 右手: hand_r  左手: 挂在 lowerarm_l 下
    public E_WeaponType[] allowedTypes;        // ← 由单值改为数组

    [HideInInspector] public WeaponConfig currentConfig;
    [HideInInspector] public GameObject currentModel;
    [HideInInspector] public string equippedUid;

    public bool Accepts(E_WeaponType t)
    {
        if (allowedTypes == null) return false;
        for (int i = 0; i < allowedTypes.Length; i++)
            if (allowedTypes[i] == t) return true;
        return false;
    }
}
```

### 6.3 玩家资产改造（`Male.prefab`，**不是** `Player.prefab`）

> ⚠️ **本节标题在 v1.3 订正**：原文写的是 `Player.prefab`，那是**弃用的旧资产**。
> 真正的玩家是 `Assets/GameMain/Entities/Player/Male.prefab`，骨架为
> `Assets/Fantasy knight/Prefab/Skin_1_4.prefab`（`hand_l` / `lowerarm_l` 命名）。
> 下方"现状"引用的 `Player.prefab:4313~4326` 同理作废 —— 完整勘定见 §12.1。

**真实现状（`Male.prefab`）—— 两个槽指向同一挂点**：

```yaml
weaponSlots:
- slotName: MainHand
  holdPoint: {fileID: 1176333465599525210}   # = Skin_1_4 的 hand_l
  allowedType: 0                             # Sword
- slotName: AxeHand
  holdPoint: {fileID: 1176333465599525210}   # ← 同一个挂点！
  allowedType: 2                             # Axe
mainWeaponSlotIndex: 0
```

<details>
<summary>旧资产 <code>Player.prefab</code> 的原始记录（保留备查，勿再引用）</summary>

```yaml
weaponSlots:
- slotName: MainHand
  holdPoint: {fileID: 1684160854168212548}   # J_Bip_R_Hand
  allowedType: 0     # Sword
- slotName: MainHand                            # 名字重复
  holdPoint: {fileID: 1684160854168212548}   # 同一挂点！
  allowedType: 3     # Bow
mainWeaponSlotIndex: 0
```

</details>

**目标结构**：

```
weaponSlots:
- slotName: RightHand
  holdPoint: hand_r                  ← Skin_1_4 的 right hand 骨骼
  allowedTypes: [Sword, Dagger, Axe, Shield]
- slotName: LeftHand
  holdPoint: 新建空物体，父级设 lowerarm_l（左小臂）
  allowedTypes: [Shield, Sword]
mainWeaponSlotIndex: 0   （= RightHand）
```

> 盾挂 **左小臂** 而非左手掌，更接近真实持盾姿势；具体位置需在 Play 里对着
> `SwordAndShield_Block_Loop` 调整。
>
> **⚠️ Q6 已有一半答案**：用户已把圆盾手工挂在 **`hand_l`（左手掌）** 上并调好了
> `Position (-0.02, -0.033, 0)` / `Rotation (71.275, -94.847, -81.633)`。
> 阶段 1.3 要么把这个数值迁到 `lowerarm_l` 挂点重新校准，要么就地改用左手掌 ——
> 需在 Play 里对着格挡动画二选一。
>
> 新增挂点时注意：`holdPoint` 是**跨 prefab 的 stripped Transform**
> （`m_CorrespondingSourceObject` 指回 `Skin_1_4.prefab`），不能凭名字找。

### 6.4 装备规则（两个必须实现的约束）

**约束 1：同一件装备不可同时占用两手**

```csharp
/// <summary>装备时若该 uid 已在另一手，先卸下另一手</summary>
void EnsureNotDualEquipped(string uid, WeaponSlot target)
{
    foreach (var slot in weaponSlots)
    {
        if (slot == target) continue;
        if (slot.equippedUid == uid) UnequipSlotInternal(slot);
    }
}
```

**不实现此约束的后果**：两个模型指向同一个 `PackageLocalItem.uid`，
卸下一边时另一边的 `currentModel` 会变成"已销毁的 GameObject" → 空引用必崩。

**约束 2：目标槽优先，不自动换手**

现有 `FindSlotForWeapon` 是"先找空槽、再找同类型槽替换"。
双槽下这会变成"拖个盾到左手，系统自动把右手的剑顶掉"。

**规则**：装备请求携带**明确的目标槽**，`FindSlotForWeapon` 只在无目标槽时作为兜底。
魂系从不替玩家做换手决策。

---

## 7. 动画系统改造

### 7.1 Layer1 状态重命名（Unity 编辑器操作）

| 现名 | 新名 | 说明 |
|---|---|---|
| `Melee_Attack_1` | `Attack_Light_1` | 右手轻击第 1 段 |
| `Melee_Attack_2` | `Attack_Light_2` | 右手轻击第 2 段 |
| `Melee_Attack_3` | `Attack_Light_3` | 右手轻击第 3 段 |
| `Melee_Impact` | `Attack_Impact` | 受击 |
| *（新增）* | `Shield_Bash_1` | 左手（盾击）第 1 段 |
| *（新增）* | `Shield_Bash_2` | 左手（盾击）第 2 段 |
| *（新增）* | `Shield_Bash_3` | 左手（盾击）第 3 段 |
| *（新增）* | `Block_Start` / `Block_Loop` / `Block_End` | 举盾三件套 |
| `Melee_CounterAttack` | **不改** | 演出动作，两态共用 |
| `Melee_CounterVictim` | **不改** | 同上 |
| `Melee_FallBackDeath` | **不改** | 同上 |

> ⚠️ 重命名后**必须同步**所有引用旧名的代码与资产：
> - `MeleeFighter.cs:120` `animator.CrossFade("Empty", 0.1f, 1)`
> - `Assets/Resources/WeaponConfigs/*.asset` 里 `AttackData` 的 `AnimName`
> - `EnemyController` 相关（敌人用的是 `EnemyController.controller`，不受影响）

### 7.2 三个覆盖器的 clip 映射

素材来源：`Assets/Sword_and_Shield_Anims/Art/Animations/`（同名 `.controller` 已存在，可直接引用其状态）

**`SwordAndShield_Override`**（主力动作集，素材最全）

| 槽位 | 素材 |
|---|---|
| `StandState` Idle | `SwordAndShield_Idle_CombatReady` |
| `Walk_Forward/Left/Right/Back` | `SwordAndShield_Walk_*` |
| `Run_Forward/Sprint_Forward/Back/Left/Right` | `SwordAndShield_Run_*` |
| `Attack_Light_1/2/3` | `Attack_3HitCombo01` 拆三段，或 `Attack_Forward_Slash01/02` + `Attack_Forward_Stab` |
| `Shield_Bash_1/2/3` | `Shield_Forward_Bash`、`Shield_InPlace_QuickBash`、`Shield_Forward_UpwardSwing` |
| `Block_Start/Loop/End` | `Block_Start` / `Block_Loop` / `Block_End` |
| Roll | `Evade_Roll_Forward`、`Evade_Back/Left/Right` |
| Jump | `Jump_InPlace_preJump/Jump/Loop/preLand/Land` |

**`OneHandedSword_Override`**：同上，但**去掉所有盾相关动作**，
`Shield_Bash_*` 留空（该动作集下左手无攻击）。

**`Unarmed_Override`**：素材来自 `Assets/ARPGWarrior/`（空手动作）。
`Shield_Bash_*` 映射到左直拳。**若某槽位素材缺失 → 见 §7.3 fallback**。

### 7.3 动作槽位 fallback 链（容错）

仿 TAE 查表降级。**空手没有"跳跃攻击""处决"怎么办？** 三级降级：

```
1. 当前动作集有该槽位      → 用它
2. 没有 → 用另一动作集的同名槽位
3. 都没有 → 用 Attack_Light_1 兜底 + Debug.LogWarning
```

**禁止**"查不到就什么都不播"——表现是"按了左键人不动"且无报错，是最难排查的一类 bug。

---

## 8. 输入映射改造

### 8.1 现状冲突

| 键 | 当前绑定 | 冲突 |
|---|---|---|
| 鼠标左键 | `LightAttack` **+** `Fire` | 两个 action 抢同一键 |
| 鼠标右键 | `Aim` **+** `Block` | **左手轻击还需要它** |
| V | `Block` | `GetBlockInput` 体为空 |
| E | `GetPickup_ShopInput` | — |

### 8.2 目标映射（魂系标准，不新增按键）

| 操作 | 键位 | Action | 说明 |
|---|---|---|---|
| 右手轻击 | 鼠标左键 | `LightAttack` | 已有 |
| 左手轻击（盾击） | **V** | `LeftAttack` | **新增**，复用现 `Block` 的空壳位置 |
| 举盾（按住） | **鼠标右键按住** | `Block` | 语义改为"按住 = 举盾" |
| 瞄准（持弓） | 鼠标右键 | `Aim` | 仅当 `activeConfig.isRanged` 时响应 |
| 射击 | 鼠标左键 | `Fire` | 仅当持弓时响应 |
| 切换武器 | 数字键 1/2 | — | 保留；建议加"装备/卸下"独立键 |

**`Aim` 与 `Block` 的互斥规则**：
`GetAimInput` / `GetBlockInput` 内先判断当前右手配置是否 `isRanged`，
是则走瞄准，否则走格挡。**两个 action 共用一键时必须显式互斥**，
否则会出现"举着盾同时开镜"。

### 8.3 输入缓冲

**需求**：攻击后摇内按下的输入不应被丢弃。

**现状**：`MeleeFighter.ToTryAttack()` 只在 `Cooldown` 阶段设 `doCombo = true`，
窗口外的输入被直接丢弃。

**方案**：加一个短缓冲（建议 0.2s）：

```csharp
// MeleeFighter 内
float m_bufferedAttackRealtime = -1f;
const float InputBufferWindow = 0.2f;

public void ToTryAttack(MeleeFighter target = null)
{
    if (inCounter) return;

    if (!inAction) { StartCoroutine(Attack(target)); return; }

    // 可连段窗口内：立即接招（现有逻辑）
    if (AttackState == E_AttackState.Cooldown) { doCombo = true; return; }

    // 不可取消阶段：缓冲输入，窗口开启时消费
    m_bufferedAttackRealtime = Time.time;
}
```

在 `Attack` 协程进入 `Cooldown` 的分支里检查缓冲。**这不是"借鉴埃尔登法环"，
这是直接抄它的手感**——拳连击和剑连斩都会明显更跟手。

> **要求（借鉴 CBTFM §11.7 的教训）**：缓冲必须是**具名、可查询的属性**，
> 而不是散落的匿名字段——否则调试时无法显示"现在有没有缓存输入"：
> ```csharp
> /// <summary>是否有一笔待消费的输入（供调试显示 / 状态门禁查询）</summary>
> public bool HasBufferedInput => m_bufferedAttackRealtime >= 0f
>                              && Time.time - m_bufferedAttackRealtime <= InputBufferWindow;
> ```

#### 8.3.1 时效机制：用"窗口开启时清空"代替"每条输入打时间戳"

CBTFM 的 `InputBufferComponent` 是 32 格环形缓冲，**且条目没有时间 TTL**
（`InputBufferComponent.cs:42-60`）。条目只通过两种方式消失：被消费，或被清空。

**清空时机**是它的设计精髓：`ANS_BufferInput.NotifyBegin` 在连招窗口**开启的那一帧**
调 `ClearBufferInfo()`（`ANS_BufferInput.cs:42-53`）——
**上一个招式期间按下的陈旧输入，在下一次允许接招的瞬间就被全部丢弃。**

这比"每条输入记录时间戳 + 消费时判断新鲜度"更简单，且语义更贴合动作游戏：

| 做法 | 语义 | 缺点 |
|---|---|---|
| 时间戳 TTL（我原方案） | "按下的输入在 0.2s 内有效" | 需要给每条输入打时间戳；且"上一次攻击期间乱按的键"仍可能落在窗口内被消费 |
| **窗口开启时清空**（CBTFM） | **"只有本窗口开启后按的键算数"** | 需要一个显式的"窗口开启"时机 |

> **采纳决定**：本期**两者都要**，理由是它们管的是不同的事——
> - **时间戳 TTL** 管"手感窗口"（按下后多久内算数）
> - **窗口开启时清空** 管"输入来源的合法性"（上一次攻击期间的乱按不算）
>
> 落地方式：在 `Attack` 协程进入 `Cooldown` 的分支里，
> **先把 `m_bufferedAttackRealtime` 重置为 -1，再检查是否有新输入**——
> 这样"上一段攻击期间按的键"不会漏进下一段。
>
> 另一个来自 CBTFM 的细节：它把输入的 **`InputActionPhase` 一起入队**
> （Started / Performed / Canceled 分开记录，`InputBufferComponent.cs:10-38`）。
> 我方目前不需要（没有蓄力/长按招式），**但如果将来做重击蓄力，
> 必须能从缓冲里区分"按下"和"松手"**——这个设计点先记在这里。

### 8.4 动作集切换的输入门禁

**问题**：在翻滚 / 攻击 / 受击硬直中途切换动作集（换武器、卸下武器），
会让 Animator 在动作中途换图，表现是**动作瞬移**。

**方案**：收敛成**一个**具名准入门禁属性，而不是在每个调用点写 `if` 组合。

```csharp
// WeaponManager 或 PlayerAnimationSet 内
/// <summary>
/// 当前是否允许切换动作集。翻滚 / 攻击 / 受击 / 死亡期间一律拒绝。
/// 【为什么收敛成一个属性】散落在各调用点的 if 组合必然漏改一处，
/// 且无法在 Inspector / 调试面板里一眼看出"现在为什么切不了"。
/// </summary>
public bool CanSwitchMoveset =>
    !isDead
    && (playerDodge == null || !playerDodge.IsDodging)
    && (meleeFighter == null || !meleeFighter.inAction)
    && !isHitStunned;
```

**调用点规则**：所有改 `runtimeAnimatorController` 的路径**必须**先查 `CanSwitchMoveset`；
不满足时**暂存请求**（`m_pendingMoveset`），在下一次 Update 重试，
而不是直接丢弃——否则玩家在翻滚中换武器会"按键没反应"。

> 这是对 CBTFM "用状态门禁做输入锁"（§11.7）的轻量实现：
> 它用 GameplayTag + `State == Idle` 表达同一件事，我们用一个属性表达。

---

## 9. 战斗流程改造

### 9.1 左手攻击通道

`MeleeFighter` 当前只有一条连招链。改造方案：**保留一个 `MeleeFighter`，
但攻击入口接受"手"参数**。

```csharp
public enum E_Hand { Right, Left }

public void ToTryAttack(E_Hand hand, MeleeFighter target = null)
{
    m_pendingHand = hand;   // Attack 协程内据此选择招式表
    ...
}

List<AttackData> ResolveAttacks(E_Hand hand)
{
    MovesetConfig ms = MovesetResolver.Current;
    if (ms == null) return attacks;                        // 兜底，保持现有行为
    var list = hand == E_Hand.Left ? ms.leftHandAttacks : ms.rightHandAttacks;
    if (list != null && list.Count > 0) return list;
    return ms.rightHandAttacks;                            // 左手无独立连招 → 用右手
}
```

**为什么不建第二个 `MeleeFighter`**：`MeleeFighter` 持有 `currentWeapon`、
`WeaponCollider`、`Health` 引用、`GameEvents` 订阅，建两个会产生
双份伤害判定路径，违反"致死只走 `DamageRouter` 唯一路径"的约定。

### 9.2 独立连招计数器

左右手**必须各自维护连招进度**。否则"右手砍两刀 → 右键盾击 → 变成盾击第 3 段"。

```csharp
int[] m_comboCount = new int[2];   // [0]=Right [1]=Left
```

### 9.3 攻击判定开窗

沿用现有机制，新增 `Shield` 分支：

```csharp
// MeleeFighter.EnableHitbox() 的 switch 内新增
case E_AttackHitbox.Shield:
    if (m_shieldCollider != null) m_shieldCollider.enabled = true;
    break;
```

`m_shieldCollider` 在 `EquipWeapon` / `SetWeapon` 时从左手槽模型上取
（与现有 `WeaponCollider = currentWeapon.GetComponent<BoxCollider>()` 同构）。

**规则**：左手和右手**不可能同时打开判定窗**（一个 `MeleeFighter` 同一时刻只有一条攻击协程）。
这是当前的天然优势，别破坏它。

### 9.4 判定窗口的数据表达（含一条立即改进）

**现状**：`AttackData.ImpactStartTime` / `ImpactEndTime` 是**归一化时间点**，
由 `MeleeFighter.Attack` 协程按 `animState.length` 换算。这等价于 FS 社 TAE 的判定事件，
只是存在 SO 上而不是存在动画上。

**在 3~5 个招式的规模下这个折中完全够用**，本期**不迁移**到 Notify 体系（§11.8）。

**但立即采纳一条改进**：给 `AttackData` 增加 `AnimationCurve` 字段。

```csharp
[Header("手感曲线（替代写死的常量）")]
[Tooltip("判定强度曲线（0~1）。留空 = 窗口内恒为 1。用于做「格挡/弹反的精确帧」")]
[SerializeField] private AnimationCurve impactStrength = AnimationCurve.Constant(0f, 1f, 1f);

[Tooltip("该招式的位移曲线。留空 = 用 Animator.deltaPosition 原样")]
[SerializeField] private AnimationCurve displacementScale = AnimationCurve.Constant(0f, 1f, 1f);
```

**理由**：**手感调优是必然反复的工作**。曲线在 Inspector 里拖拽调参的成本，
远低于"改代码常量 → 等编译 → 进 Play → 试一下 → 再改"的循环。
CBTFM 把这件事做到了工业级（`BlendData.m_BlendCurve` / `RootMotionData` 的逐帧开关，§11.9），
我们至少要把"曲线"这个数据形态引进来。

> **第二阶段的收益**：`displacementScale` 一旦存在，
> 就能实现"盾冲只在前半段有位移""突刺在 30% 处停住"这类需求，
> 而不需要引入完整的 `RootMotionData` 系统。

### 9.5 精力与格挡（第二阶段）

**类魂核心闭环**：

```
攻击消耗精力 → 格挡受击消耗精力 → 精力耗尽 → 破防硬直 → 挨打
```

- `PlayerStamina.TryConsume` 已接好攻击（`PlayerCombat`）与翻滚（`PlayerDodge`）
- 需新增：格挡受击扣精力（`guardStaminaCost`）
- 需新增：破防判定（累计 `guardBreakPoise`）
- `StaminaConfig.heavyAttackCost` 目前无调用方，重击实装后接入

> **没有格挡，盾就只是"换动画的道具"。** 类魂战斗的节奏 80% 来自
> "举盾观察 → 被抓破防 → 翻滚"。这是本系统从"能动"到"好玩"的分水岭。

---

## 10. 实施路线图

> **顺序不可调换。** 从阶段 3（配动画）开始做是最容易犯的错——
> 那是最有成就感的部分，但结构定错会导致配好的动画数据全部返工。

### 阶段 0：解除阻塞 🔴

| 任务 | 产出 | 验收 | 状态 |
|---|---|---|---|
| 0.1 修敌人命中盒 | `Enemy.prefab` + `MeleeFighter` 改造 | 敌人攻击玩家，玩家掉血 | ✅ **已验收** |
| 0.2 盾碰撞体默认关闭 | 盾 `BoxCollider.enabled = false` | 走动不误伤 | ⚠️ 代码完成，**待盾内容到位后补验** |

**0.1 实施记录（方案 C · ✅ 已完成）**：

| 改动 | 位置 | 内容 |
|---|---|---|
| 命中盒 | `Enemy.prefab` → `mixamorig:Sword_joint` | 新增 `BoxCollider`（`IsTrigger`，`enabled = 0`）+ `Hitbox` 标签 + `Enemyhitbox(9)` 层；`Size (1.6, 0.1, 0.1)`、`Center (0.8, 0, 0)` |
| 绑定逻辑 | `MeleeFighter.TryBindPreplacedWeapon` | 三级查找：`preplacedHitbox`(Inspector) → 骨骼名 → `Hitbox` 标签；全部落空时 `LogWarning`（原实现只认名字 `Sword`，静默返回 null） |
| 可视化 | `MeleeFighter.OnDrawGizmosSelected` | 画出命中盒（黄=判定窗口已开，灰=待机）。命中盒平时是 disabled 的，Scene 里根本看不到，这是唯一能确认朝向/尺寸的手段 |
| 顺带修正 | `MeleeFighter.EnableHitbox` | `Weapon` 分支不再同时打开右手 `SphereCollider`（剑招不该带贴身判定盒） |
| 导入设置 | `Paladin WProp J Nordstrom.fbx.meta` | `isReadable: 0 → 1`（自动定尺要读顶点；见下方"未完成项"） |

**验收结果**：✅ 敌人攻击玩家 → 玩家掉血（每刀 5 点，`Health.maxHealth = 25`）。

> **伤害数值 5 是兜底值**：`Enemy.prefab` 的 `weaponID = -1`，所以 `DamageRouter.ResolveAmount`
> 落到 `FallbackBaseDamage = 5`。**不是 bug**，但意味着敌人伤害目前无法用数据表配置 ——
> 要调它得走阶段 1.5（`Item.txt` 订正）并给敌人配上 `weaponID`。

> **⚠️ 未完成项：自动定尺（`autoFitWeaponHitbox`，默认已关闭）**
>
> 动机是把"肉眼试命中盒尺寸"变成"从武器网格顶点量出来"。**没做成**，已默认关闭，
> 当前正确做法是**手工填 `Size` / `Center`** 并用 Gizmos 目视校准。
>
> 已确认的事实：
>
> | 观察 | 数值 | 说明 |
> |---|---|---|
> | 身体网格在 `bindposes[i].inverse` 空间里的高度 | 1.22 米 | 角色实际两米多 → **这不是模型空间，也不是骨骼空间** |
> | 剑网格在同一空间 | size `(2.04, 2.07, 0.41)`，中心离骨骼 **2.88 米** | 一把 1.6 米的剑不可能这样 |
> | 剑网格自身 AABB 长边 | 2.07 米 | 与 mesh 数据一致 |
> | 4 个候选网格（身体/头盔/盾/剑）距骨骼 | 3.46 ~ 3.87 米 | **全部**离得很远 —— 说明是坐标系问题，不是选错网格 |
>
> **结论**：`bindposes[i].inverse` 并不把顶点搬到骨骼局部空间，我原来推的
> `vertex_bone = bindpose⁻¹ * vertex` 是错的。**"所有候选都离骨骼 3 米以上"这一条是关键证据** ——
> 如果只是选错网格，剑自己应该量得准，但它也不准。
>
> **下一步排查方向**（没做完，留给以后）：
> 按完整蒙皮公式 `world = boneNow.localToWorld × bindpose × vertex` 反推，
> 搬回骨骼 bind 局部空间应该用 `(boneNow.localToWorld × bindpose).inverse` 而不是 `bindpose.inverse`
> —— 两者只差一个骨骼自身变换，正好能解释那 2.88 米的偏移量级。验证方法：在 Play 中把
> 几种候选空间并排量出来，看哪个同时满足「长边 ≈ 1.2~1.8 米」且「中心距原点 ≈ 0.5~1.0 米」。
>
> 代码保留在 `MeleeFighter.TryMeasureWeaponMesh` / `TryMeasureMeshInBoneSpace` 里：
> **候选扫描、防呆检查（点云不可能比网格自身 AABB 还大）、距离判据、尺寸超额保护这四块是对的**，
> 坐标系问题查清后可直接接上。诊断用的 `ContextMenu` 已在收尾时删除。

> **两个踩过的坑（写下来避免复发）**：
>
> 1. **"骨骼出现在网格的 `m_Bones` 里" ≠ "网格是骨骼的子物体"**。
>    `Enemy.prefab` 的层级是 `Paladin WProp J Nordstrom` 下并列着
>    `mixamorig:Hips`（骨骼树）与 `Paladin_J_Nordstrom_Sword`（网格）。
>    `Sword_joint` 是个 `m_Children: []` 的空物体，**从骨骼往下找网格必然落空**。
>    碰撞体挂骨骼是对的（顶点由它驱动），但**找网格必须从模型根横向扫**。
>
> 2. **静默失败比报错危险得多**。原实现找不到命中盒就 return null，
>    自动定尺失败也一声不响 —— 而默认值 `(1.6, 0.1, 0.1)` 恰好"看起来合理"
>    （剑长估得没错），于是三轮排查里没人怀疑它其实从未生效。
>    现在每条失败路径都会打出具体原因。

> **为什么浏览器碰撞体随动画走**：`mixamorig:Sword_joint` 是 **`Paladin_J_Nordstrom_Sword` 网格的蒙皮骨骼**
> （见 `Enemy.prefab` 的 `m_Bones` 列表），剑的顶点由它驱动，所以碰撞体挂在这个骨骼上
> 与挂在剑模型上是同一件事，且天然跟随动画。
> 而名字叫 `Paladin_J_Nordstrom_Sword` 的那个节点是 `SkinnedMeshRenderer` 的宿主，
> 自身不随动画移动 —— 给它加 `BoxCollider` 是错的。

**0.2 实施记录（✅ 代码完成 · 待内容验收）**：

| 改动 | 位置 | 内容 |
|---|---|---|
| 盾标识 | `WeaponConfig.cs` | 新增 `isShield`（默认 `false`） |
| 初始碰撞体状态 | `WeaponManager.ApplyInitialColliderState` | 装备时统一决定：武器 → 全部 collider `enabled = true`；盾 → 全部 `enabled = false` |
| 调用点 | `WeaponManager.EquipWeapon` / `SwitchToSlot` | 两处都调用，避免切槽位时状态残留 |

**验收结果**：⚠️ **部分待验**。代码层已闭环，但**项目里目前没有任何盾的内容**
（无 `ShieldConfig.asset`、无盾 Prefab、`E_WeaponType` 也还没有 `Shield` 值），
所以"装盾走动不误伤"这条**无法在当前状态下复现验收** —— 它要等阶段 1（双槽 + 盾内容）之后才能实测。

在内容到位之前，这条改动的意义是**把隐式约定变成显式规则**：
碰撞体默认是开的，"盾走路误伤"是否发生取决于谁先跑谁后跑；
集中到 `ApplyInitialColliderState` 一处之后，规则可读、可测、可复现。

> **⚠️ 与后续阶段的耦合（阶段 4 必须处理）**：
> 盾的 `Collider` 被本阶段关掉了，但 **`MeleeFighter.EnableHitbox` 目前没有任何分支会把它打开** ——
> 那需要阶段 4.2 的 `E_AttackHitbox.Shield` 枚举值 + `EnableHitbox` 分支 + `MeleeFighter` 持有盾碰撞体引用。
> **在那之前，盾装上后不会造成任何伤害**（这正是本阶段想要的安全状态，但它不是终态）。
>
> 另一处要一并处理的遗留：`MeleeFighter` 只有一个 `WeaponCollider`，
> 而 `WeaponManager.SyncFighterWeapon()` 把 `mainWeaponSlotIndex` 指向的模型交给它。
> 主手槽若装了盾，`WeaponCollider` 就会指向盾 —— 这是阶段 1.3 / 1.4 改双槽时要顺手理清的地方。

**Gate**：阶段 0 未通过，阶段 4 之后的验收全部无法执行。
→ **0.1 已通过**（敌人攻击玩家 → 掉血）；**0.2 待盾内容到位后补验**，不阻塞后续阶段。

### 阶段 1：双槽地基

| 任务 | 产出 |
|---|---|
| 1.1 `E_WeaponType` 加 `Shield`（末尾追加） | `WeaponType.cs` |
| 1.2 `WeaponSlot.allowedTypes` 改数组 | `WeaponSlot.cs` |
| 1.3 `Player.prefab` 加 `J_Bip_L_Forearm` 挂点，两槽改名 RightHand/LeftHand | `Player.prefab` |
| 1.4 `EnsureNotDualEquipped` + 目标槽优先 | `WeaponManager.cs` |
| 1.5 `Item.txt` 编码转 UTF-8 + 订正 WeaponType 列 + 加盾行 | `Item.txt` |

**Gate**：能在 Play 里把剑装到右手、盾装到左手，两个模型同时可见且位置正确。

### 阶段 2：动作集路由骨架

| 任务 | 产出 |
|---|---|
| 2.1 `E_MovesetType` + `MovesetResolver` | 新建两个文件 |
| 2.2 `MovesetConfig` SO 类型 | 新建 |
| 2.3 三个 `MovesetConfig` 资产，**覆盖器先留空** | 3 个 `.asset` |
| 2.4 `MeleeFighter.SetWeaponConfig` 改为走 `MovesetResolver` | `MeleeFighter.cs:196~207` |
| 2.5 `WeaponManager` 空手配置注入 | `WeaponManager.cs` |
| **2.6 🔴 换覆盖器时的「参数快照 → 赋值 → 等一帧 → 回灌」** | `PlayerAnimationSet.cs`（见 §11.6） |
| **2.7 🔴 `CanSwitchMoveset` 准入属性 + 待切换请求暂存** | `WeaponManager.cs`（见 §8.4） |

> **2.6 为什么是必须项而不是优化项**：
> `animator.runtimeAnimatorController = x` 这个赋值动作会让**全部 animator 参数回到默认值**。
> 不处理的表现是——**角色在跑步中换武器，动画瞬间跳回待机**。
> 这是本方案里最容易被漏掉、且一旦漏掉就非常显眼的一处。
> 处理方式是"快照 → 赋值 → `yield return null` → 回灌"三步（对齐 CBTFM 的时序）。

**Gate**：换装备时 Console 打印出正确的 `E_MovesetType`；
**覆盖器留空但链路跑通、不报错**；
**跑步中换武器，动画不跳回待机**（2.6 生效）；
**翻滚中换武器，请求被暂存并在翻滚结束后生效**（2.7 生效）。

> 这一步仍然**不需要任何动画资产**，是对纯逻辑链路的验证。

### 阶段 3：动作集动画落地

| 任务 | 产出 |
|---|---|
| 3.0 Layer1 状态重命名 + 同步所有引用 | `PlayerMove.controller` |
| 3.1 `Unarmed_Override.overrideController` | 新建 |
| 3.2 `OneHandedSword_Override.overrideController` | 新建 |
| 3.3 `SwordAndShield_Override.overrideController` | 新建 |
| 3.4 挂到三个 `MovesetConfig` 上 | 3 个 `.asset` |

**Gate**：空手走路播空手动作；持盾剑走路播盾剑动作；**脚都不打滑**（同一棵 BlendTree）。

### 阶段 4：左手攻击通道

| 任务 | 产出 |
|---|---|
| 4.1 `E_Hand` 参数 + 双连招计数器 | `MeleeFighter.cs` |
| 4.2 `E_AttackHitbox.Shield` + `EnableHitbox` 分支 | `MeleeFighter.cs` |
| 4.3 `LeftAttack` action + `GetLeftAttackInput` | `PlayerActions.inputactions` + `PlayerController.cs` |
| 4.4 盾击三段的 `AttackData`（含判定窗口实测值） | 3 个 SO |
| **4.5 `AttackData` 增加 `impactStrength` / `displacementScale` 曲线字段** | `AttackData.cs`（见 §9.4） |

**Gate**：盾剑状态下左键出剑、V 出盾击，两套连招互不干扰。

### 阶段 5：举盾 + 格挡判定 + 精力

| 任务 | 产出 |
|---|---|
| 5.1 `Block_Start/Loop/End` 接线（先只播动画） | `PlayerMove.controller` + C# |
| 5.2 格挡受击判定 + `guardStaminaCost` | `ParrySystem` / `Health` 链路 |
| 5.3 破防判定 + 硬直 | 新建 |
| 5.4 `Aim` / `Block` 互斥 | `PlayerController.cs` |

**Gate**：举盾时敌人攻击不掉血只掉精力；精力耗尽触发破防硬直。

### 阶段 6：弹反 + 处决

资产现成：`Paired_Attack_ShieldParryStab_Attacker/Victim`。
`MeleeFighter` 已有 `inCounter` 状态与 `PerformCounterAttack` 框架。

---

## 11. 可借鉴与不可借鉴（CBTFM 框架对照）

> 参考实现：`C:\Unity\CBTFM`（第三方战斗框架）。
> 其 `Assets/Combat System/Animators/` 结构与本方案的假设**逐字对应**，
> 是"动作集由覆盖器切换"这一设计的可运行证据。

### 11.1 CBTFM 的实际目录结构（证据）

```
Animators/Base/
  └ Locomotion_Base.controller          ← 唯一基础控制器
Animators/Override/
  ├ Unarmed_Override.overrideController
  ├ SwordAndShield_Override.overrideController
  ├ Bow_Override.overrideController
  └ Mage_Override.overrideController
Scriptable Objects/Weapon Preset/
  ├ Unarmed.asset            m_WeaponTypeTag = Weapon.Type.None
  ├ Sword And Shield.asset   m_WeaponTypeTag = Weapon.Type.SwordAndShield
  ├ Bow.asset
  └ Mage.asset
```

**这是本方案 §3.5 的直接印证**：一个基础控制器 + N 个覆盖器 + 每个动作集一个预设资产。

### 11.2 独立验证：本方案的每一条核心假设都在 CBTFM 里被实现过

| 本方案的主张 | CBTFM 的实现证据 | 结论 |
|---|---|---|
| 基础控制器唯一，动作集用覆盖器 | `Animators/Base/Locomotion_Base.controller`（唯一）+ `Animators/Override/` 下 4 个 `.overrideController` | ✅ 被印证 |
| 覆盖器在编辑器配死，运行时零生成 | 全仓无 `new AnimatorOverrideController(...)`；`SwordAndShield_Override` 的 `m_Clips` 列了 20+ 条 `m_OriginalClip → m_OverrideClip`；其 `m_Controller` 指回同一份 `Locomotion_Base.controller` | ✅ 被印证 |
| 动作集 = f(左手, 右手) | **见 §11.3 —— 最强的单条证据** | ✅ 被印证 |
| 基础控制器只做一次，永不复制 | `AnimatorOverrideController` 只换 clip，状态机结构沿用 | ✅ 被印证 |

### 11.3 决定性证据：CBTFM 里「盾」根本不是独立的类

这是整个分析里最重要的一条发现，**它直接验证了本方案 §3.1 的宪法性设计**。

CBTFM 的 `Shield.prefab` 和 `Sword.prefab` 挂的是**完全相同的脚本 GUID**（同一个 `MeleeWeapon` 组件）。
两者的全部差异只有两个字段：

| 字段 | `Sword.prefab` | `Shield.prefab` |
|---|---|---|
| `m_EquipSocket` | `Right Hand` | `Left Hand` |
| `m_WeaponPreset` | `Sword And Shield.asset` | **`Sword And Shield.asset`（同一个资产）** |

框架里**没有 `Shield` 类、没有 `OffHand` 类**——"盾"纯粹是一个数据约定：
**一把挂到左手 socket 的 `MeleeWeapon` + 一个共用的动作集预设。**

**为什么这件事重要**：这证明了"动作集归属于**组合**，而不是归属于**单件武器**"是可行且经过实践的设计。
盾和剑指向同一个预设资产，正是因为**它们共同决定同一个动作集**。

> **对我们的启示**：
> 这正是 `MovesetResolver.Resolve(右手, 左手)`（§3.1）存在的理由。
> 如果按"每件武器自带一套动画"实现，盾就必须自带一套"盾剑动作"，
> 而剑也必须自带一套"盾剑动作"——**同一套数据存两份，必然漂移**。
>
> CBTFM 的解法是让两把武器共享一个预设；我们的解法更进一步，
> 让预设由组合**推导**出来，连"两把武器都要手动指向同一个资产"这个易错点都消掉了。

### 11.4 借：`WeaponPreset` 把"动作集"做成完整数据包

CBTFM 的 `Sword And Shield.asset` 不只存覆盖器，而是一个完整数据包：

| 字段 | 作用 | 我们是否采纳 |
|---|---|---|
| `m_CurrentMovementOptions.m_Controller`（类型 `AnimatorOverrideController`） | 移动动作集覆盖器 | ✅ 采纳 → `MovesetConfig.locomotionOverride` |
| `m_SyncMovementSpeed`（Walk/Jog/Sprint 三个倍率） | 每个动作集独立的移动速度校准 | ✅ 采纳 → `walkMoveTier` / `runMoveTier` |
| `m_CurrentMovementOptions` 的 5 个 bool 开关 | WalkArc / SprintStart / SprintStop / SprintTurn / SprintArc 动画开关 | ⏸ 我方素材没有这些动画，不采纳 |
| `m_BaseProperties`（`[SerializeReference] List<BaseProperty>`） | **输入 → 能力 绑定表** | ⚠️ 部分采纳（见下） |
| `m_ActivatableAbilities` / `m_ActivatableBuffs` | 换武器 = 换能力池 / Buff 池 | ✅ 采纳（本期只用于轻攻击） |
| `m_WeaponTypeTag`（GameplayTag） | 动作集身份标识 | ⚠️ 简化 → `E_MovesetType` 枚举 |
| `m_EquipAnimation` / `m_UnequipAnimation`（`SO_AnimationData`） | 拔刀/收刀动画 | ✅ 采纳但本期留空（见 §11.5） |
| `m_DropInfo`（丢弃力/速度/贝塞尔曲线/UnityEvent） | 武器掉落表现 | ❌ 本期不采纳（无丢弃功能） |

**关于 `m_BaseProperties`（最重要的借鉴点）**：CBTFM 把"某个按键触发哪个技能"做成了
**预设资产内的数据**，而不是 C# 里的 `if`。整条输入链是：

```
InputAction → InputActionProperty → PlayAbilityProperty → AbilityComponent.TryActivateAbility
```

同一份输入 action，在 `Sword And Shield.asset` 里绑到 `PlayAbilityProperty(剑轻击)`，
在 `Unarmed.asset` 里绑到另一个能力。**换预设 = 换整张输入映射表。**

> **对我们的启示**：换动作集时，**不只是换动画，还换整套攻击招式表**。
> 这正是 `MovesetConfig.rightHandAttacks` / `leftHandAttacks` 存在的原因——
> 招式表挂在 `MovesetConfig` 上，**不是**挂在 `WeaponConfig` 上。
>
> **但我们不采纳它的实现方式**（`[SerializeReference]` 多态属性链 + 属性管理器）：
> 那需要一整套 `PropertyManagerComponent` 基础设施，违反"不引入框架级抽象"的约束（§11.12）。
> 我们用两个 `List<AttackData>` 表达同样的语义，**代码只认"槽位"，不认武器**。

### 11.5 借：Equip/Unequip 是"可为空的资产字段"

CBTFM 的 Avatar Mask 目录：

```
Full Body.mask   Upper Body.mask   Lower Body.mask
Left Arm.mask    Right Arm.mask    Both Arm.mask
```

控制器里有 `Full Body` / `Upper Body` / `Left Arm` / `Right Arm` / `Additive` 等层。
**左手动作与右手动作分属不同层**，可各自独立播放。

我们当前的 Layer1 是一个无遮罩、`weight=1` 的 Override Layer，
攻击会**整体覆盖**上下身。

**借鉴策略（分两步）**：
1. **本期不引入 Mask**——单一 Override Layer 已能满足"攻击时下半身继续走"的手感需求，
   引入 Mask 会显著增加动画机复杂度。
2. **记录为演进方向**：当需要"一边举盾一边挥剑"或"移动中上半身独立动作"时，
   再拆成 `Upper Body.mask` 的层。届时 `SwordAndShield` 的 Block 循环可以走
   独立层，不影响下半身移动。

### 11.6 ⭐ 重要技术细节：换动作集时的「参数快照与回灌」

**这是 CBTFM 最值得抄的一段代码，也是我原方案最大的漏洞。**

`AnimationBase.SetAnimatorControllerCoroutine()` 不是简单地赋值 `runtimeAnimatorController`，
而是走 PlayableGraph 做了一次精心设计的过渡：

1. **切换前**：`SetAnimatorParameters()` —— 把旧 animator 上**所有非曲线驱动的**
   float / int / bool / trigger 参数**快照**进一个 `Dictionary`
2. 等一帧（`WaitForEndOfFrame`）
3. **切换后**：`GetAnimatorParameters()` —— 把快照**回灌**到新的 playable
4. 用临时 `AnimationMixerPlayable(2)` 把「旧 controller playable」与「新 controller playable」
   并起来，**0.25s 内把权重从 0 lerp 到 1**
5. 最后才 `m_Animator.runtimeAnimatorController = animatorController`

**它解决的两个问题**：

| 问题 | 不加这层的表现 |
|---|---|
| 换 controller 瞬间参数被重置为默认值 | `PlayerState` 突然回 1、`MoveSpeed` 回 0 → **角色在跑步中换武器，动画瞬间跳回待机** |
| 换 controller 瞬间状态机位置丢失 | 姿势硬切，没有过渡 |

> **对我的方案的修正**：
> 我在 §3.4 说过"`AnimatorOverrideController` 不重置状态机位置"——**这句只对了一半**。
> override 确实会保留状态机位置（这是它相对换整图的核心优势），
> **但 `animator.runtimeAnimatorController = x` 这个赋值动作本身会让参数回到默认值。**
>
> **必须采取的措施**：换覆盖器前后，把 `PlayerMove.controller` 的全部参数
> （`PlayerState` / `MoveSpeed` / `TurnSpeed` / `JumpSpeed` / `FeetTween`）快照并回灌。
>
> **本期不引入 PlayableGraph**（那是框架级改造，且我方 Animator 没有自定义 Graph）。
> 本期做法：在 `MovesetResolver` 应用覆盖器的方法里做"快照 → 赋值 → 回灌"三步，
> **并额外等一帧**（`yield return null`）再回灌，使时序与 CBTFM 一致。
> 这条已补入 §10 阶段 2 的任务 2.6。

### 11.7 借：用状态门禁做输入锁（不是布尔互斥）

CBTFM 用 GameplayTag 表达角色状态，`Equip` / `Unequip` / `DropWeapon` 的
`CanActivateAbility` 一律要求 **`State == Idle`**。攻击/受击动画期间，
`ANS_SetState`（一个 NotifyState）在动画时间轴上把 State 改成 `Attack` / `HitReaction`，
于是"动作中不能换武器"这条规则**自动成立**，不需要在每个能力里写 `if`。

**三个可借鉴点**：

1. **"能不能做"由当前状态推导，不由"上一步是什么"推导**——又一次单一真相来源
2. **状态切换由动画时间轴驱动**（NotifyState），不是由代码计时器驱动
3. **能力单槽互斥**：`AbilityComponent.m_CurrentActiveAbility` 只有一个，
   新能力**会踢掉**旧能力（不是拒绝）。而 `Equip` 自己额外要求
   `GetCurrentActiveAbility() == null`，实现"装备中不能再装备"

> **对我的方案的修正**：
> 我在 §8.3 只说"输入缓冲"，**没有说清"什么阶段拒绝输入"**。
> 本期做法（不引入 GameplayTag）：
> ```csharp
> // 换动作集的准入条件：与其他"独占动作"互斥
> bool CanSwitchMoveset =>
>     (playerDodge == null || !playerDodge.IsDodging) &&
>     (meleeFighter == null || !meleeFighter.inAction) &&
>     !isHitStunned && !isDead;
> ```
> **关键是这条判断必须是"具名属性"**，而不是散落在各调用点的 `if` 组合。
> 已补入 §10 阶段 2 的任务 2.6。

### 11.8 借：`SO_AnimationData` 的「动画通知」体系 —— 我原方案的落伍之处

CBTFM 的动画时间轴事件**不是 Unity AnimationEvent**，而是一套自己实现的 Notify 系统：

| 组件 | 位置 | 作用 |
|---|---|---|
| `SO_AnimationData` | `Common/Scriptable Objects/` | 承载 clip + `m_Notifies` 列表 + `RootMotionData` + `AvatarMask` |
| `Notify` | `Characters/Notify/Base/Notify.cs` | **瞬时事件**，`[Serializable]` 普通类，带 `m_StartTime` |
| `NotifyState` | `Characters/NotifyState/Base/NotifyState.cs` | **区间事件**，带 `m_StartTime` / `m_EndTime` + `NotifyBegin` / `NotifyTick` / `NotifyEnd` |

派生的通知类型有 30+ 种，和战斗直接相关的：

| 通知 | 作用 | 对应我方的什么 |
|---|---|---|
| `AN_EnableTrace` / `AN_DisableTrace` | 开关命中盒 | `AttackData.ImpactStartTime` / `ImpactEndTime` |
| `ANS_SetState` | 动画期间改角色状态 | 我方没有（见 §11.7） |
| `ANS_BufferInput` | 动画期间开输入缓冲窗口 | 我方没有（见 §8.3） |
| `ANS_MotionWarping` / `ANS_RotateToTarget` / `ANS_LookAtTarget` | 攻击吸附 / 转向 | `PlayerCombat.cs` 里的 Slerp 粗糙版 |
| `AN_SetHItReaction` / `AN_SetKnockback` / `AN_AddImpulse` | 受击表现 | `DamageRouter` 的受击链路 |
| `AN_SetTimeScale` | 该动画的卡肉缩放 | `HitStopManager` |
| `AN_EquipWeapon` | **动画上打点决定"武器什么时候出现在手上"** | `WeaponManager.EquipWeapon` 的瞬时挂载 |
| `AN_EndCurrentActiveAbility` | 动画播完结束能力 | `MeleeFighter.Attack` 协程的 `length` 计时 |

**它相对"代码里写 normalizedTime 硬编码"的具体优势**：

1. **可批量编辑**：30 个攻击的命中窗口要在编辑器里调，AnimationEvent 得逐个打开 clip
2. **可版本化 / 可 diff**：Notify 存在 SO 里，是文本可比的；AnimationEvent 埋在 FBX 导入产物里
3. **支持区间语义**：`NotifyState` 天然表达"从第 3 帧到第 7 帧"，不用配两个时间点
4. **支持多态与 Clone**：`[SerializeReference]` + `SO_AnimationData.Clone()`，能整包复制
5. **降低美术-程序耦合**：美术在动画资产上打点，程序不用改代码

> **对我方的判断（重要）**：
> 我方现在的 `AttackData.ImpactStartTime` / `ImpactEndTime` **就是这套系统的功能子集**，
> 只是存在 SO 上而不是存在动画上。**这个折中在 3~5 个招式的规模下完全够用**，
> 本期**不做** Notify 系统（那是"为了 5 个招式建一套框架"，违反 §2.2 的范围约束）。
>
> **但立即采纳它的一条最小改进**：给 `AttackData` 增加 **`AnimationCurve` 字段**
> （至少给位移和卡肉曲线），因为"手感调优"是**必然反复**的，
> 而曲线在 Inspector 里拖拽调参的成本远低于改代码常量。
> 已补入 §10 阶段 4 的任务 4.5。
>
> **迁移触发条件**（写进 §14 待决问题）：当招式数量超过 **15** 个，
> 或出现"同一个 clip 在不同动作集里需要不同判定窗口"时，再迁移到 Notify 体系。

### 11.9 ⭐ 借给"演进方向"：`RootMotionData` 的逐帧开关

CBTFM 把每次攻击的 root motion 位移做成了**资产里的曲线数据**
（`SO_AnimationData.RootMotionData`，`RootT.x/y/z` 按 `Time` / `NormalizedTime` 两种轴重新采样），
并支持用两条动画曲线 `Disable Root Position` / `Disable Root Rotation` **逐帧开关**位移与朝向。

**我方现状对照**：`PlayerMovement.AnimatorMove()` 直接吃 `Animator.deltaPosition`，
并在 `MeleeFighter` 攻击期间用 `Animator.deltaPosition` 驱动 `CharacterController.Move`。

**差距**：我方无法"让某段攻击在第 30% 处停止位移"，也无法"位移只在前半段生效"。
这类需求在做**突刺、跳劈、盾冲**时必然出现。

> **本期不做**（§2.2），但**记录为演进方向**。
> 触发条件：第一次出现"我想要某段攻击只在前半段有位移"的需求时。

### 11.10 借：用 Avatar Mask 做分层

CBTFM 的 `m_EquipAnimation` / `m_UnequipAnimation` 在
`Sword And Shield.asset` 和 `Unarmed.asset` 里**都是 `{fileID: 0}`（空）**。

这印证了 §3.3 与埃尔登法环的行为：**战斗中换武器不播拔刀/收刀动画**。
字段存在（保留扩展性），但默认留空。

> **对我们**：`MovesetConfig` 不设 `equipAnimName` / `unequipAnimName` 字段。
> 若后续要做拔刀表现，再单独加——**加它不影响架构**。
> 素材现成：`SwordAndShield_Idle_Equip_Sword` / `Idle_Unequip_Sword`。

### 11.11 借：字符串键的 Socket 表（不写死左右手）

CBTFM 的挂点是**任意数量的、以 GameObject 名为键的字典**，不是两个硬编码枚举：

```csharp
// SocketManagerComponent.cs:14
public SerializedDictionary<string, Socket> m_Sockets = new();
// FindAllSocket():77-96 —— GetComponentsInChildren<Socket>(true) 扫描全子树，以 SocketName 为键
```

`PlayerBase.prefab` 实测扫描到 **12 个 socket**，装备相关的三个：
`Right Hand`、`Left Hand`、`Upper Chest`（背挂点）。
武器只声明两个字符串：`m_EquipSocket: Right Hand` / `m_UnequipSocket: Upper Chest`。
`Socket.IsEquipped` 标记让"手"与"背"**互斥**——这就是"收刀入鞘"的实现方式。

> **对我们的判断**：
> 我方案里的 `WeaponSlot[] weaponSlots` 已经是"数组 + 挂点引用"，
> **结构上等价，且更简单**（不需要字符串查表，直接 SerializeField 拖引用）。
> 本期**不改**。
>
> **但有一条必须采纳的警告**：`FindAllSocket` 用 `GetComponentsInChildren<Socket>()`
> **会把武器模型自带的 socket 一起扫进角色表**（`Sword.prefab` 有 3 个、`Shield.prefab` 有 3 个），
> 靠"名字不撞车"维持，很脆。
>
> 我方如果将来引入 socket 概念，**必须过滤掉 `holdPoint` 子树下的对象**：
> ```csharp
> // 只扫描角色骨架下的 socket，排除已挂载的武器模型
> foreach (var s in GetComponentsInChildren<Socket>(true))
>     if (!IsUnderAnyHoldPoint(s.transform)) list.Add(s);
> ```
> 已补入 §12 风险表。

### 11.12 不借：GameplayTag 体系 + 巨型常量表

CBTFM 有完整的 `Common/GameplayTag/` 体系，外加**代码生成**的 `GameplayTagID`
（`public const ulong Xxx = 0x...UL`，全项目 100+ 个标签）。

它解决的问题：状态判定从"布尔字段的排列组合"变成"标签的集合运算"。

**不借的四个理由**：

1. **引入成本高**：需要编辑器工具、代码生成、初始化顺序管理
2. **规模不匹配**：我方状态约 10 个，远未达到需要标签系统的规模
3. **违反现有约定**：我方已有 `GameEvents` 静态事件总线；且约定"不引入第三种单例模式"
4. **它有实际缺陷**：`GameplayTagID` 常量表让 tag 增删**静默落进 `switch` 的 `default` 分支**
   （如 `StateManagerComponent.cs:81-99` 的 `default:` 落到 `Stance.Idle`），
   编译期不报错、运行期行为错——这是很难排查的一类 bug

**替代方案**：显式枚举 + 具名布尔属性，并把它们收敛成**一个**准入门禁属性（见 §11.7）。

> **补充观察（值得引以为戒）**：CBTFM 自己的 `StateManagerComponent.cs:67` 那行
> `SetInteger(HASH_COMBAT_STANCE, 1)`（战斗站姿）**是被注释掉的**——
> 意味着它基础图里的"战斗姿态" `int = 1` **从未被写入过**，"战斗态"在动画上目前是空转的。
>
> 同类的还有：`AbilityComponent` 用 **`AbilityName` 字符串当主键**
> （`AbilityComponent.cs:150-158`，`AddAbility` 去重、`TryActivateAbility` 查找全走字符串），
> 改名或重名会**静默错配**。
>
> **对我们的启示**：`PlayerMove.controller` 里那个未被写入的 `CombatMode` 参数（§3.3）
> 不是我们独有的问题——**"声明了但没人写"的动画参数、以及"用字符串当主键"，
> 是这套架构的两类常见残留**。
> 处理方式：**用枚举/引用代替字符串键，并明确决定不用哪些声明，写进文档**，
> 而不是留着让它们变成未定义的陷阱。

### 11.13 不借：`BaseObject` / `BaseComponent` / `PropertyManager` 对象框架

CBTFM 的 `Core/Object/BaseObject.cs` + `Framework/Property/*` 引入了
类似 Unity DOTS/ECS 的实体-组件-属性模型，配合 `rid:` 序列化引用，
数据驱动程度极高（`Sword And Shield.asset` 里 10 个 `BaseProperties` 全部以引用形式配置）。

**不借的原因**：
1. 它是**框架级替换**，不是功能级借鉴——引入等于重写整个角色层
2. 我方是 GameFramework (GF) 体系的项目，两套框架并存会撕裂架构
3. 调试成本高：数据全在资产引用里，断点追不到
4. 学习收益 >> 项目收益

**但值得学的是它的"数据驱动决策"思路**：CBTFM 的 `Sword And Shield.asset`
里，"按 A 键 → 播能力 X → 用动画数据 Y"整条链是**资产配置**，
C# 代码完全不知道"直剑"这个概念。

> **对我们的启示**：`MovesetConfig` 里的 `rightHandAttacks` / `leftHandAttacks`
> 就是这个思路的**最小可用版本**——数据在资产里，代码只认"槽位"。
> 这是"借鉴"与"照搬"的分界线。

### 11.14 借鉴总结表

| CBTFM 模式 | 位置 | 采纳程度 |
|---|---|---|
| 基础控制器 + N 个 OverrideController | `Animators/Base` + `Animators/Override` | ✅ **完全采纳**（本方案核心） |
| 每个动作集一个 `WeaponPreset` 资产 | `Scriptable Objects/Weapon Preset/` | ✅ **完全采纳** → `MovesetConfig` |
| **盾/剑共用一个预设资产**（盾没有独立的类） | `Shield.prefab` 与 `Sword.prefab` 同指 `Sword And Shield.asset` | ✅ **完全采纳**（§11.3，印证 §3.1 宪法） |
| 动作集内独立的移动速度校准 | `m_SyncMovementSpeed` | ✅ 采纳 → `walkMoveTier` / `runMoveTier` |
| 输入→能力绑定做成资产数据 | `m_BaseProperties`：`InputActionProperty → PlayAbilityProperty` | ✅ 采纳（仅轻攻击层面） |
| Equip/Unequip 为可空字段 | `m_EquipAnimation: {fileID: 0}`（剑盾/弓/空手全空） | ✅ 采纳（留空） |
| **换 controller 时参数快照/回灌** | `AnimationBase.cs:415-462` | ✅ **立即采纳**（§11.6，修正了原方案的漏洞） |
| **用状态门禁做输入锁** | `Equip.CanActivateAbility` 要求 `State == Idle` | ✅ **立即采纳**（§11.7，收敛为准入属性） |
| `AttackData` 增加 `AnimationCurve` | 类比 `BlendData` / `RootMotionData` | ✅ **立即采纳**（§11.8） |
| 字符串键 Socket 表 | `SocketManagerComponent.m_Sockets` | ⚠️ 结构等价，不改；**采纳其"排除武器自身 socket"的警告** |
| 输入缓冲为可观测的独立组件 | `InputBufferComponent`（32 格环形缓冲）+ 专用 UI | ⚠️ 部分采纳（§8.3 最小实现，但须为具名属性） |
| Avatar Mask 分层（左上身/右手） | `Avatar Masks/*.mask`（6 个） | ⏸ 记录为演进方向 |
| 时序 Notify 系统（替代 AnimationEvent） | `SO_AnimationData.m_Notifies` + 30+ 通知类型 | ⏸ 记录为演进方向（**触发条件：招式 > 15 个**） |
| `RootMotionData` 逐帧位移开关 | `SO_AnimationData.cs:43-256` | ⏸ 记录为演进方向 |
| MotionWarping | `MotionWarpingComponent` + `ANS_MotionWarping` | ⏸ 记录为演进方向 |
| `m_DropInfo`（武器掉落表现） | `SO_WeaponPreset.m_DropInfo` | ❌ 本期不采纳（无丢弃功能） |
| GameplayTag 状态体系 + 代码生成常量表 | `Common/GameplayTag/` + `Generator/GameplayTagID.cs` | ❌ 不采纳 |
| `BaseObject` / `Property` 对象框架 | `Core/Object/` + `Framework/Property/` | ❌ 不采纳 |

**不采纳的理由已经在 §11.12 / §11.13 分别写明**，核心是三条判据：

1. **规模不匹配**：为 3 个动作集、5 个招式建标签系统 / Notify 框架，是负债不是资产
2. **框架级替换不是借鉴**：`BaseObject` 体系引入等于重写角色层，且与我方 GF 体系撕裂
3. **有更简单的等价物**：枚举 + 具名属性 + `List<AttackData>` 已能表达同样语义

**判据的分界线**：如果一个模式能用**一个类 + 一个字段**表达，就采纳；
如果需要**一套基础设施 + 编辑器工具**，就记录为演进方向并写明触发条件。

### 11.15 ⏸ 演进方向：socket 扫掠胶囊判定（替代 Trigger + Collider）

**已在 §4.1.1 详述**。核心是"判定不读武器 `Collider`，而是用武器上两个 socket
之间的 `Physics.CapsuleCastAll` 扫掠"。

| 维度 | 我方（Trigger） | CBTFM（扫掠胶囊） |
|---|---|---|
| 快挥穿透 | ❌ 漏判 | ✅ 抗穿透 |
| 需要 Rigidbody | ⚠️ 有历史坑 | ✅ 不需要 |
| 与拾取物理冲突 | ⚠️ 会 | ✅ 不会 |
| 判定形状可调 | 改 Collider 尺寸 | 改一个 `float radius` |
| 盾走路误伤（§4.2） | ⚠️ 存在 | ✅ 不存在 |

**本期不做**，因为它要重写整条判定通道，与动作集系统不属于同一期。
**记录为架构演进方向**，触发条件：判定手感问题（漏判 / 误伤）累计出现 3 次以上，
或开始做"多段攻击 + 精确判定帧"的时候。

### 11.16 ⏸ 演进方向：受击反应数据挂在动画时间轴上

CBTFM 把**受击方的反应**也做成了动画通知：

```
攻击动画的 AN_SetHitReaction（normalizedTime=0）
  → 把 HitReactionData 写进【攻击者】（TraceInfo / TraceRadius / 反应效果列表）
  → 命中时打包成 DamageInfo
  → 受击方 HitReactionComponent.OnReaction → 逐个执行 HitReactionEffect
```

三个我方案里没有的设计点：

1. **受击反应由攻击者的动画携带，不由受击者的代码决定**。
   `AN_SetHitReaction` 是在**攻击动画**上打点，把"这一刀打多疼、造成什么反应"赋给攻击者
2. **`HitReactionEffect` 列表可带状态过滤**：一份配置就能表达
   "打 Idle 扣血 40 / 打 Block 扣架势 25"——效果各自带 `m_StateTags` 白名单
3. **`AN_SetTimeScale` 做单个动画的卡肉**，而不是全局 `Time.timeScale`

> **对我方的启示（重要）**：
> 我方 `AttackData` 里的 `DamageMultiplier` / `PoiseDamage` / `Parryable` / `HitStop*`
> **已经是"这一刀打多疼"的数据**，方向一致。
> 但我们**没有第 2 点**——`Health` 拿到伤害就扣血，没有"打到盾上应该掉精力而不是血"的分支。
>
> **这正是 §9.5 阶段 5 要补的东西**，而且 CBTFM 给了明确的数据形态参考：
> **不是**在 `Health` 里写 `if (isBlocking) ...`，
> **而是**让"伤害的后果"成为一个可配置列表，按受击者状态过滤。
>
> **本期仍用最简形式**（`Health` + 一个 `isBlocking` 分支），
> 因为为一套受击效果建基础设施，在只有玩家一个可格挡单位的规模下不划算。

### 11.17 ⚠️ 不借：连招绕过能力门禁的这个取舍

**这是 CBTFM 里最需要被理解的一个取舍，也是我最不建议照搬的一条。**

CBTFM 的连招路径是：`ANS_BufferInput`（动画窗口）→ `BufferedInputProperty`
→ `PlayAnimationProperty`（**直接播下一个动画**）。

而正常出招路径是：`InputActionProperty` → `AnimationAbilityProperty`
→ `AbilityComponent.TryActivateAbility`（**过门禁**）。

**连招完全绕过了 `AbilityComponent`**，因此不受 tag、冷却、消耗的任何约束
（`SO_AnimationData.cs:353` 的 notify → `AnimationProperty.cs:12-18`）。

它换来的好处是**手感干脆**（连招是"立即取消并接续"，不是"排队等当前招式播完"）。

**但对我方是危险的**，原因很具体：**我方有耐力系统**（`PlayerStamina`）。
如果连招绕过门禁，就会出现：

```
第一刀扣耐力 → 第二刀不扣 → 第三刀不扣 → 玩家无限连招，耐力形同虚设
```

**我方的做法**：连招**必须仍然走 `PlayerStamina.TryConsume`**。
也就是——借它的"窗口化输入缓冲"（时序），
**不借**它的"绕过门禁"（权限）。

> 这是"借鉴"与"照搬"的第二次分界线：
> **它解决的是"手感"，不是"规则"。手感可以抄，规则必须自己定。**

### 11.18 ⚠️ 不要学：`using UnityEditor;` 混进运行时程序集

CBTFM 有 **18 个 Runtime 程序集文件**在 `#if UNITY_EDITOR` **之外**
写了 `using UnityEditor;`（如 `MotionWarpingComponent.cs:2`、`SO_AnimationData.cs:2`、
`Character.cs:4`），而它的 asmdef **没有平台限制**。

后果：**这套代码目前打不出 Player 包**（编译期找不到 `UnityEditor`）。

**对我方的提醒**：我方是**无 asmdef 的单程序集**（全部编译进 `Assembly-CSharp`），
这意味着 `using UnityEditor;` 会**直接导致打包失败**，而不是警告。
现有约定已经处理了这点（`GmCmd` 用 `#if UNITY_EDITOR` 包裹）。

> **本期所有新增 C# 文件必须遵守**：任何引用 `UnityEditor` 的代码
> 一律包在 `#if UNITY_EDITOR ... #endif` 内。
> 已补入 §13 验收总清单。

---

## 12. 风险评估

| 风险 | 等级 | 影响 | 缓解 |
|---|---|---|---|
| **换覆盖器导致 animator 参数重置** | 🔴 高 | 跑步中换武器 → `MoveSpeed`/`PlayerState` 回默认 → **动画瞬间跳回待机**。这是本方案最容易漏、且一旦漏掉非常显眼的问题 | 阶段 2.6：赋值前后"快照 → 赋值 → 等一帧 → 回灌"（§11.6）。Gate 里必须有"跑步中换武器不跳帧"这一条 |
| **盾/剑模型在骨骼上的位置需要手工调** | 🔴 高 | `Sword_and_Shield_Anims` 的盾剑模型换角色后手上不会自动出现，必须用 `holdPoint` 挂载并调整偏移 | 阶段 1 先在 Play 里对着 `Idle_CombatReady` 调挂点，**再**进入阶段 3。**⚠️ 已修正**：原判断"盾是蒙皮在骨骼上的网格"**是错的**，实际是静态刚性道具，见下方 §12.2 |
| **跨骨架动画重定向的手型不匹配** | 🟠 中 | 我方角色用 `J_Bip_*`（Biped），素材是 `Android_SkeletalMesh`。均为 Humanoid 可重定向，但手指/手腕细节可能不贴合 | 阶段 3 Gate 必须目视检查盾牌是否"握在手里"而非"浮在旁边" |
| **Layer1 重命名漏改引用** | 🟠 中 | 重命名后旧名引用会静默失效（`CrossFade` 找不到状态不报错，只是不播） | 阶段 3.0 完成后全局搜索 `Melee_Attack` 确认无残留 |
| **未来引入 socket 概念时扫描到武器自身的挂点** | 🟡 低 | CBTFM 的 `FindAllSocket` 用 `GetComponentsInChildren<Socket>()`，会把武器模型自带的 3 个 socket 扫进角色表，靠名字不撞车维持（§11.11） | 我方本期用 `WeaponSlot[]` 显式引用，**不引入字符串扫描**。若将来引入，必须过滤 `holdPoint` 子树 |
| **`WeaponConfig` 承载了非武器（盾）** | 🟡 低 | 命名与语义不符，新人误读 | 保留类名（改名成本高于收益），在 `CLAUDE.md` 已知边界中登记 |
| **精力/破防推迟导致盾无价值** | 🟡 低 | 阶段 5 之前，盾在玩法上只是"换动画的道具" | 阶段 5 优先级高于阶段 6；中期评审时确认 |
| **`Item.txt` 编码转换引入乱码** | 🟡 低 | GBK → UTF-8 转换错误会破坏现有数据 | 转换后逐行 diff 校对，特别是中文描述列 |
| **`allowedTypes` 改数组后旧 prefab 数据丢失** | 🟡 低 | Unity 序列化字段类型变更会清空原值 | 改完后在 Inspector 里重新勾选，并 Play 验证 |
| **蒙皮网格顶点坐标系未查清** | 🟡 低 | 自动定尺（`autoFitWeaponHitbox`）会选错空间、把命中盒放大到角色体外。**已默认关闭**，不影响手工配置的命中盒 | 排查方向见 §10 阶段 0.1 的"未完成项"。关闭状态下无风险，`autoFitMaxOversizeRatio` 是第二道闸 |
| **盾的碰撞体被关闭后无人再打开** | 🟡 低 | 阶段 4.2 之前盾不会造成伤害 —— 这是阶段 0.2 想要的安全状态，但**不是终态**，容易被误当成"盾击不生效"的 bug | 阶段 4.2 落地 `E_AttackHitbox.Shield` + `EnableHitbox` 分支时一并处理 |
| **盾模型没有可用的预制体** | 🟠 中 | 路线图 §15.1 只列了 `ShieldConfig.asset`，**漏了"盾预制体"**。而项目里只有裸 FBX（无碰撞体），装备链路拿不到可用的 `weaponPrefab` | 阶段 1 顺手补一个盾预制体（含 `BoxCollider`）。见 §12.2 |

### 12.1 玩家资产的真相（v1.3 订正 · 重要）

**本方案全文（含 v1.0）都假设玩家是 `Assets/GameMain/Entities/Player/Player.prefab`。这是错的。**

| 项 | `Player.prefab` | **`Male.prefab`（真正的玩家）** |
|---|---|---|
| 路径 | `Assets/GameMain/Entities/Player/Player.prefab` | `Assets/GameMain/Entities/Player/Male.prefab` |
| 状态 | ❌ **已弃用**，未被任何使用方引用 | ✅ `TestScene` 第 511 行的 PrefabInstance，**场景根对象** |
| 层 / Tag | Player 层 8 / — | **Layer 6（Player）/ Tag `Player`** |
| 骨架 | `J_Bip_*`（Biped，来自 Ada 模型） | **`Skin_1_4.prefab`**（来源：`Assets/Fantasy knight/Prefab/Skin_1_4.prefab`），mixamo 风格命名 `hand_l` / `lowerarm_l` |
| 组件栈 | 不全 | `PlayerController` / `PlayerMovement` / `PlayerAnimator` / **`MeleeFighter`** / `PlayerCombat` / `PlayerLockOn` / **`WeaponManager`** / `RangedFighter` / `ProjectilePool` / `WeaponSwitcher` / `MinimapMarker` / `Health` / `PlayerStamina` / `PlayerDodge` |

**真实骨架的左臂链条**（`Skin_1_4.prefab`，阶段 1.3 建左手挂点用）：

| 骨骼 | Transform fileID | 局部位置（相对父） | 备注 |
|---|---|---|---|
| `clavicle_l` | — | — | |
| `upperarm_l` | — | — | |
| `lowerarm_l`（左小臂） | `8449375469879590680` | `(-0.303, 0.017, 0.012)` 相对 upperarm_l | 文档 §6.3 说的"左手挂点"就在这里 |
| `hand_l`（左手掌） | `4641400885159566759` | `(-0.244, 0.015, 0)` 相对 lowerarm_l | **`WeaponManager` 现有两个槽都指向它**；盾目前挂在这里 |

**真实 `WeaponManager` 配置的缺陷**（与 §6.3 描述的一致，只是路径错了）：

```yaml
weaponSlots:
- slotName: MainHand
  holdPoint: {fileID: 1176333465599525210}   # = Skin_1_4 的 hand_l
  allowedType: 0                             # Sword
- slotName: AxeHand
  holdPoint: {fileID: 1176333465599525210}   # ← 同一个挂点！
  allowedType: 2                             # Axe
mainWeaponSlotIndex: 0
```

> 注意 `holdPoint` 是**跨 prefab 的 stripped Transform**（`m_CorrespondingSourceObject` 指向
> `Skin_1_4.prefab` 的 `4641400885159566759`）。阶段 1.3 新增左手挂点时，
> 同样要用这种 stripped 引用，不能凭名字找。

**盾的现状**（用户已在 `Male.prefab` 上挂好，第 674~771 行）：

| 项 | 值 |
|---|---|
| 挂点 | `hand_l`（Transform `7055462120595353160` → `Skin_1_4` 的 `4641400885159566759`） |
| Position / Rotation / Scale | `(-0.02, -0.033, 0)` / `(71.275, -94.847, -81.633)` / `1` |
| 层 / Tag | `8（Playehitbox）` / `Hitbox` |
| 碰撞体 | 新增 `BoxCollider`（`IsTrigger`），`Size (0.6, 0.6, 0.1)`、`Center (0, 0, 0.05)` |
| Animator | 已移除（`m_RemovedComponents` → `5866666021909216657`），符合 §12.1 的建议 |

> **⚠️ 阶段 0.2 的实测缺口依然存在**：碰撞体目前 `m_Enabled: 1`（开），但因为
> `MeleeFighter.DisableAllHitxboxes()` 与 `WeaponManager.ApplyInitialColliderState` **都不认识
> 这个手工挂上去的盾**（它不走 `EquipWeapon` 链路，也不是 `mainWeaponSlotIndex` 指向的模型），
> 所以**没有任何代码会去关它**。要么在预制体里手工取消勾选，要么等阶段 4.2 把它接入判定通道。

---

### 12.2 盾资产的实际形态（实测，修正了 v1.0 的假设）

v1.0 在风险表里写过"盾剑是**蒙皮在骨骼上**的网格"。**实测证明这句话是错的**，两处证据：

| 证据 | 来源 | 说明 |
|---|---|---|
| `SkelMesh_RoundShield.fbx` 只有 71.8 KB，内部节点仅 `RootNodeL` / `RoundShield_Root` / `MaxHandleS` | FBX 二进制字符串扫描 | 这是 3ds Max 导出的**静态刚性道具**，不是蒙皮网格 |
| 素材包作者自己的挂法：`m_LocalPosition` / `m_LocalRotation` **全为 0** | `SwordandShieldAndroid.prefab:115-207` | 顶点本来就烘焙在那个骨骼的局部空间里，所以挂载时无需调任何数值 |
| 但 FBX 导入设置里有 `SkelMesh_RoundShieldAvatar`（`animationType: 2`） | `.fbx.meta:98` | 导入器硬塞的空 Avatar，**这个道具不需要 Animator**，应移除以免干扰阶段 3 |

> **这对方案的实际影响（重要）**：
>
> 盾的顶点烘焙在**素材包自己骨架**的 hand 骨骼空间里，而玩家用的是 `J_Bip_*`（Biped）骨架。
> 两者原点位置、朝向、尺度都不同 → **"零变换挂在骨骼上"这个模式在我们这里不成立**，
> 必须反过来：**调整盾的 transform 去贴合玩家的骨骼**。
>
> ⚠️ **本节原有的一段玩家骨架数据全部作废**（原写的是 `Player.prefab` 的 `J_Bip_*` 骨架）——
> 那份 prefab **是弃用的旧资产**。正确的玩家资产与骨架见上方 §12.1。
> 这正是"改代码前先确认真实资产路径"的一次教训。


---

## 13. 验收总清单（Play 验证）

全部在 `Assets/GameMain/Scenes/TestScene.unity` 执行。

### 阶段 0
- [x] 敌人攻击玩家 → 玩家血量下降，`GameEvents.OnUnitDamaged` 触发 —— **已验**（每刀 5 点兜底伤害）
- [ ] 玩家站着不动、走动 → 不会因盾的碰撞体误伤敌人 —— **待验**。
      盾现在**已经挂在 `Male.prefab` 的 `hand_l` 上**（`BoxCollider` + IsTrigger + Hitbox 标签 + `Playehitbox(8)` 层），
      但碰撞体 `m_Enabled: 1`（开），且**没有任何代码会关它**（见 §12.1 末的缺口说明）。
      验之前先手工取消该碰撞体的组件启用勾，或等阶段 4.2 接入判定通道。

### 阶段 1
- [x] `E_WeaponType` 末尾追加 `Shield`（值 = 5）—— **已完成**（阶段 1.1，编译通过）
- [ ] 剑装右手、盾装左手 → 两个模型同时可见，位置正确
- [ ] 尝试把同一把剑装到两手 → 右手自动卸下，不出现空引用
- [ ] Console 无 `MissingReferenceException`

### 阶段 2
- [ ] 空手 → Console 打印 `Unarmed`
- [ ] 右手剑 + 左手空 → `OneHandedSword`
- [ ] 右手剑 + 左手盾 → `SwordAndShield`
- [ ] 双盾 / 空手 → `Unarmed`
- [ ] 覆盖器留空状态下无报错、无动作瞬移

### 阶段 3
- [ ] 空手走路 → 空手动作；持盾剑走路 → 盾剑动作
- [ ] **两种状态下走路脚都不打滑**（`Animator.speed` 补偿仍生效）
- [ ] 换装备时 Animator **没有回到 DefaultState**（无姿势跳变）
- [ ] 翻滚在三种动作集下都正常（`PlayerDodge` 时长计算未受影响）
- [ ] Animator 窗口确认 `runtimeAnimatorController` 切换正确

### 阶段 4
- [ ] 盾剑状态：左键 → 剑三段连斩；V → 盾三段连击
- [ ] 两套连招进度互不干扰（右手砍两刀后 V，盾击从第 1 段开始）
- [ ] 空手状态：左键 → 拳；`RightHande` 判定盒生效
- [ ] 盾击命中敌人 → 敌人掉血；盾击判定窗口外 → 不掉血

### 阶段 5
- [ ] 按住右键 → 播 `Block_Loop`；松开 → `Block_End`
- [ ] 举盾时被攻击 → 不掉血，精力下降
- [ ] 精力归零 → 破防硬直
- [ ] 持弓时右键 → 瞄准而非举盾

### 通用
- [x] 所有新增/修改 C# 为 **UTF-8 无 BOM + CRLF** —— **每轮已逐文件校验**（BOM=false / bareLF=0）
- [x] 新代码可编译（`MeleeFighter.cs` / `WeaponConfig.cs` / `WeaponManager.cs` / `WeaponType.cs` 联合编译零错误，
      仅 4 条 `CS1705` netstandard 门面版本噪音）—— 验证方法见下方备注
- [ ] Console 无 mojibake、无 `NullReferenceException`
- [ ] 伤害仍只走 `DamageRouter` 唯一路径
- [ ] **任何 `using UnityEditor;` 都在 `#if UNITY_EDITOR` 内**（§11.18，我方无 asmdef，混入会直接导致打包失败）

> **编译自检方法（本次排查中建立的，可复用）**：
> `dotnet build Assembly-CSharp.csproj` **走不通** —— 这台机器的 .NET 10 SDK 缺两个 workload 目录
> （`MSB4276`），`restore` 直接失败且**不打印任何错误**，很有迷惑性。
> 可用做法是绕过 MSBuild，直接调 Unity 自带的 Roslyn：
>
> ```powershell
> $csc = "C:\Program Files\dotnet\sdk\10.0.300\Roslyn\bincore\csc.dll"
> # 引用集 = SDK 的 ref 包 + Unity 的 Data\Managed\UnityEngine\*.dll + Library\ScriptAssemblies\Assembly-CSharp.dll
> # ⚠️ 不要引用 Data\Managed\UnityEngine.dll（兼容门面），否则每个类型都会 CS0433"同时存在于两个程序集"
> dotnet $csc -nostdlib+ -langversion:9.0 -nowarn:1701,1702,1705,0436,0618 -target:library `
>   -out:check.dll @refs 你的文件.cs
> ```
>
> 判读标准：**只应剩 4 条 `CS1705`**（Unity 程序集要 netstandard 2.1、SDK ref 包给 2.0 的门面差异），
> 任何其他 `error CS` 都是真问题。用"故意加一个重复成员"做过对照，确认这套检查能抓到错。

---

## 14. 待决问题（Open Questions）

| # | 问题 | 影响 | 建议 |
|---|---|---|---|
| Q1 | 双盾时，左手盾能否攻击？ | 决定 `Unarmed` 的 `leftHandAttacks` 是否留空 | 建议**留空**（双手盾 = 纯空手动作，符合"就是拳的动作"的需求原文） |
| Q2 | 单手剑（左手空）时，左手是否有独立动作？ | 同上 | 建议**留空**，左手无攻击 |
| Q3 | 盾的基础伤害是否为 0？ | 决定 `Item.txt` 盾行与 `AttackData` 的伤害来源 | 建议 `BaseDamage = 0`，盾击伤害全部来自 `AttackData.DamageMultiplier` |
| Q4 | 是否需要"装备/卸下"独立按键？ | 当前靠数字键 1/2 切换，语义混淆 | 建议**需要**。`GetPickup_ShopInput` 已占用 E，可考虑 `R` 或 `Tab` |
| Q5 | 空手动作素材是否充足？ | `ARPGWarrior` 包的空手动作数量未盘点 | 阶段 3 开始前先盘点；不足则 `Unarmed_Override` 只覆盖能覆盖的槽位，其余走 §7.3 fallback |
| Q6 | 盾的挂点用左手掌还是左小臂？ | 影响持盾姿势是否自然 | **已有一半答案**：用户已把盾手工挂在 **`hand_l`（左手掌）** 上并调好 `Position (-0.02, -0.033, 0)` / `Rotation (71.275, -94.847, -81.633)`（见 §12.1）。阶段 1.3 需在 Play 里对着 `Block_Loop` 决定：迁到 `lowerarm_l` 重新校准，还是就地沿用左手掌 |
| Q11 | 敌人命中盒修复用哪个方案？ | 直接决定阶段 0 的产出 | ✅ **已决：方案 C**，并已验收（`mixamorig:Sword_joint` 挂 `BoxCollider`）。长期演进方向仍是 **方案 D（socket 扫掠胶囊）**，见 §4.1.1 / §11.15 |
| Q7 | 是否引入 Avatar Mask 分层？ | 决定能否"一边举盾一边移动上半身独立动作" | 本期**不引入**（§11.10），记录为演进方向 |
| Q8 | 何时迁移到时序 Notify 系统？ | 决定"判定窗口"这个数据住在 `AttackData` 还是住在动画资产上 | **触发条件**：招式数量 > **15** 个，或出现"同一个 clip 在不同动作集里需要不同判定窗口"时，迁移到 §11.8 的 Notify 体系 |
| Q9 | 何时需要 `RootMotionData` 式的逐帧位移开关？ | 决定能否做"突刺只在前半段位移" | **触发条件**：第一次出现"我想要某段攻击只在前半段有位移"的需求时。**阶段 4.5 的 `displacementScale` 曲线可先顶一阵** |
| Q10 | `PlayerMove.controller` 的 `CombatMode` 参数是否删除？ | 留着是"声明了但没人写"的残留（CBTFM 也有同样问题，§11.12） | 建议**保留但登记**（删除需改动画机资产）。若阶段 5 的格挡需要区分站姿，再评估启用 |
| Q11 | 敌人命中盒修复用哪个方案？ | 直接决定阶段 0 的产出 | 我倾向 **方案 C**：敌人已有 `mixamorig:Sword_joint` 骨骼（`Enemy.prefab:2309`），把 `BoxCollider` 挂到该骨骼上最省事，且动画驱动时碰撞体跟着走。长期演进方向是 **方案 D（socket 扫掠胶囊）**，见 §4.1.1 / §11.15 |
| Q12 | 是否引入"受击反应按状态过滤"（打盾掉精力而非血）？ | 决定阶段 5 是写 `if` 分支还是做效果列表 | 本期**写 `if` 分支**（只有玩家一个可格挡单位，建列表不划算）。数据形态参考 §11.16，触发条件：出现第二种"受击后果因状态而异"的情况时 |
| Q13 | 卡肉是否需要从全局 `Time.timeScale` 升级为逐动画？ | 决定 `AN_SetTimeScale` 式的单动画卡肉能否实现 | 本期**不做**（`HitStopManager` 是 `timeScale` 唯一所有者，这个约定要守住）。触发条件：出现"某段攻击不该卡肉"的需求时 |

---

## 15. 附录

### 15.1 涉及文件清单

> 状态图例：✅ 已完成 ｜ ⚠️ 部分/待验 ｜ ⬜ 未开始

**新建**

| 文件 | 说明 | 状态 |
|---|---|---|
| `Assets/GameMain/Scripts/Weapon/MovesetType.cs` | `E_MovesetType` 枚举 | ⬜ 阶段 2.1 |
| `Assets/GameMain/Scripts/Weapon/MovesetConfig.cs` | 动作集数据资产 | ⬜ 阶段 2.2 |
| `Assets/GameMain/Scripts/Weapon/MovesetResolver.cs` | 组合 → 动作集 路由（§3.1 宪法） | ⬜ 阶段 2.1 |
| `Assets/GameMain/Scripts/Combat/E_Hand.cs` | 手位枚举（或并入 `MeleeFighter.cs`） | ⬜ 阶段 4.1 |
| `Assets/GameMain/Scripts/Entity/Player/PlayerAnimationSet.cs` | 动作集应用器：快照/回灌 animator 参数、`CanSwitchMoveset` 准入、待切换请求暂存（§11.6 / §8.4） | ⬜ 阶段 2.6 / 2.7 |
| `Assets/Resources/Movesets/UnarmedMoveset.asset` | 动作集资产 | ⬜ 阶段 2.3 |
| `Assets/Resources/Movesets/OneHandedSwordMoveset.asset` | 动作集资产 | ⬜ 阶段 2.3 |
| `Assets/Resources/Movesets/SwordAndShieldMoveset.asset` | 动作集资产 | ⬜ 阶段 2.3 |
| `Assets/GameMain/Scripts/Entity/Player/Animations/Override/Unarmed_Override.overrideController` | 覆盖器 | ⬜ 阶段 3.1 |
| `Assets/GameMain/Scripts/Entity/Player/Animations/Override/OneHandedSword_Override.overrideController` | 覆盖器 | ⬜ 阶段 3.2 |
| `Assets/GameMain/Scripts/Entity/Player/Animations/Override/SwordAndShield_Override.overrideController` | 覆盖器 | ⬜ 阶段 3.3 |
| `Assets/Resources/WeaponConfigs/UnarmedConfig.asset` | 空手占位配置 | ⬜ 阶段 2.5 |
| `Assets/Resources/WeaponConfigs/ShieldConfig.asset` | 盾配置 | ⬜ 阶段 1.5 |
| **盾预制体**（路径待定，如 `Assets/GameMain/Entities/Shield/ShieldPrefab.prefab`） | **⚠️ v1.0 遗漏项**：把 `SkelMesh_RoundShield.fbx` 包一层，含 `BoxCollider` + Hitbox 标签 + `Playehitbox(8)` 层。`WeaponConfig.weaponPrefab` 需要它 | ⬜ 建议并入阶段 1.5 |

**修改**

| 文件 | 改动 | 状态 |
|---|---|---|
| `Assets/GameMain/Scripts/Weapon/WeaponType.cs` | 加 `Shield`（末尾，值 = 5） | ✅ **阶段 1.1** |
| `Assets/GameMain/Scripts/Weapon/WeaponConfig.cs` | 加 6 个字段（`isShield` 已落地，其余 5 个待各阶段） | ⚠️ **1/6** |
| `Assets/GameMain/Scripts/Entity/Player/WeaponSlot.cs` | `allowedType` → `allowedTypes[]` | ⬜ 阶段 1.2 |
| `Assets/GameMain/Scripts/Entity/Player/WeaponManager.cs` | 空手注入、双持约束、目标槽优先；`ApplyInitialColliderState` | ⚠️ 仅碰撞体状态已落地 |
| `Assets/GameMain/Scripts/Combat/MeleeFighter.cs` | 命中盒绑定/查找/Gizmos（已完成）；`SetWeaponConfig` 走路由、双连招、`Shield` 判定、输入缓冲（待做） | ⚠️ 部分 |
| `Assets/GameMain/Scripts/Combat/AttackData.cs` | `E_AttackHitbox` 加 `Shield` + 两条 `AnimationCurve` | ⬜ 阶段 4.2 / 4.5 |
| `Assets/GameMain/Scripts/Entity/Player/PlayerController.cs` | `GetLeftAttackInput`、`Aim`/`Block` 互斥 | ⬜ 阶段 4.3 / 5.4 |
| `Assets/GameMain/Scripts/Entity/Player/Animations/PlayerMove.controller` | Layer1 状态重命名 + 新增槽位 | ⬜ 阶段 3.0 |
| ~~`Assets/GameMain/Entities/Player/Player.prefab`~~ → **`Assets/GameMain/Entities/Player/Male.prefab`** | 加左手挂点、两槽改名、`allowedTypes` 重配。（盾已手工挂上，见 §12.1） | ⬜ 阶段 1.3 |
| `Assets/GameMain/Resources/DataTables/Item.txt` | 编码转 UTF-8、订正 WeaponType、加盾行 | ⬜ 阶段 1.5 |
| `Assets/InputActions/PlayerActions.inputactions` | 加 `LeftAttack` | ⬜ 阶段 4.3 |
| `Assets/GameMain/Entities/Enemy/Enemy.prefab` | 阶段 0 命中盒修复（`Sword_joint` 加 BoxCollider + 标签 + 层） | ✅ **阶段 0.1** |
| `Assets/Art/Models/Characters/Enemy/Paladin WProp J Nordstrom.fbx.meta` | `isReadable: 0 → 1` | ✅ **阶段 0.1** |
| `CLAUDE.md` | 登记已知边界（`WeaponConfig` 承载盾、动作集系统、玩家资产是 `Male.prefab`） | ⬜ |

### 15.2 素材索引

| 用途 | 路径 |
|---|---|
| 盾剑全套动画 | `Assets/Sword_and_Shield_Anims/Art/Animations/`（125 个） |
| 圆盾模型 | `Assets/Sword_and_Shield_Anims/Art/Models/SkelMesh_RoundShield.fbx` |
| 直剑模型 | `Assets/Sword_and_Shield_Anims/Art/Models/SkelMesh_StraightSword.fbx` |
| 成套角色参考 | `Assets/Sword_and_Shield_Anims/Prefabs/SwordandShieldAndroid.prefab` |
| 空手动作 | `Assets/ARPGWarrior/` |
| 敌人动画 | `Assets/GameMain/Scripts/Entity/Enemy/Animations/EnemyController.controller` |
| 参考框架 | `C:\Unity\CBTFM\Assets\Combat System\` |

### 15.3 参考链接

- [Souls Modding Wiki — Adding a Moveset](https://soulsmodding.com/doku.php?id=tutorial:adding-a-moveset&rev=1740532303)
  —— `Weapon Moveset Category` / `Weapon Override Category` 的权威定义
- [vawser/ER-Documentation](https://github.com/vawser/ER-Documentation) —— Elden Ring 文件格式与 TAE 文档
- [The12thAvenger/ERClipGeneratorTool](https://github.com/The12thAvenger/ERClipGeneratorTool) —— `hkbClipGenerator` 编辑器
- [魂3动作文件对应表](https://github.com/zhangxm2312/zhangxm2312.github.io/blob/main/_posts/2022-05-15-%E9%AD%823%E5%8A%A8%E4%BD%9C%E6%96%87%E4%BB%B6%E5%AF%B9%E5%BA%94%E8%A1%A8.md)
  —— 中文动画 ID 对照表（`a0x` 分类编号规律）
- CBTFM 官方文档（Notion）：见 `C:\Unity\CBTFM\Assets\Combat System\Documentation\CST_Documentation.txt`

---

**文档结束**

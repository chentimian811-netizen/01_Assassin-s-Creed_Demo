# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## 项目概述

Unity 2022.3 LTS 第三人称动作游戏 Demo（刺客信条风格）。包含近战连击战斗系统、敌人 AI 状态机、武器/背包系统（含抽卡）、商店系统、完整 UI 框架。

## 构建与运行

在 Unity Editor 2022.3.62f2c1 (LTS) 中打开项目，通过编辑器 Play 模式测试，无 CLI 构建/测试命令。

- **唯一场景：** `Assets/Scenes/SampleScene.unity`
- **解决方案文件：** `01_Assassin's Creed_Demo.sln`、`01_PlayerMove.sln`（gitignore，Unity 自动生成）
- **渲染管线：** URP 14.0.12，质量等级配置在 `Assets/Settings/`（Balanced/HighFidelity/Performant）

## 架构

### 核心模式

- **泛型状态机** (`Assets/Utils/State Machine/`)：`State<T>` / `StateMachine<T>`，用于敌人 AI。状态实现 `Enter(T owner)`、`Execute()`、`Exit()`。
- **单例模式**：`GameManager`、`InventoryManager`、`EnemyManager`、`UIManager`、`PackageLocalData`、`ToastMessage` — 混合使用 MonoBehaviour `Awake()` 和懒加载 C# 单例。全部类无命名空间。
- **ScriptableObject**：`AttackData`（攻击定义）、`WeaponConfig`（武器属性/预制体）、`PackageTables`（静态物品数据库）、`ShopConfig`（商店配置）。通过 Unity 菜单创建（"Combat System/"、"Weapon/"、"Package/"）。
- **事件/委托**：`MeeleFighter.OnGotHit/OnHitComplete`、`InventoryManager.OnItemAdded/OnItemRemoved/OnItemEquipped/OnItemUnequipped`、`WeaponManager.OnWeaponModelChanged`、`CurrencyManager.OnGoldChanged`。
- **数据持久化**：`PackageLocalData` 将背包数据序列化为 JSON 存入 `PlayerPrefs`；`CurrencyManager` 将金币存入 `PlayerPrefs`（key "PlayerGold"，默认 1000）。

### 系统数据流

```
WeaponPickup → [PlayerController 输入] → InventoryManager
  → PackageLocalData (PlayerPrefs JSON)
  → WeaponManager (在 WeaponSlot 挂点实例化预制体)
  → MeeleFighter.SetWeapon (战斗碰撞体)

PlayerController → MeeleFighter (攻击/反击) + EnemyManager (锁定/索敌)

EnemyManager ↔ EnemyController (攻击节奏、警报传播)
  → StateMachine: Idle → CombatMovement → Attack → RetreatAfterAttack
                            ↑ GettingHit ─┘        ↓ Dead

ShopNPC → ShopPanel → ShopManager (购买/出售) + CurrencyManager (金币)

UIManager → BasePanel 面板体系 (从 Resources/Prefabs/Panels/ 按需加载)
```

### 关键模块

| 模块 | 目录 | 核心脚本 |
|------|------|----------|
| 玩家 | `Assets/Player/Scripts/` | `PlayerController.cs` — 输入(Input System)、移动、锁定、攻击 |
| 战斗 | `Assets/Combat/Scripts/` | `MeeleFighter.cs` — 连击、碰撞体切换、反击、血量 |
| 敌人 AI | `Assets/Enemy/Scripts/` | `EnemyController.cs` + `States/` 子目录下的状态类 |
| 敌人协调 | `Assets/Enemy/Scripts/` | `EnemyManager.cs` — 攻击节奏（同时仅一人攻击）、自动索敌 |
| 背包 | `Assets/Inventory/Scripts/` | `InventoryManager.cs`(操作)、`PackageLocalData.cs`(持久化)、`PackageTables.cs`(配置) |
| 武器 | `Assets/Weapon/` | `WeaponPickup.cs`(场景拾取)、`WeaponConfig.cs`(SO配置)、`WeaponManager.cs`(装备/可视化) |
| 商店 | `Assets/Core/Managers/` + `Assets/UI/Resources/Scripts/Shop/` | `ShopManager.cs`(交易逻辑)、`CurrencyManager.cs`(金币)、`ShopPanel.cs`(UI)、`ShopNPC.cs`(场景触发) |
| UI | `Assets/UI/Resources/Scripts/` | `UIManager.cs`(面板生命周期)、`BasePanel.cs`(基类)、`Backpack/`、`Lottery/`、`Shop/` 子目录 |
| 核心管理 | `Assets/Core/Managers/` | `GameManager.cs` — 数据访问层、抽卡逻辑、物品排序 |

### UI 面板系统

面板通过 `UIManager.OpenPanel(name)` 从 `Resources/Prefabs/Panels/` 按需加载。面板名称常量定义在 `UIconst` 中。`BasePanel` 提供 `OpenPanel`/`ClosePanel` 生命周期。`ToastMessage` 是独立的单例 Toast 通知组件。

### 输入系统

使用 Unity Input System (`Assets/InputActions/`)。`PlayerController` 通过 `InputAction.CallbackContext` 注册回调。E 键根据上下文复用于拾取、打开背包和打开商店。

## Layers & Tags

- **Layers**：Player(6)、Enemy(7)、Playehitbox(8)、Enemyhitbox(9)、VersionSensor(10)、Obstacles(11)
- **Tags**：Enemy、Hitbox

## 代码中需注意的命名

- **以下拼写错误是历史遗留，不要"修正"它们**：`MeeleFighter`、`CombaController`、`AttackSates`、`CombatMovmentStates`、`LottertCell`、`VersionSensor`、`tatgetEnemy`、`ReteatAfterAttack`、`E_PlayerPostrue`、`Norml`
- **无命名空间** — 所有类都在全局命名空间中，新增代码也应保持此风格
- **血量硬编码**为 25（`MeeleFighter`），伤害硬编码为 5
- `E_WeaponType` 定义了 5 种武器类型，但只有 Sword 有实际游戏逻辑
- **食物系统**（`PackageTypeFood = 2`）仅定义了常量，未实现功能
- `CombaController.cs` 是空壳/WIP 文件

## 已知 Bug

- `ShopNPC.OnTriggerEnter` 方法名为 `OTriggerEnter`（缺少 'n'），导致触发回调不会触发

## 第三方资源（勿修改）

- `Assets/ThirdParty/URP UniVrm/` — VRM 模型导入（UniGLTF、VRM、VRMShaders）
- `Assets/Blink/` — 角色美术资源
- Cinemachine 2.10.6（通过 Package Manager）
- Unity Input System 1.14.2
- AI Navigation 1.1.7
- URP 14.0.12

---

## 🎯 当前优先级：面试准备 + 项目打磨（30天计划）

> **策略**：Phase 1 收尾 + 消化现有代码 + 面试话术准备
> **Phase 2-4 作为加分项**，时间充裕再做

### 30天时间表

```
Week 1: Phase 1 收尾（项目打磨）
  Day 1-2: 修复编辑器引用（#if UNITY_EDITOR）+ 已知 Bug
  Day 3-4: 完善 UI（背包筛选/翻页）+ 武器动画覆盖接入
  Day 5-6: Item 系统基础（ItemBase + ItemType）
  Day 7:   测试验证 + 提交

Week 2-3: 消化理解（核心任务）
  Day 8-9:   第一层 — 状态机 + PackageLocalData + 枚举
  Day 10:    第二层 — 三个 ScriptableObject
  Day 11-13: 第三层 — MeeleFighter + PlayerController（重点）
  Day 14-16: 第四层 — 6个状态 + EnemyController + EnemyManager
  Day 17:    第五层 — InventoryManager + WeaponManager + UI

Week 4: 面试冲刺
  Day 18-20: 画架构图 + 状态流转图 + 数据流图
  Day 21-23: 默写核心代码片段 + 面试话术练习
  Day 24-26: 模拟面试（自问自答 / 找人模拟）
  Day 27-28: 查漏补缺 + 整理项目亮点
  Day 29-30: 休息调整
```

### 面试复习路径（逐层递进）

> 每层按 **读 → 画图 → 复述 → 默写** 四步走

#### 第一层：基础工具（Day 8-9）

| 文件 | 核心内容 | 面试考点 |
|:-----|:---------|:---------|
| `State.cs` + `StateMachine.cs` | 泛型状态机 | 为什么用泛型？状态切换流程？ |
| `PackageLocalData.cs` | JSON持久化 | uid vs id？为什么用 PlayerPrefs？ |
| `WeaponType.cs` | 枚举定义 | 枚举在系统间如何传递？ |

**面试话术：**
> "我用泛型状态机实现敌人 AI，`State<T>` 让每个状态能直接访问所属对象，不用强转。状态切换时自动调用 Exit/Enter 做清理和初始化。"

#### 第二层：数据配置（Day 10）

| 文件 | 核心内容 | 面试考点 |
|:-----|:---------|:---------|
| `AttackData.cs` | 攻击定义 | 归一化时间 vs 秒？ |
| `WeaponConfig.cs` | 武器配置 | 数据驱动的好处？ |
| `PackageTables.cs` | 物品数据库 | 运行时数据 vs 配置数据分离？ |

**面试话术：**
> "我用 ScriptableObject 做数据配置层，策划可以在编辑器里直接调数值，不用改代码。运行时数据（背包）和配置数据（物品表）分离。"

#### 第三层：核心战斗（Day 11-13）⭐ 最重要

| 文件 | 核心内容 | 面试考点 |
|:-----|:---------|:---------|
| `MeeleFighter.cs` | 连击系统 + 碰撞体 + 反击 | 攻击三阶段？连击怎么实现？反击窗口？ |
| `PlayerController.cs` | 输入 + 移动 + 锁定 | Input System 回调？锁定时移动方向？根运动？ |

**必须掌握的流程图：**
```
MeeleFighter 攻击流程：
ToTryAttack → Attack协程
  → Windup(前摇) → Impact(生效，开碰撞体) → Cooldown(后摇)
  → comboQueued? → 继续下一段

PlayerController 锁定流程：
LockEnemy → 禁用Cinemachine → 覆盖轴向 → 开启高亮
  → 移动方向改为相对敌人计算
```

**面试话术：**
> "战斗系统用协程驱动攻击三阶段：前摇→生效→后摇，只在生效阶段开启碰撞体做判定。连击通过排队标志实现，反击只在敌人第一次攻击的前摇窗口触发，触发后进入过场动画式的对位演出。"

#### 第四层：敌人 AI（Day 14-16）

**状态流转图（必须能画出来）：**
```
┌──────────┐    发现目标    ┌────────────────┐
│   Idle   │ ───────────→ │ CombatMovement  │
└──────────┘              └───────┬────────┘
                                  │
                          ┌───────▼────────┐
                          │     Attack     │
                          └───────┬────────┘
                                  │
                          ┌───────▼────────┐
                          │  RetreatAfter  │
                          │    Attack      │
                          └───────┬────────┘
                                  │
                                  ▼
                          ┌────────────────┐
                          │ CombatMovement │ (循环)
                          └────────────────┘

受击: 任意状态 → GettingHit → CombatMovement
死亡: 任意状态 → Dead → 禁用组件

CombatMovement 子状态：Idle ↔ Chase ↔ Circling（随机切换）
```

| 脚本 | 职责 | 面试考点 |
|:-----|:-----|:---------|
| `EnemyController` | 单个敌人大脑 | 视野检测？警报传播？ |
| `EnemyManager` | 全局攻击调度 | 怎么避免玩家被围殴？ |

**面试话术：**
> "敌人 AI 用泛型状态机管理6个状态，CombatMovement 内部还有子状态机控制追击/环绕/等待的随机切换。EnemyManager 做全局攻击调度，确保同时只有一个敌人攻击，避免玩家被围殴。敌人被击中时通过 OverlapBox 通知附近同伴进入战斗。"

#### 第五层：系统串联（Day 17）

**数据流图（必须能画出来）：**
```
场景拾取：WeaponPickup → InventoryManager → WeaponManager → MeeleFighter
背包操作：PackagePanel → InventoryManager → PackageLocalData (JSON)
商店交易：ShopNPC → ShopPanel → ShopManager → CurrencyManager + InventoryManager
UI面板：  UIManager → Resources加载 → BasePanel生命周期 → panelDict缓存
```

**面试话术：**
> "系统之间通过事件解耦：InventoryManager 发出装备事件，PackageDetail 监听事件刷新 UI。UI 面板通过 UIManager 按需加载，BasePanel 提供统一的生命周期管理。武器拾取流程是：场景触发器 → 弹窗 → 玩家输入 → InventoryManager → WeaponManager 实例化模型 → MeeleFighter 同步战斗组件。"

---

### 面试高频问题清单

| 问题 | 回答要点 |
|:-----|:---------|
| "介绍一下你的项目" | 刺客信条风格 Demo，近战连击+敌人AI状态机+武器背包+抽卡商店 |
| "战斗系统怎么实现的" | 协程驱动三阶段 + 碰撞体开关 + ScriptableObject 数据驱动 |
| "敌人 AI 怎么做的" | 泛型状态机6状态 + EnemyManager 全局攻击调度 |
| "怎么避免玩家被围殴" | EnemyManager 确保同时只有一人攻击，根据 CombatMovementTimer 轮换 |
| "武器系统怎么设计的" | ScriptableObject 配置 + 多槽位装备 + 实例化预制体到挂点 |
| "数据怎么持久化的" | JSON 序列化存 PlayerPrefs，uid 标识实例，id 对应配置表 |
| "UI 怎么管理的" | UIManager 单例 + Resources 按需加载 + BasePanel 生命周期 |
| "系统之间怎么解耦的" | 事件/委托驱动，InventoryManager 发事件，UI 监听刷新 |
| "为什么用泛型状态机" | 避免强转，状态直接访问所属对象，类型安全 |
| "连击系统怎么实现的" | 协程 + comboQueued 标志，Impact 阶段检测输入排队 |

---

### 项目亮点提炼（面试加分项）

1. **数据驱动设计** — ScriptableObject 分离配置和逻辑，策划可直接调数值
2. **事件解耦** — 系统间通过委托通信，低耦合，易扩展
3. **全局攻击调度** — EnemyManager 协调多敌人，避免围殴，提升游戏体验
4. **泛型状态机** — 可复用于玩家/NPC/BOSS，类型安全
5. **完整持久化方案** — JSON + PlayerPrefs，uid/id 分离设计

---

## Phase 1 — 基础完善（项目打磨）

> **目标**：修复已知问题，完善核心系统，让项目更专业
> **优先级**：面试准备 > 项目打磨

### 完成度总览

| 系统 | 完成度 | 状态 |
|:----|:-----:|:-----|
| 玩家移动 | 90% | ✅ 蹲/走/跑/跳/落/着陆 + RootMotion |
| 锁定系统 | 85% | ✅ 锁敌/Cinemachine禁用/自动切换 |
| 近战战斗 | 80% | ✅ 连招/命中盒/受击/反击/死亡 |
| 敌人AI | 85% | ✅ 完整FSM / 检测/追击/环绕/攻击/撤退/受击 |
| 武器拾取 | 80% | ✅ 地面拾取/弹出UI/装备 |
| 武器装备 | 75% | ✅ 多槽位/模型实例化/替换 |
| 背包系统 | 70% | ✅ JSON持久化/增删改查/排序 |
| 商店系统 | 100% | ✅ 已完整实现 |
| 抽卡系统 | 80% | ✅ 随机武器/单抽十连/New标记 |
| UI系统 | 65% | ✅ 面板管理/背包/详情/抽卡/提示 |
| 场景烘焙 | ✅ | ✅ NavMesh已烘焙 |

### 待修复问题

#### 🔴 高优先级 — 空壳/未完成组件

| 文件 | 问题 | 影响 |
|:----|:-----|:------|
| `CombaController.cs` (25行) | **空壳** — 只有 Start() 获取 MeeleFighter，无战斗分发 | 远程/近战切换无法路由 |
| `InventoryManager.cs` | **槽位硬编码** — 写死 `E_WeaponType.Sword` | Bow/Staff 无法被识别为武器类型 |
| `Item/Scripts/` | **空文件夹** — 无 ItemBase 抽象基类 | 物品系统无类型抽象 |
| `Item/ScriptableObjects/` | **空文件夹** | 物品配置无法创建 |
| `Core/Events/` | **空文件夹** | 事件系统待建设 |
| `Core/Singleton/` | **空文件夹** | 单例工具类待建设 |

#### 🔴 高优先级 — UI 功能未完成

| 问题 | 位置 | 说明 |
|:----|:----|:------|
| 背包筛选未实现 | `PackagePanel.cs` | 武器/食物等分类筛选按钮无响应 |
| 背包翻页未实现 | `PackagePanel.cs` | 左右翻页按钮未绑定逻辑 |
| 武器动画覆盖未接入 | `WeaponConfig.animOverride` | SO 已定义但 WeaponManager 未应用到 Animator |

#### 🟡 中优先级 — 编辑器引用（发布构建风险）

| 文件 | 问题代码 | 修复 |
|:----|:---------|:-----|
| `MainPanel.cs` | `EditorApplication.isPlaying` | 包裹 `#if UNITY_EDITOR` |
| `MeeleFighter.cs` | `using UnityEditor.Search` | 包裹 `#if UNITY_EDITOR` 或移除 |
| `EnemyController.cs` | `using UnityEditor.Experimental.GraphView` | 包裹 `#if UNITY_EDITOR` 或移除 |
| `GmCmd.cs` | 全部 API 使用 `[MenuItem]` | 整个文件包裹 `#if UNITY_EDITOR` |

### Phase 1 实现顺序

```
第一阶段：修复已知问题（Day 1-2）
  □ 修复编辑器引用（#if UNITY_EDITOR 包裹）
  □ 修复 ShopNPC.OTriggerEnter 命名

第二阶段：完善 UI（Day 3-4）
  □ 实现背包筛选/翻页功能
  □ 接入武器动画覆盖

第三阶段：Item 系统（Day 5-6）
  □ 创建 ItemBase 抽象基类
  □ 创建 ItemType 枚举
  □ 打通 Item/Scripts/ 和 Item/ScriptableObjects/ 目录
```

### 已实现功能

#### 商店系统 ✅

- `ShopConfig.cs` (~50行) — ScriptableObject 商品清单
- `CurrencyManager.cs` (~60行) — 金币管理单例
- `ShopManager.cs` (~80行) — 商店交易逻辑
- `ShopPanel.cs` (~120行) — 商店 UI 面板
- `ShopCell.cs` (~60行) — 商品格子 UI
- `ShopNPC.cs` (~40行) — 场景交互 NPC

---

## Phase 2-4 — 未来规划（面试加分项，非必须）

> 以下功能作为**加分项**，时间充裕再实现。面试时能讲清楚设计思路即可。

### Phase 2 — 核心玩法

| 功能 | 代码量 | 设计要点 |
|:----|:-----:|:---------|
| 跑酷系统 | ~500行 | 攀爬检测(Raycast+Tag)、跳跃过渡、墙跑、边缘抓取、下落翻滚 |
| 潜行系统 | ~400行 | 警戒值(视野/听觉/距离)、状态切换(未察觉→怀疑→调查→战斗)、草丛隐身、掩体系统 |
| 暗杀系统 | ~300行 | 背后暗杀、高空暗杀、连杀机制 |
| 鹰眼视觉 | ~200行 | 透视高亮、标记追踪、冷却机制 |

### Phase 3 — 场景与内容

- 关卡场景搭建（屋顶、街道、塔楼等可跑酷城市场景）
- 敌人据点（放置巡逻敌人 + 站岗敌人）
- 任务流程设计（目标指引 → 潜行 → 暗杀 → 撤离）

### Phase 4 — 打磨与发布

- HUD（血条/耐力/准星/小地图）
- 音效（脚步/战斗/环境）
- 存档系统
- 开场/主菜单
- 任务系统
- 构建发布

---

## 待实现功能代码量统计

| 阶段 | 功能 | 代码量 | 状态 |
|:----|:----|:-----:|:----|
| Phase 1 | 商店系统 | ~410行 | ✅ 已实现 |
| Phase 1 | Bug修复 + UI完善 | — | 📋 待实现 |
| Phase 1 | Item 系统 | ~100行 | 📋 待实现 |
| Phase 2 | 跑酷系统 | ~500行 | 📋 加分项 |
| Phase 2 | 潜行系统 | ~400行 | 📋 加分项 |
| Phase 2 | 暗杀系统 | ~300行 | 📋 加分项 |
| Phase 2 | 鹰眼视觉 | ~200行 | 📋 加分项 |
| Phase 3 | 场景/关卡 | 美术工作 | 📋 加分项 |
| Phase 4 | HUD/音效/存档等 | 综合 | 📋 加分项 |
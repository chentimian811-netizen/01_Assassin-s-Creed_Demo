# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## 项目概述

Unity 2022.3 LTS 3D 类魂动作游戏（类似《艾尔登法环》）。包含近战战斗系统、敌人 AI 状态机、武器系统、商店系统、完整 UI 框架。

**核心游戏体验**：
- 精确操作的战斗（翻滚 i-Frame + 弹反 + 耐力管理）
- 有挑战的 Boss 战（多阶段 + 差异化机制）
- 死亡与成长的循环（篝火 + 死亡惩罚 + 魂拾回）
- 连通地图探索（捷径解锁 + 区域连接）

## 构建与运行

在 Unity Editor 2022.3.62f2c1 (LTS) 中打开项目，通过编辑器 Play 模式测试。

- **唯一场景：** `Assets/Scenes/MainScene.unity`
- **渲染管线：** URP 14.0.12，质量等级配置在 `Assets/Settings/`

## 开发时间线（7月15日面试）

| 阶段 | 时间 | 内容 | 优先级 |
|------|------|------|--------|
| **阶段1** | 6/28-7/2 | PlayerController拆分 + 翻滚系统 + 耐力系统 | 🔴 必须 |
| **阶段2** | 7/3-7/7 | 弹反/格挡系统 + 受击反馈优化 | 🔴 必须 |
| **阶段3** | 7/8-7/11 | 任务系统(Excel) + 商店优化 + UI/HUD | 🟡 加分 |
| **阶段4** | 7/12-7/15 | 篝火/死亡惩罚原型 + 整体打磨 | 🟡 加分 |

## 架构

### 核心模式

- **泛型状态机** (`Assets/Utils/State Machine/`)：`State<T>` / `StateMachine<T>`，用于敌人 AI。状态实现 `Enter(T owner)`、`Execute()`、`Exit()`。
- **单例模式**：`GameManager`、`InventoryManager`、`EnemyManager`、`UIManager`、`PackageLocalData`、`ToastMessage` — 混合使用 MonoBehaviour `Awake()` 和懒加载 C# 单例。**全部类无命名空间**。
- **ScriptableObject**：`AttackData`（攻击定义）、`WeaponConfig`（武器属性/预制体）、`PackageTables`（静态物品数据库）、`ShopConfig`（商店配置）。通过 Unity 菜单创建（"Combat System/"、"Weapon/"、"Package/"）。
- **事件/委托**：`MeleeFighter.OnGotHit/OnHitComplete`、`InventoryManager.OnItemAdded/OnItemEquipped/OnItemUnequipped`、`WeaponManager.OnWeaponModelChanged`、`CurrencyManager.OnGoldChanged`。
- **数据持久化**：`PackageLocalData` 将背包数据序列化为 JSON 存入 `PlayerPrefs`；`CurrencyManager` 将金币存入 `PlayerPrefs`（key "PlayerGold"，默认 1000）。

### 系统架构

```
Player/
├── PlayerController.cs        ← 核心输入协调器（待拆分）
├── PlayerMovement.cs          ← 移动/跳跃/着陆（待创建）
├── PlayerCombat.cs            ← 攻击输入处理（待创建）
├── PlayerLockOn.cs            ← 锁定系统（待创建）
├── PlayerDodge.cs             ← 翻滚闪避 + i-Frame（待创建）
├── PlayerStamina.cs           ← 耐力管理（待创建）
└── PlayerHealthBar.cs         ← 血量/耐力UI（待创建）

Combat/
├── MeleeFighter.cs            ← 连击/反击（现有，需扩展）
├── ParrySystem.cs             ← 弹反判定 + 格挡窗口（待创建）
├── HitStopManager.cs          ← 集中管理HitStop（待创建）
└── AttackData.cs              ← 攻击数据SO（现有，需扩展）

Enemy/
├── EnemyController.cs         ← 敌人AI核心（现有）
├── EnemyManager.cs            ← 多敌人调度（现有）
├── BossController.cs          ← 多阶段Boss框架（待重构）
└── BossPhaseConfig.cs         ← Boss阶段招式表SO（待创建）

Core/
├── GameManager.cs             ← 游戏管理器（现有，需扩展）
├── CheckpointManager.cs       ← 篝火/存档点管理（待创建）
├── QuestManager.cs            ← 任务管理器（待创建）
└── StaminaConfig.cs           ← 耐力参数SO（待创建）

Level/
├── Bonfire.cs                 ← 篝火交互 + 敌人重生（待创建）
├── ShortcutGate.cs            ← 捷径门（单向解锁）（待创建）
└── FogWall.cs                 ← Boss战雾门（待创建）

Data/
├── QuestData.cs               ← 任务数据结构（待创建）
└── DataSheets/
    ├── QuestData.xlsx         ← 任务总表（待创建）
    └── QuestObjective.xlsx    ← 任务目标子表（待创建）
```

### 事件通信设计

所有新系统通过事件解耦，不侵入现有代码：

```csharp
// 集中定义游戏事件
public static class GameEvents
{
    // 战斗事件
    public static Action<string> OnEnemyKilled;       // 敌人类型ID
    public static Action<string> OnBossKilled;        // Boss ID
    public static Action OnPlayerDeath;
    public static Action OnPlayerRevive;

    // 探索事件
    public static Action<string> OnAreaReached;       // 区域ID
    public static Action<string> OnItemCollected;     // 物品ID
    public static Action<string> OnShortcutUnlocked;  // 捷径ID

    // 任务事件
    public static Action<int> OnQuestCompleted;       // 任务ID
    public static Action<int> OnQuestObjectiveUpdated;// 目标ID
}
```

### 关键模块

| 模块 | 目录 | 核心脚本 |
|------|------|----------|
| 玩家 | `Assets/Player/Scripts/` | `PlayerController.cs` + 拆分后的组件（Movement/Combat/LockOn/Dodge/Stamina） |
| 战斗 | `Assets/Combat/Scripts/` | `MeleeFighter.cs` + `ParrySystem.cs` + `HitStopManager.cs` |
| 敌人 AI | `Assets/Enemy/Scripts/` | `EnemyController.cs` + `States/` 子目录 |
| 敌人协调 | `Assets/Enemy/Scripts/` | `EnemyManager.cs` — 攻击节奏、自动索敌 |
| Boss | `Assets/Enemy/Scripts/` | `BossController.cs` + `BossPhaseConfig.cs`(SO) |
| 背包 | `Assets/Inventory/Scripts/` | `InventoryManager.cs` + `PackageLocalData.cs` + `PackageTables.cs` |
| 武器 | `Assets/Weapon/` | `WeaponPickup.cs` + `WeaponConfig.cs`(SO) + `WeaponManager.cs` |
| 商店 | `Assets/Core/Managers/` + `Assets/UI/Resources/Scripts/Shop/` | `ShopManager.cs` + `CurrencyManager.cs` + `ShopPanel.cs` + `ShopNPC.cs` |
| UI | `Assets/UI/Resources/Scripts/` | `UIManager.cs` + `BasePanel.cs` + `Backpack/` + `Lottery/` + `Shop/` |
| 核心管理 | `Assets/Core/Managers/` | `GameManager.cs` + `CheckpointManager.cs` + `QuestManager.cs` |
| 关卡 | `Assets/Level/Scripts/` | `Bonfire.cs` + `ShortcutGate.cs` + `FogWall.cs` |

### UI 面板系统

面板通过 `UIManager.OpenPanel(name)` 从 `Resources/Prefabs/Panels/` 按需加载。面板名称常量定义在 `UIconst` 中。`BasePanel` 提供 `OpenPanel`/`ClosePanel` 生命周期。`ToastMessage` 是独立的单例 Toast 通知组件。

### 输入系统

使用 Unity Input System (`Assets/InputActions/`)。`PlayerController` 通过 `InputAction.CallbackContext` 注册回调。E 键根据上下文复用于拾取、打开背包和打开商店。

## Layers & Tags

- **Layers**：Player(6)、Enemy(7)、Playehitbox(8)、Enemyhitbox(9)、VisionSensor(10)、Obstacles(11)
- **Tags**：Enemy、Hitbox

## ⚠️ 命名约定（重要）

- **无命名空间** — 所有类都在全局命名空间中，新增代码也应保持此风格
- **血量硬编码**为 25（`MeleeFighter`），伤害硬编码为 5
- `E_WeaponType` 定义了 5 种武器类型，但只有 Sword 有实际游戏逻辑
- **食物系统**（`PackageTypeFood = 2`）仅定义了常量，未实现功能
- **注释规范** — 写代码时必须同时添加中文注释，包括：字段说明、方法功能、关键逻辑节点
- `CombatController.cs` 是空壳/WIP 文件
- **代码提供方式** — Claude 只提供带中文注释的代码文本和逻辑讲解，不直接写入文件；代码写入由用户自己完成

## 第三方资源（勿修改）

- `Assets/ThirdParty/URP UniVrm/` — VRM 模型导入（UniGLTF、VRM、VRMShaders）
- `Assets/Blink/` — 角色美术资源
- Cinemachine 2.10.6（通过 Package Manager）
- Unity Input System 1.14.2
- AI Navigation 1.1.7
- URP 14.0.12

## 项目完成度

### ✅ 已完成（基础系统）

| 系统 | 完成度 | 说明 |
|:----|:-----:|:-----|
| 玩家移动 | 90% | 蹲/走/跑/跳/落/着陆 + RootMotion |
| 锁定系统 | 85% | 锁敌/Cinemachine禁用/自动切换 |
| 近战战斗 | 80% | 连招/命中盒/受击/反击/死亡 |
| 敌人AI | 90% | 完整FSM / 检测/追击/环绕/攻击/撤退/受击 / 巡逻 |
| 巡逻系统 | ✅ | PatrolPoint/PatrolRoute/PatrolState，循环/折返/随机模式 |
| 武器拾取/装备 | 80% | 地面拾取/多槽位/模型实例化/动画覆盖接入 |
| 背包系统 | 85% | JSON持久化/增删改查/排序/筛选/翻页 |
| 商店系统 | 100% | 已完整实现（ShopConfig/ShopManager/ShopPanel/ShopCell/ShopNPC） |
| 抽卡系统 | 80% | 随机武器/单抽十连/New标记 |
| UI系统 | 75% | 面板管理/背包/详情/抽卡/商店/提示 |
| 场景烘焙 | ✅ | NavMesh已烘焙 |
| 编辑器引用修复 | ✅ | 所有 `#if UNITY_EDITOR` 已包裹 |

### 🔄 类魂系统开发（7/15前完成）

| 系统 | 完成度 | 说明 |
|:----|:-----:|:-----|
| PlayerController拆分 | ⏳ 待开始 | 879行拆分为5个组件 |
| 翻滚系统 | ⏳ 待开始 | iFrame + AnimationCurve位移 |
| 耐力系统 | ⏳ 待开始 | StaminaSystem + StaminaConfig(SO) |
| 弹反/格挡 | ⏳ 待开始 | ParrySystem + 格挡窗口判定 |
| 受击反馈 | ⏳ 待开始 | HitStun + 屏幕震动 + 音效 |
| 任务系统 | ⏳ 待开始 | Excel数据驱动 + QuestManager |
| 篝火系统 | ⏳ 待开始 | CheckpointManager + Bonfire |
| 死亡惩罚 | ⏳ 待开始 | 掉魂/拾魂/重生 |
| HUD | ⏳ 待开始 | 耐力条 + Boss血条 + 任务追踪 |

### 📋 未来可扩展

| 功能 | 设计要点 |
|:----|:---------|
| 多阶段Boss | BossPhaseConfig(SO) + 血量阈值切换 |
| 多种敌人 | 盾兵/弓手/狂战士/精英怪 |
| 完整关卡 | 连通地图 + 捷径 + 雾门 |
| 音效系统 | 脚步/战斗/环境音效 |
| 存档系统 | 结构化存档（当前仅PlayerPrefs） |
| 主菜单 | 开场/标题画面 |
| 远程武器 | ProjectileBase/Arrow/Magic/ObjectPool/RangedCombat/AimController |
| 跑酷系统 | 攀爬检测(Raycast+Tag)、跳跃过渡、墙跑、边缘抓取、下落翻滚 |

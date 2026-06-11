# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## 项目概述

Unity 2022.3 LTS 第三人称动作游戏 Demo（刺客信条风格）。包含近战连击战斗系统、敌人 AI 状态机、武器/背包系统（含抽卡）、商店系统、完整 UI 框架。

## 构建与运行

在 Unity Editor 2022.3.62f2c1 (LTS) 中打开项目，通过编辑器 Play 模式测试，无 CLI 构建/测试命令。

- **唯一场景：** `Assets/Scenes/SampleScene.unity`
- **解决方案文件：** `01_Assassin's Creed_Demo.sln`（gitignore，Unity 自动生成）
- **渲染管线：** URP 14.0.12，质量等级配置在 `Assets/Settings/`

## 架构

### 核心模式

- **泛型状态机** (`Assets/Utils/State Machine/`)：`State<T>` / `StateMachine<T>`，用于敌人 AI。状态实现 `Enter(T owner)`、`Execute()`、`Exit()`。
- **单例模式**：`GameManager`、`InventoryManager`、`EnemyManager`、`UIManager`、`PackageLocalData`、`ToastMessage` — 混合使用 MonoBehaviour `Awake()` 和懒加载 C# 单例。**全部类无命名空间**。
- **ScriptableObject**：`AttackData`（攻击定义）、`WeaponConfig`（武器属性/预制体）、`PackageTables`（静态物品数据库）、`ShopConfig`（商店配置）。通过 Unity 菜单创建（"Combat System/"、"Weapon/"、"Package/"）。
- **事件/委托**：`MeleeFighter.OnGotHit/OnHitComplete`、`InventoryManager.OnItemAdded/OnItemEquipped/OnItemUnequipped`、`WeaponManager.OnWeaponModelChanged`、`CurrencyManager.OnGoldChanged`。
- **数据持久化**：`PackageLocalData` 将背包数据序列化为 JSON 存入 `PlayerPrefs`；`CurrencyManager` 将金币存入 `PlayerPrefs`（key "PlayerGold"，默认 1000）。

### 系统数据流

```
WeaponPickup → [PlayerController 输入] → InventoryManager
  → PackageLocalData (PlayerPrefs JSON)
  → WeaponManager (在 WeaponSlot 挂点实例化预制体)
  → MeleeFighter.SetWeapon (战斗碰撞体)

PlayerController → MeleeFighter (攻击/反击) + EnemyManager (锁定/索敌)

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
| 战斗 | `Assets/Combat/Scripts/` | `MeleeFighter.cs` — 连击、碰撞体切换、反击、血量 |
| 敌人 AI | `Assets/Enemy/Scripts/` | `EnemyController.cs` + `States/` 子目录下的状态类 |
| 敌人协调 | `Assets/Enemy/Scripts/` | `EnemyManager.cs` — 攻击节奏（同时仅一人攻击）、自动索敌 |
| 背包 | `Assets/Inventory/Scripts/` | `InventoryManager.cs`(操作)、`PackageLocalData.cs`(持久化)、`PackageTables.cs`(配置) |
| 武器 | `Assets/Weapon/` | `WeaponPickup.cs`(场景拾取)、`WeaponConfig.cs`(SO配置)、`WeaponManager.cs`(装备/可视化) |
| 商店 | `Assets/Core/Managers/` + `Assets/UI/Resources/Scripts/Shop/` | `ShopManager.cs`、`CurrencyManager.cs`、`ShopPanel.cs`、`ShopNPC.cs` |
| UI | `Assets/UI/Resources/Scripts/` | `UIManager.cs`(面板生命周期)、`BasePanel.cs`(基类)、`Backpack/`、`Lottery/`、`Shop/` |
| 核心管理 | `Assets/Core/Managers/` | `GameManager.cs` — 数据访问层、抽卡逻辑、物品排序 |

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

### ✅ 已完成（核心可玩）

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

### 📋 待实现

| 功能 | 设计要点 |
|:----|:---------|
| **Phase 1 清理** | `CombatController.cs` 空壳、`InventoryManager` 槽位硬编码 `Sword`、`Core/Events/` 和 `Core/Singleton/` 空文件夹 |
| 远程武器 | ProjectileBase/Arrow/Magic/ObjectPool/RangedCombat/AimController |
| 跑酷系统 | 攀爬检测(Raycast+Tag)、跳跃过渡、墙跑、边缘抓取、下落翻滚 |
| 潜行系统 | 警戒值(视野/听觉/距离)、状态切换(未察觉→怀疑→调查→战斗)、草丛隐身 |
| 暗杀系统 | 背后暗杀、高空暗杀、连杀机制 |
| 鹰眼视觉 | 透视高亮、标记追踪、冷却机制 |
| HUD | 血条/耐力/准星/小地图 |
| 音效系统 | 脚步/战斗/环境音效 |
| 存档系统 | 通用存档读档（当前仅 PlayerPrefs） |
| 主菜单 | 开场/标题画面 |
| 任务系统 | 任务追踪/目标管理 |

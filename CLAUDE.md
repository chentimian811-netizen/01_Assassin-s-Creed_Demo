# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## 项目概述

Unity 2022.3 LTS 第三人称动作游戏 Demo（刺客信条风格）。包含近战连击战斗系统、敌人 AI 状态机、武器/背包系统（含抽卡）、完整 UI 框架。

## 构建与运行

在 Unity Editor 2022.3.62f2c1 (LTS) 中打开项目，通过编辑器 Play 模式测试，无 CLI 构建/测试命令。

- **解决方案文件：** `01_Assassin's Creed_Demo.sln`、`01_PlayerMove.sln`
- **第三方依赖：** URP UniVrm（VRM 模型支持）、Cinemachine、Unity Input System、AI Navigation

## 架构

### 核心模式

- **泛型状态机** (`Assets/Utils/State Machine/`)：`State<T>` / `StateMachine<T>`，用于敌人 AI。状态实现 `Enter(T owner)`、`Execute()`、`Exit()`。
- **单例模式**：`GameManager`、`InventoryManager`、`EnemyManager`、`UIManager`、`PackageLocalData`、`ToastMessage` — 混合使用 MonoBehaviour `Awake()` 和懒加载 C# 单例。全部类无命名空间。
- **ScriptableObject**：`AttackData`（攻击定义）、`WeaponConfig`（武器属性/预制体）、`PackageTables`（静态物品数据库）。通过 Unity 菜单创建（"Combat System/"、"Weapon/"、"Package/"）。
- **事件/委托**：`MeeleFighter.OnGotHit/OnHitComplete`、`InventoryManager.OnItemAdded/OnItemRemoved/OnItemEquipped/OnItemUnequipped`、`WeaponManager.OnWeaponModelChanged`。
- **数据持久化**：`PackageLocalData` 将背包数据序列化为 JSON 存入 `PlayerPrefs`。

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
| UI | `Assets/UI/Resources/Scripts/` | `UIManager.cs`(面板生命周期)、`BasePanel.cs`(基类)、`Backpack/`和`Lottery/`子目录 |
| 核心管理 | `Assets/Core/Managers/` | `GameManager.cs` — 数据访问层、抽卡逻辑、物品排序 |

### UI 面板系统

面板通过 `UIManager.OpenPanel(name)` 从 `Resources/Prefabs/Panels/` 按需加载。面板名称常量定义在 `UIconst` 中。`BasePanel` 提供 `OpenPanel`/`ClosePanel` 生命周期。`ToastMessage` 是独立的单例 Toast 通知组件。

### 输入系统

使用 Unity Input System (`Assets/InputActions/`)。`PlayerController` 通过 `InputAction.CallbackContext` 注册回调。E 键根据上下文复用于拾取和打开背包。

## 代码中需注意的命名

- **以下拼写错误是历史遗留，不要"修正"它们**：`MeeleFighter`、`CombaController`、`AttackSates`、`CombatMovmentStates`、`LottertCell`、`VersionSensor`、`tatgetEnemy`、`ReteatAfterAttack`、`E_PlayerPostrue`、`Norml`
- **无命名空间** — 所有类都在全局命名空间中
- **血量硬编码**为 25（`MeeleFighter`），伤害硬编码为 5
- `E_WeaponType` 定义了 5 种武器类型，但只有 Sword 有实际游戏逻辑
- **食物系统**（`PackageTypeFood = 2`）仅定义了常量，未实现功能
- `CombaController.cs` 是空壳/WIP 文件

## 第三方资源（勿修改）

- `Assets/ThirdParty/URP UniVrm/` — VRM 模型导入（UniGLTF、VRM、VRMShaders）
- `Assets/Blink/` — 角色美术资源
- Cinemachine（通过 Package Manager）
- Unity Input System 1.14.2
- AI Navigation 1.1.7

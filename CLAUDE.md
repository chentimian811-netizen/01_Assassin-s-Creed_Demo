# CLAUDE.md
## 项目概述

Unity 2022.3 LTS 3D 类魂动作游戏。近战战斗 + 敌人 AI 状态机 + 武器/背包/商店系统。UI 基于 GameFramework（StarForce 版 v2021.05.31）重构中。

## 构建与运行

- **主菜单场景**：`MainMenuScene.unity`（Scene 0，挂 GameFramework.prefab）
- **游戏场景**：`MainScene.unity`（Scene 1，游戏关卡）
- 双场景分离，Play 从 MainMenuScene 启动。GF 组件跨场景 DontDestroyOnLoad。

## 开发时间线（7/15面试）

| 阶段 | 时间 | 内容 |
|------|------|------|
| 阶段1 | 6/28-7/2 | PlayerController 拆分 + 翻滚 + 耐力 |
| 阶段2 | 7/3-7/7 | 弹反/格挡 + 受击反馈 |
| 阶段3 | 7/5-7/11 | **GF UI 框架搭建 + 面板迁移**（原任务系统延后） |
| 阶段4 | 7/12-7/15 | 篝火/死亡惩罚 + HUD 完善 |

## 架构

### 核心模式

- **无命名空间** — 所有类全局命名空间
- **GameFramework** — 20 个子系统，`GameFramework.prefab` 挂 MainMenuScene
- **GameEntry partial** — `Assets/Scripts/GameEntry.Builtin.cs`，静态属性访问 GF 组件
- **Procedure 流程**：`Launch → MainMenu → Loading → Game`，控制场景切换
- **旧 UIManager**（`OpenPanel`+`Resources.Load`）正在被 **GF UIForm**（`OpenUIForm`+`AssetDatabase`）替换
- **注释规范** — 代码添加中文注释（字段说明、方法功能、关键逻辑节点）

### UIForm 面板清单

| UIForm | 分组 | 场景 | 状态 |
|--------|:----:|:----:|:----:|
| MainMenuForm / LoadingForm / MainHUDForm / TopRightTabForm / MainMenuBarForm | Page/Loading/HUD | 跨场景 | ⏳ 新建 |
| SettingsForm / DeathForm / VictoryForm / PauseForm | Popup | 跨场景 | ⏳ 新建 |
| ShopForm / PackageForm / LotteryForm | Popup | 跨场景 | ⏳ 迁移 |
| ToastForm | Top | 跨场景 | ⏳ 迁移 |

UIGroup 深度：HUD(0) → Page(1) → Popup(2) → Top(3) → Loading(4)，Canvas sortingOrder = depth × 10000。

### 关键模块

| 模块 | 目录 | 核心文件 |
|------|------|----------|
| GF 用户层 | `Assets/Scripts/` | `GameEntry.Builtin.cs` + `Procedure/`(5个) + `EventArgs/`(7个) |
| GF UI | `Assets/UI/Scripts/` | `ACUIGroupHelper.cs` + 各 `*Form.cs` |
| 旧 UI | `Assets/UI/Resources/Scripts/` | `UIManager.cs` + `BasePanel.cs`（待替换） |
| 玩家 | `Assets/Player/Scripts/` | `PlayerController.cs` + 拆分中 |
| 战斗 | `Assets/Combat/Scripts/` | `MeleeFighter.cs` + `AttackData.cs` |
| 敌人 AI | `Assets/Enemy/Scripts/` | `EnemyController.cs` + `States/` |
| 背包/商店 | `Assets/Inventory/` + `Core/Managers/` | `InventoryManager` + `PackageLocalData` + `ShopManager` |
| 核心管理 | `Assets/Core/Managers/` | `GameManager.cs` + `CameraManager.cs` + `CurrencyManager.cs` |

### 输入系统

Unity Input System（`Assets/InputActions/`）。E 键复用：拾取 / 背包 / 商店。

### 事件通信

旧系统使用委托（`MeleeFighter.OnGotHit` 等）。GF UI 系统使用框架事件（`Assets/Scripts/EventArgs/`，7 个事件参数类，基于 `ReferencePool`）。

## ⚠️ 命名约定

- **血量硬编码** 25（MeleeFighter），伤害硬编码 5
- 只有 `E_WeaponType.Sword` 有实际游戏逻辑，`PackageTypeFood=2` 仅定义未实现
- `CombatController.cs` 是空壳 WIP
- **Claude 提供代码文本+注释，不直接写入文件**

## 第三方资源（勿修改）

`Assets/ThirdParty/` + `Assets/Blink/` + Cinemachine 2.10.6 + Input System 1.14.2 + AI Navigation 1.1.7 + URP 14.0.12

## 项目完成度

| 系统 | 进度 | 说明 |
|:----|:---:|:------|
| 敌人 AI / 巡逻 | ✅ 90%/100% | 完整 FSM + PatrolPoint/Route/State |
| 玩家移动 | ✅ 90% | 蹲/走/跑/跳/落 + RootMotion |
| 近战战斗 | ✅ 80% | 连招/命中盒/受击/反击 |
| 武器/背包 | ✅ 80%/85% | 拾取/多槽位/模型实例 + JSON持久化/排序/筛选 |
| 商店/抽卡 | ✅ 100%/80% | ShopConfig/Manager/Panel/NPC + 单抽十连/New标记 |
| **GF UI 框架** | **🚧 30%** | 4 个 Procedure + ACUIGroupHelper + 7 个事件参数已完成。UIForm Prefab 和面板逻辑待实现 |
| 旧 UI 系统 | ⏳ 待替换 | UIManager+BasePanel 逐步替换退役 |
| PlayerController 拆分 | ⏳ 待开始 | 879 行→5 组件 |
| 翻滚/耐力/弹反/受击反馈 | ⏳ 待开始 | — |
| 任务/篝火/死亡惩罚/HUD | ⏳ 延后 | 阶段 3 之后 |

---
name: unity-editor-workflow
description: 在 Unity 2022.3.62f2c1 Editor 中运行与验证本项目改动。当需要运行游戏、验证行为、复现 bug、切换或编辑场景、检查 Console 报错、造测试数据、清理存档时使用（本项目没有 CLI 构建与自动化测试，只能通过 Editor 验证）。
---

# Unity Editor 工作流

## 触发条件

- 改动后需要验证行为是否生效
- 复现或排查运行时 bug
- 需要新增/修改场景内容、Prefab、Inspector 引用
- 需要造或清理测试数据（背包、金币）

## 前置条件

- 用 Unity **2022.3.62f2c1** 打开工程根目录（其他版本会触发升级，勿用）
- 唯一场景：`Assets/GameMain/Scenes/TestScene.unity`（EditorBuildSettings 仅此一条）
- 无 CLI 构建、无测试用例、无 CI 配置文件

## 步骤

1. 打开工程 → 打开 `TestScene.unity` → 确认 Console 无编译错误（有红字先清零，否则 Play 无意义）。
2. Play。入口链路：`ProcedureLaunch` → 注册 UIGroup(HUD/Page/Popup/Top/Loading) → 直接 `ChangeState<ProcedureGame>`（**跳过主菜单与 Loading**）→ 打开 `MainHUDForm` → 激活玩家、FreeLook 相机、锁定光标。
3. 造数据（Editor 菜单 `GmCmd/`，仅 `UNITY_EDITOR` 编译）：
   - `GmCmd/读取表格` — 打印 `ItemTable` 全量
   - `GmCmd/创建背包测试数据` — 写入 3 条假背包数据并保存到 PlayerPrefs
   - `GmCmd/读取背包测试数据` — 打印当前背包
   - `GmCmd/打开背包主界面` — `UIManager.OpenPanel(UIconst.PackagePanel)`
4. 清档：删除 PlayerPrefs 键 `PackageLocalData` 与 `PlayerGold`。旧存档会掩盖改动，验证前先清。

## 验证方式

- 行为验证只能在 Play 模式下人工观察；本工程无 Unity Test Framework 用例、无测试 asmdef。
- 编译验证 = Console 无红字。
- `.csproj` / `.sln` 由 Unity 生成，被 gitignore，**不要手改**。

## 失败处理

- 中文注释变乱码（mojibake）→ 该 `.cs` 文件编码被破坏，按 `acdemo-coding-conventions` 重存为 UTF-8 无 BOM。
- 场景加载失败 → 检查 `ScenePaths` 常量是否为 `Assets/` 开头且含 `.unity` 后缀（GF 处于 `EditorResourceMode`）。
- 面板打不开 → 检查 `UIPaths` 常量指向的 prefab 是否真实存在；`UIPaths` 中有一批常量对应的预制体尚未创建。
- `AddUIGroup` 报错 → UIGroup 只在 `ProcedureLaunch` 注册一次，不要在其他 Procedure 重复注册。
- `MissingReferenceException` / 事件被调用两次 → `GameEvents` 有 lambda 订阅或漏退订；关闭 Domain Reload 时尤为明显。

---
name: acdemo-gf-ui-form
description: 在 GameFramework（StarForce 版）UI 体系下新增或迁移界面。当要新建 UIForm、把旧 UIManager/BasePanel 面板迁移到 GF、调整 UIGroup 层级、或打开/关闭某个面板时使用。
---

# GF UIForm 新增 / 迁移

## 触发条件

- 新建 GF 界面（继承 `UIFormLogic`）
- 把 `Assets/UI/Resources/Prefabs/Panels/` 下的旧面板迁到 `Assets/UI/Prefabs/`
- 面板打不开、层级被遮挡、分组报错

## 前置条件

- UIGroup 已在 `ProcedureLaunch` 注册：HUD 0 / Page 1 / Popup 2 / Top 3 / Loading 4
  （`ACUIGroupHelper.DepthFactor = 10000`，depth × 10000 = Canvas `sortingOrder`）
- GF 处于 `EditorResourceMode`：`UIPaths` 中的路径必须是 `Assets/` 开头 + 含 `.prefab` 后缀
- 两套体系并存：旧 `UIManager.OpenPanel(UIconst.X)` + `BasePanel`（Resources 加载）与新 `GameEntry.UI.OpenUIForm(UIPaths.X, "分组")` + `UIFormLogic`。**新代码一律用新体系**

## 步骤

1. 在 `Assets/GameMain/Scripts/UI/UIPaths.cs` 添加常量，按 Page / HUD / Popup / Top / Loading 归入对应注释分组。
2. 预制体放到 `Assets/UI/Prefabs/<分组>/`，命名 `<Name>Form.prefab`，与常量路径逐字一致。
3. 脚本放 `Assets/GameMain/Scripts/UI/` 或对应子目录，继承 `UIFormLogic`。
4. 生命周期只重写 `OnOpen(object userData)` / `OnClose(bool isShutdown, object userData)`；事件订阅与退订必须成对。
5. 打开面板：
   - 常驻：`GameEntry.UI.OpenUIForm(UIPaths.X, "HUD")`（参考 `ProcedureGame.OnEnter`）
   - 由 HUD 按钮触发：`GameEntry.Event.Fire(this, OpenPanelEventArgs.Create(UIPaths.X, "Popup"))`，由 `ProcedureGame.OnOpenPanel` 统一打开（HUD 不直接依赖具体面板）
6. 关闭：用返回的 `formId` 调 `CloseUIForm`，参考 `ProcedureGame.CloseFormIfOpen`。

## 验证方式

- Play 后目标面板出现且层级正确（Popup 压住 HUD，Loading 盖住一切）。
- 重复开关 3 次：Console 无重复订阅警告、无 `MissingReferenceException`。

## 失败处理

- 面板不出现 → 依次检查 `UIPaths` 路径拼写 → prefab 是否真实存在 → 分组名是否与 `ProcedureLaunch` 注册的一致。
- 被别的 UI 挡住 → 检查 `ACUIGroupHelper.DepthFactor` 与分组 depth，不要手改 Canvas `sortingOrder`。
- 关闭后仍收到事件 → `OnClose` 里漏了退订。
- 模态面板导致暂停不解除 → 旧体系的 modal 处理在 `UIManager.ModalPanels` + `HitStopManager.Pause()/Resume()`；迁移时需保留该行为，且**不要直接写 `Time.timeScale`**。

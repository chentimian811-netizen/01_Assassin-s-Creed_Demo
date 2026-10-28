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

- UIGroup 由 `UIGroupBootstrap.EnsureAll()` 在 `ProcedureLaunch` 补齐：HUD 0 / Page 1 / Popup 2 / Top 3 / Loading 4
  （`ACUIGroupHelper.DepthFactor = 10000`，depth × 10000 = Canvas `sortingOrder`。`GameFramework.prefab` 的 Helper 不建 Canvas，勿改它）
- GF 处于 `EditorResourceMode`：`UIPaths` 中的路径必须是 `Assets/` 开头 + 含 `.prefab` 后缀
- **只有一套 UI 生命周期**（GF-1.6 起）：所有界面继承 `GameUIForm`（模态）或 `UIFormLogic`（HUD/Toast），
  打开/关闭**只走 `UIRouter.Open/Close`**。旧 `UIManager.OpenPanel` + `BasePanel` 已删除，不要再引入

## 步骤

1. 在 `Assets/GameMain/Scripts/UI/UIPaths.cs` 添加常量，按 Page / HUD / Popup / Top / Loading 归入对应注释分组。
2. 预制体放到 `Assets/UI/Prefabs/<分组>/`，命名 `<Name>Form.prefab`，与常量路径逐字一致。
3. 脚本放 `Assets/GameMain/Scripts/UI/` 或对应子目录；模态面板继承 `GameUIForm`，只实现 `OnInitForm`（绑按钮，一次）/ `OnOpenForm`（刷数据，每次）/ `OnCloseForm`（退订）。
4. 生命周期约定：GF 会池化实例，`OnInit` 只跑一次，`OnOpen/OnClose` 每次开关都跑；`GameEvents` 订阅放 `OnOpen`、退订放 `OnClose`，禁止 lambda。
5. 打开面板：
   - 模态：`UIRouter.Open(UIPaths.X, "Popup")`（默认 Popup 组；暂停 + 光标由 `GameUIForm` 自动处理）
   - 常驻 HUD：`GameEntry.UI.OpenUIForm(UIPaths.X, "HUD")`（不走路由，参考 `ProcedureGame.OnEnter`）
   - 由 HUD 按钮触发：`GameEntry.Event.Fire(this, OpenPanelEventArgs.Create(UIPaths.X, "Popup"))`，由 `ProcedureGame.OnOpenPanel` 统一打开（HUD 不直接依赖具体面板）
6. 关闭：界面内部用 `CloseSelf()`，外部用 `UIRouter.Close(UIPaths.X)` / `UIRouter.Close(formId)`；**禁止 `Destroy(gameObject)`**。

## 验证方式

- Play 后目标面板出现且层级正确（Popup 压住 HUD，Loading 盖住一切）。
- 重复开关 3 次：Console 无重复订阅警告、无 `MissingReferenceException`。
- 同时开两个模态再逐个关：`GmCmd/UI 状态快照` 里三份计数归零、`timeScale=1`。

## 失败处理

- 面板不出现 → 依次检查 `UIPaths` 路径拼写 → prefab 是否真实存在 → `UIGroupBootstrap.EnsureAll()` 是否跑过。
- 被别的 UI 挡住 → 检查 `ACUIGroupHelper.DepthFactor` 与分组 depth，不要手改 Canvas `sortingOrder`。
- 关闭后仍收到事件 → `OnClose` 里漏了退订。
- 模态面板导致暂停不解除 → 暂停只能由 `GameUIForm` → `UIPauseService.Push/Pop` 计数管理；**不要直接写 `Time.timeScale`**，也不要在界面里手动调 `HitStopManager`。

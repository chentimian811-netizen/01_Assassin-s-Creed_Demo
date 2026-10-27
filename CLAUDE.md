# CLAUDE.md

Unity 2022.3.62f2c1 — 第三人称类魂动作游戏 Demo（刺客信条风格）。
近战/远程战斗 + 敌人 FSM + 武器/背包/商店/抽卡 + GF UI 重构中。

## 环境

- 编辑器：Unity 2022.3.62f2c1。**无 CLI 构建、无测试用例、无 `.github/workflows`、无 Makefile**。
- 唯一可运行场景：`Assets/GameMain/Scenes/TestScene.unity`（EditorBuildSettings 仅此一条）。
- `Assets/Scenes/` 不存在；`ScenePaths.MainMenu` 与 `MainScene` 当前同指向 TestScene。
- `.sln` / `.csproj` 被 gitignore，由 Unity 重新生成。
- 程序集：`Assets/GameMain/Scripts/` 下无 asmdef → 全部编译进 `Assembly-CSharp`。无命名空间。

## 目录

| 路径 | 用途 |
|---|---|
| `Assets/GameMain/Scripts/Base/` | GameEntry.Builtin、GameManager、CameraManager、CurrencyManager、CursorManager、ShopManager、ScenePaths |
| `Assets/GameMain/Scripts/Procedure/` | ProcedureBase / Launch / MainMenu / Loading / Game |
| `Assets/GameMain/Scripts/Core/` | Configs(CharacterConfigIdentity)、Damage(DamageInfo,DamageRouter)、Events(GameEvents)、Interfaces、Log |
| `Assets/GameMain/Scripts/Combat/` | MeleeFighter、RangedFighter、Health、HitStopManager、AttackData、CombatController、Projectile(Pool)、WeaponUpgradeSystem |
| `Assets/GameMain/Scripts/Entity/Player/` | PlayerController + Movement/Combat/LockOn/Animator/WeaponManager/WeaponSlot/WeaponSwitcher + Systems/(PlayerStamina,PlayerDodge,ParrySystem) |
| `Assets/GameMain/Scripts/Entity/Enemy/` | EnemyController、EnemyManager、VisionSensor、Patrol*、States/(7)、Boss/(PhaseController) |
| `Assets/GameMain/Scripts/Inventory/` | InventoryManager、PackageLocalData、PackageSort |
| `Assets/GameMain/Scripts/DataTable/` | DataRepository（含 Luban 查询入口）、TSVParser、DRItem、DRShop |
| `Assets/GameMain/Scripts/Config/Generated/` | **Luban 生成代码**（Tables.cs、各表行类、TbXxx；禁止手写） |
| `Assets/GameMain/Scripts/Editor/` | ConfigExportMenu、ConfigReferenceValidator、ConfigIdentityMigration（仅 Editor 编译） |
| `Assets/GameMain/Resources/DataTables/` | `Shop.txt`（TSV，第一阶段未迁移）+ `Luban/`（生成的 JSON，禁止手写） |
| `Config/` | `Luban/Datas/Gameplay.xlsx`（数值唯一编辑来源）、luban.conf、导出 bat、README、BASELINE |
| `Tools/Luban/` | Luban 5.1.0 命令行工具（版本与校验和见 VERSION.txt） |
| `Assets/GameMain/Scripts/UI/` | Core(UIRouter / GameUIForm / UIPauseService / UICursorService / ToastService / UIFormArgs)、UIPaths、各 `*Form`、Backpack\|HUD\|Lottery\|Pickup\|Shop\|Toast |
| `Assets/GameMain/Scripts/Level/` | Bonfire、FogWall、ShortcutGate、SoulsPickup |
| `Assets/GameMain/Scripts/Utility/` | State Machine(State\<T\>/StateMachine\<T\>)、GmCmd、SkinnedMeshHighlighter |
| `Assets/UI/Prefabs/` | GF UIForm 预制体：`MainMenu/`、`HUD/`、`Loading/`、`Package/`、`Lottery/`、`Shop/`、`Pickup/` |
| `Assets/UI/Resources/Prefabs/Panels/` | 仅剩格子/详情预制体（`PackageUiItem`、`PackageUiItem_Food`、`StartLevel`、`ShopWeapon`、`ShopFood`、`DetailPanel_Weapon`、`DetailPanel_Food`、`LotteryItem`），由各 Form `Resources.Load` 取；GF-5 再改成直接引用并移出 Resources |
| `Assets/GameFramework/` | GF 框架本体 + `GameFramework.prefab`（勿改） |
| `Assets/ThirdParty/`、`Assets/Blink/`、`Assets/Starter Assets/`、`Assets/ARPGWarrior/`、`Assets/Fantasy knight/`、`Assets/Warrior maiden/`、`Assets/Sword_and_Shield_Anims/`、`Assets/TextMesh Pro/` | 导入资源包（勿改） |

## 关键入口

- `GameEntry.Builtin.cs` — `partial GameEntry`，暴露 14 个静态组件属性：Base、Event、Procedure、Scene、UI、Resource、Fsm、Sound、DataTable、Setting、Entity、ObjectPool、Download、WebRequest。
- `ProcedureLaunch` — 注册 UIGroup 后直接 `ChangeState<ProcedureGame>`（**开发期跳过主菜单与 Loading**）。
- `ProcedureMainMenu` / `ProcedureLoading` — 已实现但当前入口不经过。Loading 通过静态 `ProcedureLoading.TargetScene` / `NextProcedureType` 传参。
- `ProcedureGame` — `OpenUIForm(UIPaths.MainHUDForm, "HUD")`，订阅 `MenuCommandEventArgs` / `OpenPanelEventArgs`，激活玩家与 FreeLook 相机。
- `GameManager.Awake()` — 单例 + DontDestroyOnLoad + `DataRepository.Initialize()`。
- `UIRouter` — 界面唯一入口 `Open(path, group, userData)` / `Close(path|id)` / `IsOpen(path)` / `CloseAll()`。界面参数见 `UIFormArgs.cs`。
- `GameEvents` — 玩法静态事件总线。订阅必须 `OnEnable`/`OnOpen` ↔ `OnDisable`/`OnClose` 一一对应，禁止 lambda 订阅，`ResetStatics` 在进入 Play 前清空。
- `HitStopManager` — `Time.timeScale` 的**唯一所有者**；暂停优先，卡肉不覆盖暂停。禁止在别处写 `timeScale`。

## UI（GameFramework）

- **迁移路线与阶段划分见**
  `C:\University\Myself\Github_vault\Timian_vault\毕业设计\开发文档\GF框架迁移与底座搭建计划.md`
  （GF-0 ~ GF-5；GF-0/GF-1 已完成，下一步 GF-2 UI 服务硬化）。动 UI 前先读它，不要凭记忆重推方案。
- UIGroup 深度 = Canvas `sortingOrder`：HUD 0 / Page 1 / Popup 2 / Top 3 / Loading 4；`ACUIGroupHelper.DepthFactor = 10000`。在 `ProcedureLaunch` 注册。
- UIForm 路径常量集中在 `UIPaths`。**已存在**的 GF 预制体：`MainMenuForm`、`LoadingForm`、`MainHUDForm`、`EnemyHealthBarForm`、`PackageForm`、`LotteryForm`、`ToastForm`、`ShopForm`、`PickupForm`。
- 打开/关闭界面统一走 `UIRouter.Open/Close`；模态面板继承 `GameUIForm`（自动锁光标 + 暂停，走 `UIPauseService`/`UICursorService` 计数），不要自己写 `Time.timeScale`，也不要直接 `GameEntry.UI.OpenUIForm`（GF 不查重）。不拦输入的界面（如 `ToastForm`）覆写 `IsModal=false` + `LockCursor=false`。
- **GF 的 `OpenUIForm` 是异步的**：返回 serialId 时界面实例还没创建，当帧 `GameEntry.UI.HasUIForm(...)` 恒为 `false`。所以「是否已打开」只能用 `UIRouter.IsOpen`，禁止拿 GF 状态当同步判据（早期版本这么做 → 每次按键都判定"打开失败"→ 重复开背包且关不掉）。
- `UIPaths` 已声明但**预制体尚未创建**：`TopRightTabForm`、`MainMenuBarForm`、`SettingsForm`、`DeathForm`、`VictoryForm`、`PauseForm`。
- 界面参数用 `UI/Core/UIFormArgs.cs` 里的类型（如 `ShopFormArgs { KeeperId }`）装进 `UIRouter.Open` 的 `userData`，界面在 `OnOpenForm` 里自己取；不要再走「先拿到面板实例再调 OpenWithConfig」两步。
- **只有一套 UI 生命周期**：所有界面继承 `GameUIForm`（或 HUD 的 `UIFormLogic`），由 `UIRouter` 打开/关闭。旧 `BasePanel`/`UIManager` 体系已在 GF-1.6 彻底删除，不要再引入第二套。
- 拿不到界面实例时（池化 + 异步打开）用「静态下单 + 界面轮询版本号」：见 `ToastService`（覆盖策略）与 `PickupForm.ShowPrompt/HidePrompt`（换内容/淡出），不要写「先拿实例再调方法」那套。
- 提示统一用 `ToastService.Show(...)`；旧入口 `ToastMessage.Show(...)` 是它的静态门面（等价，调用点不必改）。

## 配置系统（Luban + SO 混合）

- **数值唯一来源**：`Config/Luban/Datas/Gameplay.xlsx`。改数值只改这张表，然后执行
  Unity 菜单 `ACDemo/Config/Export And Validate`（等价于跑 `Config/Luban/gen_gameplay_json.bat`）。
  第一版**不做热更新**，导出后需重新 Play。
- 生成目录（**禁止手写**，导出会清理并覆盖）：
  - `Assets/GameMain/Scripts/Config/Generated/`：`Tables.cs`、各表行类、`TbXxx`，**无命名空间**
  - `Assets/GameMain/Resources/DataTables/Luban/`：`stamina.json`、`item.json` …

  两个目录里的 `.meta` 也进版本控制；Luban 实测不会删除 `.meta`。
- 运行时统一从 `DataRepository` 取数：`GetCharacter / GetPlayer / GetStamina / GetParry / GetEnemy /
  GetWeapon / GetWeaponUpgrade / GetAttack / GetBossPhases / GetCombatGlobal / GetEnemyGlobal`。
  `Initialize()` 幂等、失败时清空全部表并报错（不留部分可用状态），`SubsystemRegistration` 清静态缓存。
- 角色根对象挂 `CharacterConfigIdentity`（只保存一个字符串配置 ID）：玩家 `player_main`、普通敌人 `enemy_default`。
  场景/Prefab 只允许覆盖「选哪一行配置」的 ID，**不再覆盖具体数值**。
- 工具链：Luban 5.1.0 在 `Tools/Luban/`（来源/SHA256/.NET 前滚说明见 `Tools/Luban/VERSION.txt`）；
  运行时 `Packages/com.code-philosophy.luban`（内嵌包 v1.2.0，不走 UPM git 依赖）。
- 导出脚本带 `--strict`：不加的话 Luban 的范围校验只打印 ERROR、退出码仍是 0，越界值还会写进 JSON。
- 日常流程、加表/加武器/加敌人步骤与排错见 `Config/README.md`；迁移前基准见 `Config/BASELINE.md`。

## 数据 / 持久化

- 数值表：**唯一编辑来源是 `Config/Luban/Datas/Gameplay.xlsx`**（详见下方「配置系统」）。
  `DataRepository.Initialize()` 加载 Luban `Tables`（`Resources/DataTables/Luban/*.json`），
  并提供 `ItemTable` / `ItemByAssetId` / `ItemConfigCache` 等兼容视图。
- 仍走 TSV 的只剩商店：`Resources/DataTables/Shop` → `ShopTable` / `ShopByKeeper`（第一阶段不迁移）。
  `Resources/DataTables/Item.txt` 已删除。
- SO 缓存：`Resources.Load<ItemConfig_SO>($"ItemConfigs/{AssetId}")`。
- 背包：`PackageLocalData` → `PlayerPrefs["PackageLocalData"]`（`JsonUtility` 序列化 `items`；字段 uid/id/num/level/isNew/isEquipped）。
- 货币：`CurrencyManager` → `PlayerPrefs["PlayerGold"]`，`DEFAULT_GOLD = 100000`。
- GM 工具：`GmCmd` 的 Unity 菜单 `GmCmd/`（读取表格、创建/读取背包测试数据、打开背包主界面），仅 `UNITY_EDITOR` 编译。

## 输入

Unity Input System，单文件 `Assets/InputActions/PlayerActions.inputactions`，Action Map `Player`。实测绑定：

| Action | 绑定 | 进入的回调 |
|---|---|---|
| PlayerMove | W/A/S/D（2D Vector） | `GetMoveInput` |
| Look | Mouse delta | — |
| Run | **Shift（双功能）** | `GetRunInput` |
| Crouch | LeftCtrl | `GetCrouchInput` |
| Jump | Space | `GetJumpInInput` |
| LightAttack | 鼠标左键 | `GetLightAttack` |
| Fire | 鼠标左键（与 LightAttack 同键，各自独立 action） | `GetFireInput` |
| Aim | 鼠标右键 | `GetAimInput` |
| Block | 鼠标右键 / V | `GetBlockInput`（**体为空，P3 待接线**） |
| LockEnemy | Q | `GetLockInput` |
| OpenBackpack | `` ` ``（反引号） | `GetBackpackInput` |
| GetPickup_ShopInput | **E** | `GetPickup_ShopInput`（有 NPC 优先开商店，否则拾取） |
| GetShowCursor | LeftAlt / RightAlt | `GetShowCursorInput` |
| Dodge | **空（无绑定）** | `GetDodgeInput`（保留空壳） |

- **Shift 双功能**：短按 → 翻滚 `PlayerDodge.TryDodge()`；长按超阈值 → 疾跑。不再占用独立 Dodge 键。
- 键位定义在 `Player.prefab` 的 `PlayerInput` 组件上，事件按方法名绑定 PlayerController 的回调。

## Layers & Tags

- Layers：Player(6)、Enemy(7)、**Playehitbox(8)**、**Enemyhitbox(9)**（原文拼写如此，勿改）、VisionSensor(10)、Obstacles(11)、Minimap(12)。
- Tags：`Player`、`Enemy`、`Hitbox`。Sorting Layer 仅 `Default`。
- 装备到玩家身上的武器模型会被强制设为 Playehitbox(8)，并销毁残留 `Rigidbody` 与 `WeaponPickup`（见 `WeaponManager.EquipWeapon`）。

## 编码约定

- 无命名空间；新代码同样不加 namespace。
- 中文注释：字段说明 / 方法职责 / 关键逻辑节点。**C# 文件必须存为 UTF-8 无 BOM + CRLF** —— 现存 `ProcedureMainMenu.cs`、`ProcedureLoading.cs`、`ACUIGroupHelper.cs` 等已出现 mojibake，改动时勿扩散。
- 私有字段前缀 `m_`；序列化字段用 `[SerializeField] private` + `[Header]`/`[Tooltip]`。
- 单例两类并存：MonoBehaviour 单例（GameManager、HitStopManager、EnemyManager）与懒加载 C# 单例（CurrencyManager、PackageLocalData）。不要引入第三种。
- SO 通过 Unity 菜单创建，现有前缀：`Combat System/`、`Weapon/`、`Package/`。
- GF 组件快捷访问统一加在 `GameEntry.Builtin.cs` 的 partial 类中。

## 已知边界

- `Health.maxHealth` 来自 `Character` 表（按 `CharacterConfigIdentity` 的配置 ID 解析，玩家 `player_main`=100、普通敌人 `enemy_default`=1000）；
  `DamageRouter.FallbackBaseDamage` 来自 `CombatGlobal` 表，仅在武器 ID 解析不出物品时兜底。
- `E_WeaponType` 定义 5 值（Sword/Dagger/Axe/Bow/Staff）。**已实现配置仅 3 个**：`WeaponConfig.asset`(Sword)、`AxeConfig.asset`(Axe)、`BowConfig.asset`(Bow, `isRanged=1`)。Dagger/Staff 无配置；代码中多处 `?: E_WeaponType.Sword` 兜底。仅有 `Axe.overrideController` 一个动画覆盖器。
- 耐力已接线：`PlayerCombat`（轻攻击）与 `PlayerDodge`（翻滚）都读 `Stamina` 表同一行后调 `PlayerStamina.TryConsume`；
  `heavy_attack_cost` / `parry_fail_cost` / `sprint_cost_per_second` / `block_cost_per_second` / `jump_cost` 仍无调用方。
  缺配置时 `PlayerStamina` / `PlayerCombat` / `PlayerDodge` 会报错并拒绝消耗，不回落默认值。
- 弹反未实现：`ParrySystem.cs` 存在（数值已改读 `Parry` 表）但工程里没有该组件实例，`GetBlockInput` 内仍被注释
  （`// P3：parrySystem?.SetBlocking(...)`）。`Parry` 表里的一行只是「未启用功能的开发初始值」。
- **弓的 weaponID 数据缺陷**：`BowConfig.weaponID = 1111` 在 `Item` 表不存在（对应物品是 10004 Hunting Bow，
  其 `weapon_type` 又被标成 Dagger）。本次迁移按用户决定**不改 ID**，`Weapon` 表保留 1111 一行，
  `ConfigReferenceValidator` 会把它作为【已知缺陷】单列（不计失败）。修复需独立任务。
- Boss 无实例：`BossPhase` 表故意为空，只验证了表结构、分组排序与事件链路，**Boss Play 未覆盖**。
- `PackageTypeFood = 2` 仅定义常量，食物系统未实现。
- `CombatController.cs` 为空壳 WIP：仅 `Awake` 缓存 `MeleeFighter`，`Update` 为空。
- 旧 `BasePanel`/`UIManager`/`UIconst`/`HUDBtn` 已删除；场景 `Canvas` 只剩 `MinimapPanel`，`Canvas/BackPack`、`Canvas/Lottery` 两个旧入口按钮已移除。
- `MainMenuForm.prefab` 之前挂的是旧 `MainMenuPanel` 脚本、从未挂过 `MainMenuForm`（GF-1.6 已换上，字段沿用原引用）；`ProcedureLaunch` 仍直接进 `ProcedureGame`，主菜单链路要到 GF-4 才启用。
- GF Resource 处于 `EditorResourceMode`：路径必须是 `Assets/` 开头且含扩展名（见 `ScenePaths`、`UIPaths` 注释）。
- 敌人**当前没有可用的命中盒**：`Enemy.prefab` 上只有 root 的 `CharacterController` 与 `VisionSensor` 的 trigger 球；招式资产是 `HitboxToUse = Weapon(4)`，而 `MeleeFighter.TryBindPreplacedWeapon()` 找的是名字**完全等于** `Sword` 且带 `BoxCollider` 的子物体（prefab 里叫 `Paladin_J_Nordstrom_Sword`，也没有 BoxCollider）→ `WeaponCollider` 恒为 null，敌人攻击不掉血。
- 勿修改：`Assets/GameFramework/`、`Assets/ThirdParty/`、`Assets/Blink/`、`Assets/Starter Assets/`、`Assets/ARPGWarrior/`、`Assets/Fantasy knight/`、`Assets/Warrior maiden/`、`Assets/Sword_and_Shield_Anims/`、`Assets/TextMesh Pro/`。
- **交付边界：默认提供代码文本 + 中文注释；用户明确要求"直接改 / 修复"时可直接写入项目文件**——写入后必须在回复中列出改动文件与 Play 验证步骤，C# 一律保持 UTF-8 无 BOM + CRLF；涉及伤害/致死通道的改动要遵守"致死只走 `DamageRouter` 唯一路径"（兜底出口见 `Health.ForceKill`）。
- 依赖：URP 14.0.12、Cinemachine 2.10.6、Input System 1.14.2、AI Navigation 1.1.7、TMP 3.0.7、Test Framework 1.1.33（未使用）。

## Skills 索引

| Skill | 用途 | 何时触发 | 路径 |
|---|---|---|---|
| `unity-editor-workflow` | 场景/Play 验证、GmCmd 造数据、Console 排错 | 需要运行、验证、复现 bug、改场景时 | `.claude/skills/unity-editor-workflow/SKILL.md` |
| `acdemo-coding-conventions` | C# 写法、UTF-8 编码、注释、单例、SO 创建 | 新增或修改任何 C# 文件时 | `.claude/skills/acdemo-coding-conventions/SKILL.md` |
| `acdemo-gf-ui-form` | 新增/迁移 UIForm、UIGroup、面板打开链路 | 做 UI 面板、迁移旧面板、开关面板时 | `.claude/skills/acdemo-gf-ui-form/SKILL.md` |
| `acdemo-combat-damage` | 伤害通道、UnitId 定向事件、卡肉/暂停 | 改战斗、血量、受击、弹反、命中时 | `.claude/skills/acdemo-combat-damage/SKILL.md` |

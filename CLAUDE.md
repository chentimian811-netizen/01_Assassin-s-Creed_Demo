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
| `Assets/GameMain/Scripts/Core/` | Configs(Stamina,Parry)、Damage(DamageInfo,DamageRouter)、Events(GameEvents)、Interfaces、Log |
| `Assets/GameMain/Scripts/Combat/` | MeleeFighter、RangedFighter、Health、HitStopManager、AttackData、CombatController、Projectile(Pool)、WeaponUpgradeSystem |
| `Assets/GameMain/Scripts/Entity/Player/` | PlayerController + Movement/Combat/LockOn/Animator/WeaponManager/WeaponSlot/WeaponSwitcher + Systems/(PlayerStamina,PlayerDodge,ParrySystem) |
| `Assets/GameMain/Scripts/Entity/Enemy/` | EnemyController、EnemyManager、VisionSensor、Patrol*、States/(7)、Boss/(PhaseConfig,PhaseController) |
| `Assets/GameMain/Scripts/Inventory/` | InventoryManager、PackageLocalData、PackageSort |
| `Assets/GameMain/Scripts/DataTable/` | DataRepository、TSVParser、DRItem、DRShop |
| `Assets/GameMain/Scripts/UI/` | UIManager(BasePanel 旧体系)、UIPaths、ACUIGroupHelper、*Form、Backpack|HUD|Lottery|MainMenu|Shop |
| `Assets/GameMain/Scripts/Level/` | Bonfire、FogWall、ShortcutGate、SoulsPickup |
| `Assets/GameMain/Scripts/Utility/` | State Machine(State\<T\>/StateMachine\<T\>)、GmCmd、SkinnedMeshHighlighter |
| `Assets/GameMain/Resources/DataTables/` | `Item.txt`、`Shop.txt`（TSV，前两行为注释行与类型行） |
| `Assets/UI/Prefabs/` | GF UIForm 预制体（MainMenu/ HUD/ Loading/） |
| `Assets/UI/Resources/Prefabs/Panels/` | 旧体系面板（MainPanel、Package/、Shop/、Lottery/…） |
| `Assets/GameFramework/` | GF 框架本体 + `GameFramework.prefab`（勿改） |
| `Assets/ThirdParty/`、`Assets/Blink/`、`Assets/Starter Assets/`、`Assets/ARPGWarrior/`、`Assets/Fantasy knight/`、`Assets/Warrior maiden/`、`Assets/Sword_and_Shield_Anims/`、`Assets/TextMesh Pro/` | 导入资源包（勿改） |

## 关键入口

- `GameEntry.Builtin.cs` — `partial GameEntry`，暴露 14 个静态组件属性：Base、Event、Procedure、Scene、UI、Resource、Fsm、Sound、DataTable、Setting、Entity、ObjectPool、Download、WebRequest。
- `ProcedureLaunch` — 注册 UIGroup 后直接 `ChangeState<ProcedureGame>`（**开发期跳过主菜单与 Loading**）。
- `ProcedureMainMenu` / `ProcedureLoading` — 已实现但当前入口不经过。Loading 通过静态 `ProcedureLoading.TargetScene` / `NextProcedureType` 传参。
- `ProcedureGame` — `OpenUIForm(UIPaths.MainHUDForm, "HUD")`，订阅 `MenuCommandEventArgs` / `OpenPanelEventArgs`，激活玩家与 FreeLook 相机。
- `GameManager.Awake()` — 单例 + DontDestroyOnLoad + `DataRepository.Initialize()`。
- `UIManager.Instance` — 旧面板入口 `OpenPanel(name)`，面板名常量在 `UIconst`。
- `GameEvents` — 玩法静态事件总线。订阅必须 `OnEnable`/`OnOpen` ↔ `OnDisable`/`OnClose` 一一对应，禁止 lambda 订阅，`ResetStatics` 在进入 Play 前清空。
- `HitStopManager` — `Time.timeScale` 的**唯一所有者**；暂停优先，卡肉不覆盖暂停。禁止在别处写 `timeScale`。

## UI（GameFramework）

- UIGroup 深度 = Canvas `sortingOrder`：HUD 0 / Page 1 / Popup 2 / Top 3 / Loading 4；`ACUIGroupHelper.DepthFactor = 10000`。在 `ProcedureLaunch` 注册。
- UIForm 路径常量集中在 `UIPaths`。**已存在**的 GF 预制体仅 3 个：`MainMenuForm`、`LoadingForm`、`MainHUDForm`。
- `UIPaths` 已声明但**预制体尚未创建**：`TopRightTabForm`、`MainMenuBarForm`、`ShopForm`、`PackageForm`、`LotteryForm`、`SettingsForm`、`DeathForm`、`VictoryForm`、`PauseForm`、`ToastForm`。
- 两套体系并存：旧面板继承 `BasePanel` + `UIManager.OpenPanel`；新面板继承 `UIFormLogic` + `GameEntry.UI.OpenUIForm`。新代码一律用后者。

## 数据 / 持久化

- TSV 表：`Resources/DataTables/Item`、`Resources/DataTables/Shop` → `DataRepository.Initialize()` 解析为 `ItemTable` / `ShopTable` / `ShopByKeeper` / `ItemByAssetId` / `ItemConfigCache`。
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
- 单例两类并存：MonoBehaviour 单例（GameManager、HitStopManager、EnemyManager）与懒加载 C# 单例（UIManager、CurrencyManager、PackageLocalData）。不要引入第三种。
- SO 通过 Unity 菜单创建，现有前缀：`Combat System/`、`Weapon/`、`Package/`。
- GF 组件快捷访问统一加在 `GameEntry.Builtin.cs` 的 partial 类中。

## 已知边界

- `Health.maxHealth` 默认 25；`DamageRouter.FallbackBaseDamage = 5` 仅在伤害无法解析时兜底。
- `E_WeaponType` 定义 5 值（Sword/Dagger/Axe/Bow/Staff）。**已实现配置仅 3 个**：`WeaponConfig.asset`(Sword)、`AxeConfig.asset`(Axe)、`BowConfig.asset`(Bow, `isRanged=1`)。Dagger/Staff 无配置；代码中多处 `?: E_WeaponType.Sword` 兜底。仅有 `Axe.overrideController` 一个动画覆盖器。
- 耐力已接线：`PlayerCombat`（轻攻击）与 `PlayerDodge`（翻滚，`staminaConfig.dodgeCost`）调用 `PlayerStamina.TryConsume`；`StaminaConfig.heavyAttackCost` 暂无调用方。
- 弹反未实现：`ParrySystem.cs` 存在但 `GetBlockInput` 内被注释（`// P3：parrySystem?.SetBlocking(...)`）。
- `PackageTypeFood = 2` 仅定义常量，食物系统未实现。
- `CombatController.cs` 为空壳 WIP：仅 `Awake` 缓存 `MeleeFighter`，`Update` 为空。
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

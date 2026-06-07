# AGENTS.md

Unity 2022.3 LTS (2022.3.62f2c1) — 第三人称动作游戏 Demo（刺客信条风格）。

## Build & Run

- **无 CLI 构建/测试命令**。在 Unity Editor 中打开项目，Play 模式测试。
- 唯一场景：`Assets/Scenes/SampleScene.unity`
- 解决方案文件（`.sln`）被 gitignore，由 Unity 自动生成。

## 项目结构

```
Assets/
├── Player/Scripts/      — PlayerController, WeaponManager, WeaponSlot
├── Combat/Scripts/      — MeleeFighter, AttackData, CombatController(空壳WIP)
├── Enemy/Scripts/       — EnemyController, EnemyManager, States/
├── Inventory/Scripts/   — InventoryManager, PackageLocalData, PackageTables, ShopConfig
├── Weapon/Scripts/      — WeaponPickup, WeaponConfig(SO), WeaponType
├── Core/Managers/       — GameManager, ShopManager, CurrencyManager
├── UI/Resources/Scripts/ — UIManager, BasePanel, ToastMessage, Backpack/, Lottery/, Shop/
├── Utils/               — State Machine(State<T>/StateMachine<T>), GmCmd, SkinnedMeshHighlighter
├── Resources/           — Items/, Prefabs/Panels/, WeaponConfigs/, Weapons/
├── ThirdParty/URP UniVrm/ — 勿修改
├── Blink/               — 角色美术资源，勿修改
```

## 关键模式

- **状态机**：`State<T>` / `StateMachine<T>`，敌人 AI 状态在 `Enemy/Scripts/States/` 下
- **单例**：`GameManager`、`InventoryManager`、`EnemyManager`、`UIManager`、`PackageLocalData`、`ToastMessage` — 全部无命名空间，混合 MonoBehaviour 和懒加载 C# 单例
- **ScriptableObject**：`AttackData`、`WeaponConfig`、`PackageTables`、`ShopConfig` — 通过 Unity 菜单创建（"Combat System/"、"Weapon/"、"Package/"）
- **UI 面板**：通过 `UIManager.OpenPanel(name)` 从 `Resources/Prefabs/Panels/` 按需加载，面板名称常量在 `UIconst`
- **输入系统**：Unity Input System (`Assets/InputActions/`)，`PlayerController` 通过 `InputAction.CallbackContext` 注册回调
- **数据持久化**：`PackageLocalData` 将背包数据序列化为 JSON 存入 `PlayerPrefs`

## Layers & Tags

- Layers: Player(6), Enemy(7), Playehitbox(8), Enemyhitbox(9), VisionSensor(10), Obstacles(11)
- Tags: Enemy, Hitbox

## 已知限制

- 血量硬编码为 25（`MeleeFighter`），伤害硬编码为 5
- `E_WeaponType` 定义了 5 种武器类型，但只有 Sword 有实际游戏逻辑
- 食物系统（`PackageTypeFood = 2`）仅定义了常量，未实现
- `CombatController.cs` 是空壳/WIP 文件

## 第三方依赖（勿修改）

- `Assets/ThirdParty/URP UniVrm/` — VRM 模型导入
- `Assets/Blink/` — 角色美术资源
- Cinemachine 2.10.6、Unity Input System 1.14.2、AI Navigation 1.1.7、URP 14.0.12

## 代码规范

- 无命名空间 — 所有类都在全局命名空间中
- 全部类无命名空间，新增代码也应保持此风格以保持一致性

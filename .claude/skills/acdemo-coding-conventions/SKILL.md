---
name: acdemo-coding-conventions
description: 本项目的 C# 代码写法约定 —— 无命名空间、中文注释、UTF-8 无 BOM 编码、单例与 ScriptableObject 写法、GameEntry partial 扩展。当要新增或修改 Assets/GameMain/Scripts/ 下任何 .cs 文件时使用。
---

# ACDemo 代码约定

## 触发条件

- 新增任何 C# 脚本
- 修改现有脚本（尤其涉及中文注释、单例、SO、GameEntry 扩展）
- 生成代码文本交给用户粘贴回 Unity

## 前置条件

- 目标文件属于 `Assembly-CSharp`：`Assets/GameMain/Scripts/` 下无任何 asmdef
- 确认不触碰 `Assets/GameFramework/` 与 `Assets/ThirdParty/`

## 规则

1. **不写 namespace**。所有类在全局命名空间，与现有代码一致。
2. **中文注释**：字段用途、方法职责、关键逻辑节点。注释解释"为什么"，不复述代码。
3. **文件编码：UTF-8 无 BOM + CRLF**（硬性）。已有 `ProcedureMainMenu.cs`、`ProcedureLoading.cs`、`ACUIGroupHelper.cs` 因编码错误变成乱码。写文件时显式指定无 BOM；若工具输出带 BOM，先剥离。
4. **序列化字段**：`[SerializeField] private` + `[Header]` / `[Tooltip]` 分组说明。
5. **私有字段前缀 `m_`**，与 `Procedure*`、`ACUIGroupHelper`、`HitStopManager` 一致。
6. **单例只用现有两类**：
   - MonoBehaviour 单例 —— `Awake` 中判重 `Destroy`；如 `GameManager`、`HitStopManager`、`EnemyManager`
   - 纯 C# 懒加载单例 —— `private static X _instance; public static X Instance { get { if (_instance == null) _instance = new X(); return _instance; } }`；如 `UIManager`、`CurrencyManager`、`PackageLocalData`
   - 不要引入第三种单例模式
7. **ScriptableObject** 通过 Unity 菜单创建，现有前缀：`Combat System/`、`Weapon/`、`Package/`。已有 SO：`AttackData`、`WeaponConfig`、`ItemConfig_SO`、`StaminaConfig`、`ParryConfig`、`BossPhaseConfig`。
8. **GameEntry 扩展**用 `partial`：新增 GF 组件快捷访问写在 `Assets/GameMain/Scripts/Base/GameEntry.Builtin.cs`，加静态属性并在 `InitBuiltinComponents()` 中赋值。
9. **交付方式**：默认只输出代码文本 + 注释供用户粘贴，不直接写入项目文件；仅当用户明确要求写入时才用文件工具。

## 验证方式

- Console 零编译错误。
- 确认文件仍为 UTF-8 无 BOM（中文不乱码即为通过）。

## 失败处理

- 出现乱码 → 立即停止在该文件上追加内容，先修编码再改逻辑，避免把乱码扩散到更多文件。
- 编译报"类型已存在" → 检查是否重复定义了单例，或把类错放进已有 asmdef。

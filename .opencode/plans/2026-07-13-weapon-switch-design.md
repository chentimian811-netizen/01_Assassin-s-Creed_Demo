# 武器切换设计方案

日期：2026-07-13

## 1. 项目背景与现状

第三人称动作游戏 Demo（刺客信条风格），Unity 2022.3 LTS。已有背包系统、装备系统、战斗系统。

### 当前已有能力

| 功能 | 状态 | 路径 |
|------|------|------|
| 背包装备→换武器模型 | ✅ | `PackageDetail` → `InventoryManager` → `WeaponManager` → 实例化新模型 |
| 背包装备→切换攻击动画 | ✅ (部分) | `WeaponManager` → `meleeFighter.SetAnimatorOverride(animOverride)` |
| 键盘 1/2 切换槽位 | ✅ | `WeaponSwitcher` → `WeaponManager.SwitchToSlot` |
| 伤害随武器变化 | ❌ | `MeleeFighter.OnTriggerEnter` 硬编码 `TakeDamage(5f)` |
| 连招数据随武器变化 | ❌ | `MeleeFighter.attacks` 是固定 Inspector 列表 |

### 当前动画切换方案

每把武器的 `WeaponConfig.animOverride` 设为完整的 `RuntimeAnimatorController`（整个控制器替换）。

```
装备 Sword: animator.runtimeAnimatorController = Sword.controller
装备 Dagger: animator.runtimeAnimatorController = Dagger.controller
```

问题：locomotion（走路/跑步/跳跃/蹲下）在每把武器的 controller 中重复，新增武器需要复制整个控制器，维护成本高。

## 2. 目标架构

### 核心原则

- **每把武器决定自己的攻击动画、连招序列、伤害值**
- `MeleeFighter` 从当前装备的 `WeaponConfig` 读取战斗数据
- 敌人 `MeleeFighter` 不受影响（无 `WeaponConfig` 时回退到 Inspector 序列化值）
- 动画切换改用 `AnimatorOverrideController`，避免控制器重复
- 遵守面向对象六大设计原则（见第 8 节）

### 架构总览

```
┌──────────────────────────────────────────────────┐
│                   背包 UI                         │
│  PackageDetail.OnEquipClick                      │
└─────────────┬────────────────────────────────────┘
              │ InventoryManager.EquipWeapon(uid)
              ▼
┌──────────────────────────────────────────────────┐
│              InventoryManager                    │
│  卸下同类型旧武器 → WeaponManager.EquipWeapon    │
│  设置 isEquipped = true → 持久化                 │
└─────────────┬────────────────────────────────────┘
              │ WeaponManager.EquipWeapon(uid)
              ▼
┌──────────────────────────────────────────────────┐
│              WeaponManager                       │
│  实例化 weaponPrefab → 销毁旧模型                 │
│  SyncFighterWeapon()  ← 模型同步                 │
│  meleeFighter.SetWeaponConfig(config)  ← 【新增】│
└─────────────┬────────────────────────────────────┘
              │ SetWeaponConfig(config)
              ▼
┌──────────────────────────────────────────────────┐
│              MeleeFighter                        │
│  currentWeaponConfig = config                    │
│  animator 切换 overrideController               │
│  攻击时读取 config.attacks / config.baseDamage   │
└──────────────────────────────────────────────────┘
```

## 3. AnimatorOverrideController 方案（核心变更）

### 3.1 统一基础控制器

创建/修改 `PlayerMove.controller`：

```
PlayerMove.controller（唯一的基础控制器）
├── Layer 0: Locomotion
│   ├── 站立 BlendTree
│   ├── 蹲下 BlendTree
│   ├── 跳跃/滞空
│   └── 着陆
└── Layer 1: Combat
    ├── Melee_Attack_1        ← 占位动画片段（后续被 override）
    ├── Melee_Attack_2        ← 占位动画片段
    ├── Melee_Attack_3        ← 占位动画片段
    ├── Melee_Impact          ← 受击动画（原 SwordImpact）
    ├── Melee_FallBackDeath   ← 死亡动画（原 FallBackDeath）
    ├── Melee_CounterAttack   ← 反击动画（原 CounterAttack）
    └── Melee_CounterVictim   ← 反击受害者（原 CounterAttackVictim）
```

**改动说明**：

| 原状态名 | 新统一状态名 | 原因 |
|----------|-------------|------|
| `SwordImpact` | `Melee_Impact` | 通用化，所有武器共用同一状态名 |
| `FallBackDeath` | `Melee_FallBackDeath` | 同上 |
| `CounterAttack` | `Melee_CounterAttack` | 同上 |
| `CounterAttackVictim` | `Melee_CounterVictim` | 同上 |
| (攻击名来自 AttackData.AnimName) | `Melee_Attack_1/2/3` | 统一占位名，override 到具体武器动画 |

**为什么攻击状态也要统一占位名？**

因为 `AnimatorOverrideController` 是按**状态名**替换动画片段的。如果每个武器的 `AttackData.AnimName` 不统一（Sword_Attack1 vs Dagger_Stab），就无法用 override 批量替换——必须精确匹配基础控制器中的状态名称。

新设计规定：

```
所有近战武器的 AttackData.AnimName 统一为：
  - "Melee_Attack_1"  轻击第一下
  - "Melee_Attack_2"  轻击第二下
  - "Melee_Attack_3"  轻击第三下
  - "Melee_Attack_4"  重击（可选）
```

不同武器通过 `.overrideController` 将这些状态名映射到不同的实际动画剪辑。

### 3.2 每把武器一个 OverrideController

```
Sword.overrideController                 Dagger.overrideController
┌──────────────────────────────┐         ┌──────────────────────────────┐
│ Melee_Attack_1 → Sword_S1    │         │ Melee_Attack_1 → Dagger_Stab │
│ Melee_Attack_2 → Sword_S2    │         │ Melee_Attack_2 → Dagger_Slash│
│ Melee_Attack_3 → Sword_S3    │         │ Melee_Attack_3 → Dagger_Spin │
│ Melee_Impact   → SwordHit    │         │ Melee_Impact   → DaggerHit   │
│ Melee_FallBackDeath → SwordFB│         │ Melee_FallBackDeath → DagFB  │
│ Melee_CounterAttack → SwordCA│         │ Melee_CounterAttack → DagCA  │
│ Melee_CounterVictim → SwordCV│         │ Melee_CounterVictim → DagCV  │
└──────────────┬───────────────┘         └──────────────┬────────────────┘
               │                                          │
               ▼                                          ▼
        WeaponConfig.animOverride =             WeaponConfig.animOverride =
        Sword.overrideController                Dagger.overrideController
```

**注意**：`AnimatorOverrideController` 是 `RuntimeAnimatorController` 的子类，因此 `animator.runtimeAnimatorController` 的赋值方式不变。只需将 `WeaponConfig.animOverride` 字段类型改为 `AnimatorOverrideController`。

### 3.3 创建 OverrideController 的步骤

1. 在 Project 窗口右键 → `Create` → `Animator Override Controller`
2. 选择一个基础 `Animator Controller`（此处选 `PlayerMove.controller`）
3. 在 Inspector 中为每个占位状态拖入对应的实际动画剪辑

### 3.4 MeleeFighter 中的状态名同步

所有硬编码的状态名（`SwordImpact`、`FallBackDeath`、`CounterAttack`、`CounterAttackVictim`）统一改为新名称：

| 文件 | 行 | 原值 | 新值 |
|------|-----|------|------|
| `MeleeFighter.cs` | ~268 | `"SwordImpact"` | `"Melee_Impact"` |
| `MeleeFighter.cs` | ~308 | `"FallBackDeath"` | `"Melee_FallBackDeath"` |
| `MeleeFighter.cs` | ~325 | `"CounterAttack"` | `"Melee_CounterAttack"` |
| `MeleeFighter.cs` | ~326 | `"CounterAttackVictim"` | `"Melee_CounterVictim"` |

`AttackData.AnimName` 的值也从原先各武器自定义名字，统一为标准占位名（`Melee_Attack_1` 等），以便 override controller 能正确匹配。

## 4. 详细代码改动

### 4.1 `WeaponConfig.cs` — 改动 2 处

```csharp
[CreateAssetMenu(menuName ="Weapon/WeaponConfig")]
public class WeaponConfig : ScriptableObject
{
    // ... 现有字段不变 ...

    public int baseDamage;

    // 【改动 1】字段类型变更：RuntimeAnimatorController → AnimatorOverrideController
    public AnimatorOverrideController animOverride;

    // 【改动 2】新增：武器专属连招数据
    public List<AttackData> attacks;
}
```

### 4.2 `MeleeFighter.cs` — 改动 4 处

```csharp
// 【新增】字段
WeaponConfig currentWeaponConfig;

// 【新增】统一配置同步方法（替代 SetAnimatorOverride / ClearAnimatorOverride 的散落调用）
public void SetWeaponConfig(WeaponConfig config)
{
    currentWeaponConfig = config;
    if (config != null && config.animOverride != null)
        animator.runtimeAnimatorController = config.animOverride;
    else
        animator.runtimeAnimatorController = originalController;
}

// 【改动】OnTriggerEnter ~line 176: 硬编码 5 → 读取武器配置
float damage = attacker.currentWeaponConfig != null
    ? attacker.currentWeaponConfig.baseDamage
    : 5f;   // ← 敌人/无武器时回退
TakeDamage(damage);

// 【改动】Attack() 协程 ~line 122: 固定 attacks → 从武器配置读取
// 在协程开头：
var activeAttacks = (currentWeaponConfig != null && currentWeaponConfig.attacks.Count > 0)
    ? currentWeaponConfig.attacks
    : attacks;   // ← 敌人/无武器时回退到 inspector 列表

// 【改动】所有交叉淡入状态名统一
// "SwordImpact"        → "Melee_Impact"
// "FallBackDeath"      → "Melee_FallBackDeath"
// "CounterAttack"      → "Melee_CounterAttack"
// "CounterAttackVictim"→ "Melee_CounterVictim"
```

### 4.3 `WeaponManager.cs` — 清理 3 处散落调用

```csharp
// 【入口1】EquipWeapon() 中：
SyncFighterWeapon();
meleeFighter?.SetWeaponConfig(config);   // ← 替换 SetAnimatorOverride
OnWeaponModelChanged?.Invoke(config);

// 【入口2】SwitchToSlot() 中：
meleeFighter?.SetWeaponConfig(targetConfig);  // ← 替换 if/else 分支

// 【入口3】UnequipSlotInternal() 中：
SyncFighterWeapon();
meleeFighter?.SetWeaponConfig(null);  // ← 替换 ClearAnimatorOverride
OnWeaponModelChanged?.Invoke(oldConfig);
```

### 4.4 无改动的文件

| 文件 | 原因 |
|------|------|
| `InventoryManager.cs` | 已经调用 `WeaponManager.EquipWeapon(uid)`，无需改动 |
| `PackageDetail.cs` | 已经调用 `InventoryManager.EquipWeapon(uid)`，无需改动 |
| `PlayerCombat.cs` | 已经调用 `meleeFighter.ToTryAttack(target)`，无需改动 |
| `WeaponSwitcher.cs` | 已经调用 `WeaponManager.SwitchToSlot()`，无需改动 |
| `WeaponSlot.cs` | 纯数据容器 |
| `AttackData.cs` | 无需改动 |
| `EnemyController.cs` | 敌人无 WeaponConfig，回退到序列化值 |

## 5. WeaponConfig SO 配置清单

以新增一把 Dagger 为例，完整的配置步骤：

```
WeaponConfig（Dagger 武器配置）
├── weaponID: 2001
├── weaponName: "匕首"
├── weaponType: Dagger
├── weaponPrefab: → dagger_prefab.prefab
├── baseDamage: 3              ← 匕首低伤害高频
├── attackRange: 1.5
├── animOverride: → Dagger.overrideController
├── attacks:
│   ├── AttackData 0: AnimName="Melee_Attack_1", HitboxToUse=Sword, ImpactStartTime=0.1, ImpactEndTime=0.3
│   ├── AttackData 1: AnimName="Melee_Attack_2", HitboxToUse=Sword, ImpactStartTime=0.15, ImpactEndTime=0.35
│   └── AttackData 2: AnimName="Melee_Attack_3", HitboxToUse=LeftFoot, ImpactStartTime=0.1, ImpactEndTime=0.25
├── isRanged: false
└── ...其他默认值
```

## 6. 伤害与攻击的运行时选取逻辑

### 攻击时（协程）

```
Attack()
  ├── activeAttacks = currentWeaponConfig?.attacks ?? this.attacks
  ├── animator.CrossFade(activeAttacks[combocount].AnimName)
  └── 按 activeAttacks[combocount].ImpactStartTime/EndTime 控制命中框
```

### 命中时（OnTriggerEnter）

```
被攻击者.OnTriggerEnter(攻击者的Hitbox)
  ├── 攻击者 = other.GetComponentInParent<MeleeFighter>()
  ├── damage = 攻击者.currentWeaponConfig?.baseDamage ?? 5f
  └── TakeDamage(damage)
```

### 连击逻辑不变

`doCombo` / `combocount` 计数器逻辑完全保留，只改变数据的来源。

## 7. OOP 六大设计原则检查

| 原则 | 体现 |
|------|------|
| **单一职责 (SRP)** | `WeaponConfig` 只定义武器数据；`MeleeFighter` 只执行战斗逻辑；`WeaponManager` 只管理槽位生命周期 |
| **开闭原则 (OCP)** | 新增武器只需创建新的 `WeaponConfig` SO + `.overrideController`，零代码修改 |
| **里氏替换 (LSP)** | `AnimatorOverrideController` 是 `RuntimeAnimatorController` 的子类，替换字段类型后赋值代码不变 |
| **接口隔离 (ISP)** | `MeleeFighter` 仅依赖 `WeaponConfig` 的 `baseDamage` / `attacks` / `animOverride`，不需了解完整的武器管理上下文 |
| **依赖倒置 (DIP)** | `MeleeFighter` 依赖 `WeaponConfig` 抽象（ScriptableObject），不依赖 `WeaponManager` 具体类 |
| **组合优于继承** | 武器配置通过组合注入（`SetWeaponConfig()`），而非继承层级；`WeaponSlot` 组合 `WeaponConfig` + `GameObject` |

## 8. 文件改动汇总

| 文件 | 改动类型 | 说明 |
|------|---------|------|
| `Weapon/WeaponConfig.cs` | 修改 | 字段类型 `RuntimeAnimatorController` → `AnimatorOverrideController`；新增 `List<AttackData> attacks` |
| `Combat/MeleeFighter.cs` | 修改 | 新增 `currentWeaponConfig` 字段 + `SetWeaponConfig()` 方法；修改伤害和攻击的读取来源；统一状态命名 |
| `Player/WeaponManager.cs` | 修改 | 3 处入口统一调用 `SetWeaponConfig(config)`，移除散落的 `SetAnimatorOverride`/`ClearAnimatorOverride` |
| `Player/Animations/PlayerMove.controller` | 修改 | 添加占位状态 `Melee_Attack_1/2/3`；重命名现有状态为通用名 |
| `Resources/WeaponConfigs/` | 新增 | 为现有 Sword 武器创建 `.overrideController` 并填入 WeaponConfig；为 Dagger 等新武器创建相应 SO |

## 9. 实施顺序

1. **修改 `PlayerMove.controller`** → 添加占位状态，重命名统一
2. **修改 `MeleeFighter.cs`** → 新增字段/方法，修改伤害/攻击/状态名
3. **修改 `WeaponConfig.cs`** → 改字段类型 + 新增 attacks 列表
4. **修改 `WeaponManager.cs`** → 清理散落调用
5. **创建各武器的 `.overrideController`** → Sword、Dagger 等
6. **更新现有 `WeaponConfig` SO 资源** → 填入 overrideController 和 attacks
7. **验证** → Play 模式测试：背包换武器 → 模型/动画/伤害全变

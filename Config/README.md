# 数值配置系统（Luban + SO 混合）

本目录是**数值的唯一编辑来源**。日常调数值请只改 `Luban/Datas/Gameplay.xlsx`，
然后执行 Unity 菜单 `ACDemo/Config/Export And Validate`，再重新 Play。

SO / Prefab / 场景只保留**资源引用、动画、招式与命中盒结构、接线信息**，
不再保存已经迁走的数值（列表见 §3）。

---

## 1. 目录与职责

```text
Config/
├─ BASELINE.md                 # 迁移前的实际生效值基准（对照用，不要当配置来源）
├─ README.md                   # 本文件
└─ Luban/
   ├─ luban.conf               # groups / targets / schemaFiles / dataDir
   ├─ gen_gameplay_json.bat    # 唯一导出入口（脚本相对路径 + 输出目录白名单 + --strict）
   └─ Datas/
      ├─ __tables__.xlsx       # 表注册（每张表的 module/key/mode/输出文件名）
      └─ Gameplay.xlsx         # 所有数值 Sheet

Tools/Luban/                   # Luban 5.1.0 命令行工具（版本与校验和见 VERSION.txt）
Packages/com.code-philosophy.luban/   # Luban 运行时（内嵌包，版本 v1.2.0）

生成物（**禁止手写**，导出会覆盖）：
  Assets/GameMain/Scripts/Config/Generated/       # 生成的 C#（Tables.cs / 各表行类 / 各表 Tb 类）
  Assets/GameMain/Resources/DataTables/Luban/     # 生成的 JSON（stamina.json / item.json ...）
```

- 生成代码**不带命名空间**（`luban.conf` 里 `topModule` 置空），业务代码不需要 `using cfg;`。
- 运行时统一从 `DataRepository` 取数，业务脚本不直接碰 `Tables`。
- `DataRepository` 用 `Resources.Load` 读 JSON，不走 GameFramework Resource（GF 处于 EditorResourceMode）。

---

## 2. 日常操作

### 2.1 只改现有数值

1. 打开 `Config/Luban/Datas/Gameplay.xlsx`，改目标 Sheet 的数值单元格。
2. Unity 菜单 `ACDemo/Config/Export And Validate`
   （或命令行 `Config\Luban\gen_gameplay_json.bat`）。
3. 看 Console：导出成功 → 自动刷新资源 → 跑引用校验，全部通过才算完成。
4. 重新 Play 验证。**第一版不做运行中热更新**。
5. 提交时一起提交：`Gameplay.xlsx`、`Resources/DataTables/Luban/*.json`、必要时 `Generated/*.cs`。
   Excel 是二进制、diff 看不出内容，所以改数值时**同时看生成 JSON 的文本 diff**。

### 2.2 导出脚本做了什么

`gen_gameplay_json.bat`：

- 用脚本相对路径定位 `Tools/Luban/Luban.dll`，不写死本机绝对路径；
- 先做**输出目录白名单校验**，输出目录必须分别以 `Scripts/Config/Generated` 与
  `Resources/DataTables/Luban` 结尾，否则直接拒绝执行（Luban 会清空输出目录，防止误删业务目录）；
- 设置 `DOTNET_ROLL_FORWARD=Major`（Luban 5.1.0 目标是 net8.0，本机只有 9/10 运行时）；
- 带 `--strict`：**校验失败一律当作错误处理**。

> ⚠️ 不加 `--strict` 时，Luban 的 `#range` 校验只会打印 `ERROR` 行，**退出码仍是 0**，
> 而且会把越界值照常写进 JSON。所以 `--strict` 不能去掉。

### 2.3 加一张表

1. `Gameplay.xlsx` 新建 Sheet，第一行 `##var` + 字段名，第二行 `##type` + 类型，
   第三行 `##group` + 分组（客户端填 `c`），第四行 `##` + 中文注释，第五行起是数据。
   **A1 必须以 `##` 开头**，否则整张表会被静默跳过。
2. 在 `__tables__.xlsx` 追加一行（数据也从 B 列开始，A 列是 `##` 标记列）：
   `full_name | value_type | read_schema_from_file | input | index | mode | group | comment | tags | output`
   - `input` 写成 `Sheet名@Gameplay.xlsx`
   - `mode`：`map`（多行有主键）/ `one`（单行全局表）/ `list`（无主键列表）
   - `output` 写**不带扩展名**的文件名（Luban 会自己补 `.json`）
3. 导出。生成的 `Tables.cs` 会自动多一个 `TbXxx` 属性。
4. 在 `DataRepository` 里加一个 `GetXxx()` 查询入口，并补上缺表检查。
5. 在 `ConfigReferenceValidator` 里补该表与资产之间的引用/范围校验。

### 2.4 加一把武器

1. `Item` Sheet 加物品行（新 ID 一旦定下就不要改）。
2. `Weapon` Sheet 加同 `weapon_id` 的数值行。
3. 复制或新建 `WeaponConfig` 资产壳，只填：模型 Prefab、动画覆盖器、招式列表、
   `isRanged` / `isShield`、投射物 Prefab、`weaponName`。
4. 新招式要先给 `AttackData` 填 `attackId`，并在 `Attack` Sheet 加同 ID 行。
5. 导出 + 引用校验，然后验证背包、装备、伤害、命中盒、动作集。

### 2.5 加一类敌人

1. `Character` Sheet 加新的敌人 ID 与 `max_health`。
2. `Enemy` Sheet 加同 ID 的 AI/FSM 数值行。
3. 敌人招式：`AttackData` 资产壳 + `Attack` Sheet 行。
4. 敌人 Prefab 根对象挂 `CharacterConfigIdentity`，填同一个 ID。
   （迁移期可点 `ACDemo/Config/Migration/Apply Character Identities` 自动写 ID。）
5. 巡逻点、`VisionSensor`、`NavMeshAgent`、场景引用仍在 Prefab/场景里配，不迁表。

### 2.6 加字段

1. 先判断它是不是真的"可调配置"——运行时状态、内部算法常量不该进表。
2. 改 Sheet 表头（`##var` / `##type` / 注释）。
3. 导出，确认生成代码有变化。
4. 改读取代码（`DataRepository` + 消费方）。
5. 补校验，更新本 README 与 `CLAUDE.md`。

---

## 3. 表格清单与数值去向

| Sheet / 表 | 模式与主键 | 内容 |
|---|---|---|
| `Item` / `TbItem` | `map(id)` | 物品与武器基础数值（背包/商店/伤害链路） |
| `Weapon` / `TbWeapon` | `map(weapon_id)` | 攻击距离、弹药、射击冷却、弹速、散布 |
| `Attack` / `TbAttack` | `map(attack_id)` | 命中窗口、伤害倍率、削韧、可弹反、卡肉 |
| `Character` / `TbCharacter` | `map(id)` | 角色基础生命（玩家 + 敌人） |
| `Player` / `TbPlayer` | `map(id)` | 移动/重力/跳跃/锁定/翻滚/无敌帧 |
| `Stamina` / `TbStamina` | `map(id)` | 耐力上限、恢复、各项消耗 |
| `Parry` / `TbParry` | `map(id)` | 弹反与格挡（**未启用功能**，只是开发初始值） |
| `Enemy` / `TbEnemy` | `map(id)` | 敌人控制器与 FSM 数值 |
| `EnemyGlobal` / `TbEnemyGlobal` | `one` | 出手排程、初始倒计时、目标刷新周期 |
| `BossPhase` / `TbBossPhase` | `list(boss_id+phase_index)` | Boss 阶段阈值（**当前为空表**，工程无 Boss 实例） |
| `WeaponUpgrade` / `TbWeaponUpgrade` | `one` | 武器升级上限与系数 |
| `CombatGlobal` / `TbCombatGlobal` | `one` | 兜底基础伤害、默认卡肉参数 |

**已经迁出 SO / Prefab 的数值**（这些入口不再可编辑，只有在 Excel 里改才有效）：

- `Health.maxHealth`（`Character` 表；Prefab 上已删字段）
- `PlayerMovement` 的蹲/走/跑速度、重力、跳高、下落倍率、跳跃 CD、地面检测偏移、跌落阈值
- `PlayerController` 的 Shift 长按阈值、`PlayerLockOn` 的锁定距离、`PlayerAnimator` 的锁定转向速度
- `PlayerDodge` 的翻滚时长/冷却/距离/位移等待/兜底时长/无敌帧/转身时长/允许原地翻滚
  （`moveCurve` 仍留在 Prefab —— 它是整条曲线，不是单个数值）
- `StaminaConfig` 全部字段（类与 `StaminaConfig_Default.asset` 已删除）
- `ParryConfig`、`BossPhaseConfig`（类已删除，没有资产用过）
- `WeaponConfig` 的 `attackRange / maxAmmo / fireCooldown / projectilleSpeed / aimSpreadAngle / hipFireSpreadAngle`
- `AttackData` 的命中窗口、倍率、削韧、可弹反、卡肉参数
- `WeaponUpgradeSystem` 的等级上限与三个系数
- `DamageRouter.FallbackBaseDamage`、`HitStopManager` 的三个默认卡肉参数
- `EnemyController` 与全部敌人 FSM 状态的数值（含原来硬编码的 3.5 / 0.3 / 500）
- `EnemyManager` 的出手间隔区间、初始倒计时、目标刷新周期

**继续留在 Prefab / 场景 / SO 的内容**：模型与材质引用、动画覆盖器、招式列表、命中盒、
武器槽与挂点、`PatrolPiont.waitTime` 与巡逻点、`NavMeshAgent` / `CharacterController` 参数、
场景对象引用、运行时的当前血量/耐力/弹药/阶段。

---

## 4. 排错

| 现象 | 原因与处理 |
|---|---|
| `[ERROR] 拒绝执行：输出目录 ... 不是专用生成目录` | 有人改了 bat 里的输出路径；改回 `Scripts/Config/Generated` 与 `Resources/DataTables/Luban` |
| `主键值重复` / `不在范围` / `运行失败!!!` | 表数据问题，按日志里的 `记录 TbXxx[...]` 定位到 Sheet 与行 |
| 导出成功但 Play 里数值没变 | 没重新 Play（第一版不做热更新）；或改的是 Inspector 里的旧副本（已无此入口，检查是否用了旧分支） |
| `[DataRepository] 初始化失败` / `缺少配置行：id=...` | 生成 JSON 缺失或表里没这行；重新导出，或补行 |
| 用 PowerShell 看生成文件是乱码 | `Get-Content` 默认按 GBK 解码 UTF-8；加 `-Encoding UTF8` 或用编辑器打开，**不是**真的编码事故 |
| `You must install .NET to run this application` | 没有设置 `DOTNET_ROLL_FORWARD`；直接用 bat 导出，别手敲 dotnet 命令 |
| 校验报「attackId 为空」 | 该 `AttackData` 是新加的；执行 `ACDemo/Config/Migration/Apply Attack Ids` 或用 `ACDemo/Config/Export And Validate` 后手动填 |

导出失败时**不要**手工修补生成的 JSON：修 Excel，重新完整导出。
若生成目录已被失败的导出破坏，从最近一次已验证的提交恢复这两个目录再导出。

---

## 5. 已知缺陷（本次不修，已记录）

1. ~~弓的 `weaponID = 1111` 在 Item 表里不存在~~ **已修复（2026-10-28）**：
   `BowConfig.weaponID` 与 `Weapon` 表行都改为 **10004**，`Item 10004 Hunting Bow` 的 `weapon_type` 改为 **3(Bow)**，
   校验器里按 `1111` 判定的已知缺陷分支已删除；背包里现在可以正常装备弓。
   遗留：`BowConfig.animOverride` 指向的是玩家的基础控制器 `PlayerMove.controller`（类型不匹配，运行时为 null），
   弓没有专属动作覆盖 —— 要做持弓姿态需要单独建一个 AnimatorOverrideController。
2. **弹反没有启用**：`Parry` 表只有开发初始值，工程里没有 `ParrySystem` 实例，
   `GetBlockInput` 仍未接线。表里有值 ≠ 玩法已实现。
3. **Boss 没有实例**：`BossPhase` 表故意为空，只验证了结构与读取链路，Boss Play 未覆盖。
4. `Item 10007 Axe` 的 `description` 历史乱码（`SharpAxe??`）原样保留，没有在迁移里改写文案。

---

## 6. 编码与工程约定

- 手写 C#：UTF-8 无 BOM + CRLF，无命名空间，私有字段 `m_` 前缀，中文注释。
- 生成 C# / JSON 由 Luban 输出，同样是 UTF-8 无 BOM + CRLF；**不要手改**。
- 生成目录里的 `.meta` 会跟着生成物一起进版本控制；Luban 会清理目录内非生成物，
  实测**不会**删除 `.meta`，但如果目录里放了别的手写文件会被清掉。
- 改了配置**不要**顺带改 Prefab/场景：一次改动只改 Excel，diff 里只应有 xlsx + json（+ 必要时生成的 .cs）。

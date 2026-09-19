# 动作集系统 —— 分步实施计划（可执行版）

> 本文是 `Moveset_System_Design.md`（设计方案，v1.3）的**施工图**。
> 设计文档回答"为什么这样做"，本文回答"**现在按什么顺序动手、每步改哪个文件、怎么验证**"。
>
> | 项目 | 内容 |
> |---|---|
> | 建立日期 | v1.0 · 基于设计文档 v1.3 + 项目实测状态 |
> | 项目 | `01_Assassin's Creed_Demo`（Unity 2022.3.62f2c1） |
> | 玩家资产 | `Assets/GameMain/Entities/Player/Male.prefab`（骨架 `Assets/Fantasy knight/Prefab/Skin_1_4.prefab`） |
> | 唯一场景 | `Assets/GameMain/Scenes/TestScene.unity` |
> | 当前进度 | **阶段 0 完成 ｜ 步骤 1 完成** → 下一步 **步骤 2** |

---

## 一、施工约定（每步都适用）

### 1.1 交付方式

- C# 一律 **UTF-8 无 BOM + CRLF**、无 namespace、中文注释、私有字段 `m_` 前缀
- 每步完成后必须跑一次**编译自检**（见 1.2），零非环境错误才继续
- 涉及伤害/致死的改动，守住"致死只走 `DamageRouter`"（兜底出口 `Health.ForceKill`）

### 1.2 编译自检（本项目专用，已实测可用）

`dotnet build Assembly-CSharp.csproj` **在本机走不通** —— .NET 10 SDK 缺两个 workload 目录
（`MSB4276`），`restore` 直接失败且**不打印任何错误**，极具迷惑性。改用直接调 Roslyn：

```powershell
$sdk   = "C:\Program Files\dotnet\sdk\10.0.300"
$csc   = "$sdk\Roslyn\bincore\csc.dll"
$unity = "C:\Unity\2022.3.62f2c1\Editor"
$proj  = "C:\Unity\01_Assassin's Creed_Demo"

$refs  = @()
$refs += Get-ChildItem "$sdk\ref" -Filter "*.dll" | ForEach-Object { $_.FullName }
$refs += Get-ChildItem "$unity\Data\Managed\UnityEngine" -Filter "*.dll" | ForEach-Object { $_.FullName }
$refs += "$proj\Library\ScriptAssemblies\Assembly-CSharp.dll"
$refs  = $refs | Select-Object -Unique      # ⚠️ 不要加 Data\Managed\UnityEngine.dll（兼容门面 → CS0433）

$args = @($csc,"-nologo","-target:library","-langversion:9.0","-nostdlib+","-out:$env:TEMP\chk.dll",
          "-nowarn:1701,1702,1705,0436,0618")
foreach ($r in $refs) { $args += "-r:$r" }
$args += @("要检查的1.cs","要检查的2.cs")
& dotnet @args 2>&1 | Where-Object { $_ -match 'error CS' }
```

**判读标准**：只应剩 **4 条 `CS1705`**（Unity 要 netstandard 2.1、SDK ref 给 2.0 的门面差异）。
任何其他 `error CS` 都是真问题。

### 1.3 编码自检

```powershell
$b = [System.IO.File]::ReadAllBytes($f)
"BOM=$($b[0] -eq 0xEF -and $b[1] -eq 0xBB -and $b[2] -eq 0xBF)"   # 必须 False
$crlf=0;$lf=0; for($i=0;$i -lt $b.Length;$i++){ if($b[$i] -eq 10){ if($i -gt 0 -and $b[$i-1] -eq 13){$crlf++}else{$lf++} } }
"CRLF=$crlf bareLF=$lf"   # bareLF 必须 0
```

### 1.4 改 Prefab / 资产的铁律

1. **先确认真实资产路径**。本项目已因此踩过一次坑：设计文档长期把玩家写成
   `Player.prefab`，而真正在用的是 **`Male.prefab`**（`Player.prefab` 已弃用）。
2. **Unity 开着时改 `.prefab` / `.asset` YAML 有风险**：若该资产正在 Prefab 模式中被打开，
   编辑器里的 Ctrl+S 会覆盖磁盘改动。改前先切回场景、别打开那个 prefab。
3. 改完必须校验：**fileID 唯一性**、**组件与 GameObject 双向引用一致**、**括号/缩进完整**。
4. 手工改 YAML 时，**不要依赖"运行时按名字查找"**——能显式指引用就显式指（步骤 0 的教训）。

---

## 二、步骤 0：已完成（回顾，作为基线）

> 这两步是其他一切的前提，**已做完并验收**，列在这里只为让后续步骤有明确基线。

| # | 内容 | 关键文件 | 验收 | 状态 |
|---|---|---|---|---|
| **0.1** | 修敌人命中盒（方案 C）：把 `BoxCollider` 挂到敌人武器**蒙皮骨骼** `mixamorig:Sword_joint` | `Assets/GameMain/Entities/Enemy/Enemy.prefab`、`Assets/GameMain/Scripts/Combat/MeleeFighter.cs` | 敌人攻击玩家 → 玩家掉血（每刀 5 点） | ✅ 已验收 |
| **0.2** | 盾碰撞体装备时默认关闭 | `Assets/GameMain/Scripts/Weapon/WeaponConfig.cs`（新增 `isShield`）、`Assets/GameMain/Scripts/Entity/Player/WeaponManager.cs`（新增 `ApplyInitialColliderState`） | 走动不误伤 | ⚠️ 代码就绪，**实测待补** |

**0.1 的遗留（已知，不阻塞）**：`MeleeFighter` 里有一套"按网格顶点自动测量命中盒尺寸"的
实验代码（`autoFitWeaponHitbox`，**默认关闭**）。网格顶点坐标系未查清，开启会选错空间。
正确做法是手工填 `preplacedHitbox` 的 `Size`/`Center` + 用 `OnDrawGizmosSelected` 目视校准。

**0.2 的遗留（步骤 2 会碰到）**：盾目前是**手工挂**在 `Male.prefab` 的 `hand_l` 上的，
不走装备链路，所以 `ApplyInitialColliderState` 覆盖不到它。

---

## 三、步骤总览与依赖

```
步骤1 [✅] E_WeaponType 加 Shield（值=5）
   │
   ├─> 步骤2 [ ] WeaponSlot.allowedType → allowedTypes[]
   │      │
   │      ├─> 步骤3 [ ] Male.prefab 加左手挂点 + 两槽改名/重配   ★高风险
   │      │      │
   │      │      ├─> 步骤4 [ ] WeaponManager 装备规则（双持约束 + 目标槽优先）
   │      │      │      │
   │      │      │      └─> 步骤5 [ ] Item.txt 加盾行 + 盾预制体（★漏项）
   │      │      │             │
   │      │      │             └──> ★★ 阶段一 Gate：剑右手 / 盾左手 同时可见
   │      │      │
   │      │      └─> 步骤6 [ ] Moveset 路由骨架（可在步骤4/5 之后并行启动）
   │      │             └─> 步骤7 [ ] 换覆盖器参数快照回灌 + 准入属性
   │      │
   │      └─> 步骤9 [ ] Layer1 状态重命名（与步骤6 同批，避免改两次引用）
   │
   └─> 步骤8 [ ] 三个覆盖器 .overrideController

步骤10 [ ] 左手攻击通道（E_Hand / Shield 判定 / LeftAttack 输入 / 盾击 AttackData）
步骤11 [ ] 举盾 + 格挡判定 + 精力 + 破防
步骤12 [ ] 弹反 + 处决
```

**依赖要点**：
- 步骤 2、3、4、5 是**严格串行**的（都改同一批数据/资产）
- 步骤 6/7 可以在步骤 4 之后、5 之前启动（代码层互不干扰）
- 步骤 9 必须与步骤 6 同批做（重命名会牵动 `AttackData.AnimName`，别改两次）
- 步骤 8 依赖 9（覆盖器要按新槽位名映射）
- 步骤 10 依赖 5 + 8

---

## 四、逐步实施说明

### 步骤 1 — `E_WeaponType` 末尾追加 `Shield` ✅ 已完成

| 项 | 内容 |
|---|---|
| 文件 | `Assets/GameMain/Scripts/Weapon/WeaponType.cs` |
| 改动 | 末尾追加 `Shield`（值 = **5**） |
| 验收 | 编译通过；`Item.txt` 已有数据不受影响 |
| 状态 | ✅ 已完成并编译验证 |

**为什么加在末尾**：`Item.txt` 的 `WeaponType` 列存整数，插值会让整列语义偏移。

> **实测数据（比设计文档更准）**：现有 `WeaponType` 值与枚举**完全对得上**：
> `10001 Sever=0(Sword)`、`10002 Demon Breaker=3(Bow)`、`10003 Ascending Dragon=1(Dagger)`、
> `10004 Hunting Bow=1(Dagger)`、`10007 Axe=2(Axe)`。
> 所以**步骤 5 不需要"订正 WeaponType 列"**（设计文档 §6.1 的这个判断已过时）。
> 但 `10002`/`10003`/`10004` 的**名字与类型不匹配**（Demon Breaker 名字像剑却是弓、
> Hunting Bow 是弓却标 1=Dagger），属于**文案/配置问题**，与本期无关，先不动。

---

### 步骤 2 — `WeaponSlot.allowedType` → `allowedTypes[]`

| 项 | 内容 |
|---|---|
| 文件 | `Assets/GameMain/Scripts/Entity/Player/WeaponSlot.cs`（改）+ `Assets/GameMain/Scripts/Entity/Player/WeaponManager.cs`（同步 6 处） |
| 依赖 | 步骤 1 |
| 预估 | 小 |

**现状**（`WeaponSlot.cs:10`）：

```csharp
public E_WeaponType allowedType;      // 单值
```

**目标**：改为数组 + 一个 `Accepts()` 查询方法（设计文档 §6.2）。

**`allowedType` 的全部引用点**（已 grep 确认，共 6 处 / 2 个文件）：

| 文件 | 位置 | 现用途 |
|---|---|---|
| `WeaponSlot.cs` | `:10` | 字段定义 |
| `WeaponManager.cs` | `:69` | `EquipWeapon` 里校验 `wType != targetSlot.allowedType` |
| `WeaponManager.cs` | `:134` | `UnequipSlotByType` 按类型找槽 |
| `WeaponManager.cs` | `:188` | `FindSlotForWeapon` 找空槽 |
| `WeaponManager.cs` | `:194` | `FindSlotForWeapon` 找同类型槽替换 |
| `WeaponManager.cs` | `:241` 附近 | `SwitchToSlot` |

**⚠️ 高风险点**：Unity 序列化字段**类型变更会清空 prefab 上的原值**。
`Male.prefab` 现在两个槽的 `allowedType` 分别是 `0`(Sword) 和 `2`(Axe)，
改数组后会变成**空数组**，必须在步骤 3 里重新勾选。

**验收**：
1. 编译零错误
2. `Male.prefab` 的 `weaponSlots` 在 Inspector 里显示为列表型字段（值为空是预期的）
3. 进 Play 不报错（此时两个槽都不接受任何武器 → 装不上武器是**预期**的，步骤 3 修）

---

### 步骤 3 — `Male.prefab` 加左手挂点 + 两槽重配 ★高风险

| 项 | 内容 |
|---|---|
| 文件 | `Assets/GameMain/Entities/Player/Male.prefab` |
| 依赖 | 步骤 2 |
| 预估 | 中 |

**现状**（已勘定）：

```yaml
weaponSlots:
- slotName: MainHand
  holdPoint: {fileID: 1176333465599525210}   # = Skin_1_4 的 hand_l
  allowedType: 0                             # Sword
- slotName: AxeHand
  holdPoint: {fileID: 1176333465599525210}   # ← 同一个挂点！
  allowedType: 2                             # Axe
mainWeaponSlotIndex: 0
```

**目标**：

```yaml
weaponSlots:
- slotName: RightHand
  holdPoint: <右手骨骼>
  allowedTypes: [Sword, Dagger, Axe]          # 右手不放盾
- slotName: LeftHand
  holdPoint: <新建空物体，父级 = lowerarm_l 或 hand_l>
  allowedTypes: [Shield]
mainWeaponSlotIndex: 0                          # = RightHand
```

**已勘定的骨架坐标（`Skin_1_4.prefab`，可直接用）**：

| 骨骼 | Transform fileID | 局部位置（相对父） |
|---|---|---|
| `lowerarm_l`（左小臂） | `8449375469879590680` | `(-0.303, 0.017, 0.012)` |
| `hand_l`（左手掌） | `4641400885159566759` | `(-0.244, 0.015, 0)` |
| `hand_r`（右手掌） | **待勘**（步骤开始时先 grep `m_Name: hand_r`） | — |

**⚠️ 关键技术点**：`holdPoint` 是**跨 prefab 的 stripped Transform**
（`m_CorrespondingSourceObject` 指回 `Skin_1_4.prefab`）。新增挂点必须用同样形式的引用，
**不能凭名字查找**。

**Q6 决策点（盾挂手掌还是小臂）**：用户已把盾手工挂在 `hand_l` 上调好了
`Position (-0.02, -0.033, 0)` / `Rotation (71.275, -94.847, -81.633)`。
建议：**先沿用 `hand_l`**（已有可用数值，风险最低），
阶段三配好 `Block_Loop` 动画后再评估是否迁到 `lowerarm_l`。

**验收（阶段一 Gate 的前半）**：
1. Play 中 Console 无 `MissingReferenceException`
2. Inspector 里两个槽的 `allowedTypes` 已正确勾选
3. 盾的模型在左手位置正确（此时还没接装备链路，看的是手工挂的那个）

---

### 步骤 4 — `WeaponManager` 装备规则（双持约束 + 目标槽优先）

| 项 | 内容 |
|---|---|
| 文件 | `Assets/GameMain/Scripts/Entity/Player/WeaponManager.cs` |
| 依赖 | 步骤 3 |
| 预估 | 中 |

**要改的三处**：

**(a) 双持约束**：同一件装备不可同时占两手。

```csharp
/// <summary>装备时若该 uid 已在另一手，先卸下另一手</summary>
void EnsureNotDualEquipped(string uid, WeaponSlot target)
{
    foreach (var slot in weaponSlots)
    {
        if (slot == target) continue;
        if (slot.equippedUid == uid) UnequipSlotInternal(slot);
    }
}
```

> **不做的后果**（设计文档 §6.4 已写明）：两个模型指向同一个 `PackageLocalItem.uid`，
> 卸下一边时另一边的 `currentModel` 变成"已销毁的 GameObject" → 空引用必崩。

**(b) 目标槽优先**：装备请求携带明确的目标槽，`FindSlotForWeapon` 只在无目标槽时兜底。
否则"拖个盾到左手 → 系统自动把右手的剑顶掉"。

**(c) ★ `SyncFighterWeapon()` 必须排除盾**（设计文档**漏了这条**，是本次核对新发现的）：

```csharp
// 现状：把 mainWeaponSlotIndex 指向的模型直接交给 MeleeFighter
meleeFighter.SetWeapon(mainSlot.currentModel);

// 问题：主手槽若装了盾，WeaponCollider 会指向盾 → 剑的攻击判定打在盾上
```

**风险等级：高**。必须在步骤 5 之前处理，否则 `MeleeFighter.WeaponCollider` 会指向盾，
`EnableHitbox` 开的是盾的碰撞体，剑砍不中人。

建议改法：`MeleeFighter` 只认**右手槽**，盾的碰撞体单独持有（为步骤 10 的
`E_AttackHitbox.Shield` 预留）。

**验收**：
1. 把同一把剑先后装到两个槽 → 先装的自动卸下，无空引用、无 `MissingReferenceException`
2. `MeleeFighter.WeaponCollider` 在装盾后仍指向剑（用 Inspector 或临时日志确认）

---

### 步骤 5 — `Item.txt` 加盾行 + 新建盾预制体

| 项 | 内容 |
|---|---|
| 文件 | `Assets/GameMain/Resources/DataTables/Item.txt` + **新建盾预制体** + `Assets/Resources/WeaponConfigs/ShieldConfig.asset` |
| 依赖 | 步骤 4 |
| 预估 | 中 |

#### 5a. `Item.txt` 加盾行

**⚠️ 先订正一个过时判断**：设计文档 §6.1 说"文件编码是 GBK 不是 UTF-8，直接按 UTF-8 读会失败"。
**实测不成立** —— 该文件前 8 字节是 `23 09 49 64 09 54 79 70`（即 ASCII `#\tId\tTyp`），
主体是纯 ASCII，只有个别中文标点（如 `SharpAxe！` 的 `！`）。
`TSVParser` 按 UTF-8 读**不会失败**。所以**不需要编码转换**，只需追加盾行。

**要加的行**（格式与现有行严格对齐：12 列 Tab 分隔）：

```text
	10008	3	5	0	0	3	Round Shield	...	...	Sprites/UI/Weapon/shield_01	Item_RoundShield
列序说明：            ↑Type=3   ↑WeaponType=5(Shield)   ↑BaseDamage=0
```

| 列 | 值 | 说明 |
|---|---|---|
| `Id` | `10008` | 需确认不与现有冲突（当前最大 10007） |
| `Type` | `3` | 1=武器 2=食物；**3=盾**（新约定，需在解析侧确认不报错） |
| `WeaponType` | `5` | = `E_WeaponType.Shield`（步骤 1 刚加的值） |
| `BaseDamage` | `0` | 盾击伤害全来自 `AttackData.DamageMultiplier`（Q3 建议） |
| `AssetId` | `Item_RoundShield` | 需配套 `Resources/ItemConfigs/Item_RoundShield.asset` |

**⚠️ 前置核实**：`DataRepository` 解析时对 `Type=3` 是否有白名单校验？
动手前先读 `Assets/GameMain/Scripts/DataTable/` 下的解析代码。

#### 5b. 新建盾预制体（★ 设计文档遗漏项）

设计文档 §15.1 只列了 `ShieldConfig.asset`，**漏了盾预制体**。
`WeaponConfig.weaponPrefab` 需要它，而项目里目前只有裸 FBX。

内容物（照 `Assets/GameMain/Entities/SwordPrefab.prefab` 的既有约定）：

| 项 | 值 |
|---|---|
| 源模型 | `Assets/Sword_and_Shield_Anims/Art/Models/SkelMesh_RoundShield.fbx` |
| 层 | `Playehitbox(8)` |
| Tag | `Hitbox` |
| 碰撞体 | `BoxCollider`，`IsTrigger = true`，**初始 `enabled = false`** |
| Size / Center | `(0.6, 0.6, 0.1)` / `(0, 0, 0.05)`（用户已验证的数值） |
| 禁止 | 不要 Animator（裸 FBX 会带一个空 Avatar，要移除） |
| 建议路径 | `Assets/GameMain/Entities/Shield/ShieldPrefab.prefab` |

#### 5c. `ShieldConfig.asset`

`Assets/Resources/WeaponConfigs/ShieldConfig.asset`：`weaponID = 10008`、`isShield = true`、
`weaponPrefab` 指向 5b、`isRanged = false`。

**验收（★ 阶段一 Gate）**：
1. Play 中把盾装到左手、剑装到右手 → **两个模型同时可见、位置正确**
2. 盾的碰撞体 `enabled == false`（走路不误伤 —— 这一步才算把 0.2 的实测补上）
3. Console 无 `MissingReferenceException`

---

### 步骤 6 — Moveset 路由骨架（纯逻辑，不需要动画资产）

| 项 | 内容 |
|---|---|
| 文件 | **新建** `Assets/GameMain/Scripts/Weapon/MovesetType.cs`、`MovesetConfig.cs`、`MovesetResolver.cs`；改 `MeleeFighter.SetWeaponConfig` |
| 依赖 | 步骤 4 |
| 预估 | 中 |

**(a) 新建三个文件**（设计文档 §5.3 / §3.1）：

- `MovesetType.cs` —— `E_MovesetType { Unarmed, OneHandedSword, SwordAndShield }`
- `MovesetConfig.cs` —— SO，含 `movesetType` / `locomotionOverride` / `rightHandAttacks` / `leftHandAttacks` / 演出槽位名 / `walkMoveTier` / `runMoveTier`
- `MovesetResolver.cs` —— **唯一判定点**，纯函数：

```csharp
public static E_MovesetType Resolve(WeaponConfig right, WeaponConfig left)
{
    bool rightSword = IsCategory(right, E_WeaponType.Sword);
    bool leftShield = IsCategory(left, E_WeaponType.Shield);
    if (!rightSword) return E_MovesetType.Unarmed;          // 空手 / 双盾 / 非法组合
    return leftShield ? E_MovesetType.SwordAndShield
                      : E_MovesetType.OneHandedSword;
}
```

> **宪法（设计文档 §3.1）**：任何地方都不得出现第二个"当前是什么动作集"的判断。

**(b) 三个 `MovesetConfig` 资产**（`Assets/Resources/Movesets/`），**覆盖器先留空**。

**(c) 改 `MeleeFighter.SetWeaponConfig`** —— 现状是把 `animOverride` 当整个控制器直接赋值
（语义错误，设计文档 §1.3 已登记），改走 `MovesetResolver`。

**验收（设计文档 §10 阶段 2 Gate）**：
1. 空手 → Console 打印 `Unarmed`
2. 右手剑 + 左手空 → `OneHandedSword`
3. 右手剑 + 左手盾 → `SwordAndShield`
4. 双盾 / 空手 → `Unarmed`
5. **覆盖器留空状态下无报错、无动作瞬移**

---

### 步骤 7 — 换覆盖器的参数快照回灌 + 准入属性 🔴 关键

| 项 | 内容 |
|---|---|
| 文件 | **新建** `Assets/GameMain/Scripts/Entity/Player/PlayerAnimationSet.cs`；改 `WeaponManager.cs` |
| 依赖 | 步骤 6 |
| 预估 | 中 |

**为什么是必须项而不是优化项**（设计文档 §11.6）：
`animator.runtimeAnimatorController = x` 这个赋值动作**会让全部 animator 参数回到默认值**。
不处理的表现是 —— **角色在跑步中换武器，动画瞬间跳回待机**。

**两步走**：
1. **快照 → 赋值 → `yield return null` → 回灌**（`PlayerMove.controller` 的全部参数：
   `PlayerState` / `MoveSpeed` / `TurnSpeed` / `JumpSpeed` / `FeetTween`）
2. **`CanSwitchMoveset` 准入属性**（收敛成**一个**具名属性，不要在调用点写 `if` 组合）：
   翻滚 / 攻击 / 受击 / 死亡期间一律拒绝；不满足时**暂存请求**（`m_pendingMoveset`）
   并在下一次 Update 重试，**而不是丢弃** —— 否则玩家在翻滚中换武器会"按键没反应"。

**验收**：
1. **跑步中换武器 → 动画不跳回待机**
2. **翻滚中换武器 → 请求被暂存，翻滚结束后生效**

---

### 步骤 8 — 三个 `.overrideController`

| 项 | 内容 |
|---|---|
| 文件 | `Assets/GameMain/Scripts/Entity/Player/Animations/Override/` 下 3 个新建 |
| 依赖 | 步骤 9（槽位名要先定） |
| 预估 | 大（素材映射工作量大） |

**前置**：`PlayerMove.controller` 必须已按步骤 9 重命名，且**是唯一基础控制器，永不复制**
（设计文档 §3.4）。`PlayerDodge.cs` 会遍历 `runtimeAnimatorController.animationClips`
计算翻滚时长 —— 一式两份控制器必然有一份算错，覆盖器方案天然避开这个问题。

素材来源：`Assets/Sword_and_Shield_Anims/Art/Animations/`（125 个）。
详细槽位映射表见设计文档 §7.2。

**验收**：
1. 空手走路 → 空手动作；持盾剑走路 → 盾剑动作
2. **两种状态下走路脚都不打滑**（共用同一棵 BlendTree，`PlayerAnimator` 的移动档位一行不改）
3. 翻滚在三种动作集下都正常

---

### 步骤 9 — Layer1 状态重命名 + 同步引用

| 项 | 内容 |
|---|---|
| 文件 | `PlayerMove.controller` + 所有引用旧名的资产 |
| 依赖 | 与步骤 6 同批做 |
| 预估 | 小（但漏改会静默失效） |

**重命名表**（已确认现状名称存在）：

| 现名 | 新名 |
|---|---|
| `Melee_Attack_1/2/3` | `Attack_Light_1/2/3` |
| `Melee_Impact` | `Attack_Impact` |
| *（新增）* | `Shield_Bash_1/2/3`、`Block_Start/Loop/End` |
| `Melee_CounterAttack` / `Melee_CounterVictim` / `Melee_FallbackDeath` | **不改**（演出动作，两态共用） |

**⚠️ 重命名后必须同步**：
- `Assets/GameMain/Scripts/Combat/Attacks/Sword/*.asset` 的 `AnimName`
  （实测现值：`Sword_Attack1 = Melee_Attack_1`、`2 = Melee_Attack_2`、`3 = Melee_Attack_3`）
- `Assets/Resources/WeaponConfigs/*.asset` 里 `AttackData` 的引用
- `MeleeFighter.cs` 里的 `animator.CrossFade("Melee_Impact", ...)`、`"Empty"`、死亡/处决相关字符串
- 敌人用的是 `EnemyController.controller`，**不受影响**

> **⚠️ 静默失效警告**：`CrossFade` 找不到状态**不报错，只是不播**。
> 重命名后必须全局搜索 `Melee_Attack` / `Melee_Impact` 确认无残留。

---

### 步骤 10 — 左手攻击通道

| 项 | 内容 |
|---|---|
| 文件 | `MeleeFighter.cs`、`AttackData.cs`、`PlayerController.cs`、`PlayerActions.inputactions` + 3 个盾击 SO |
| 依赖 | 步骤 5 + 8 |
| 预估 | 大 |

**五件事**：
1. `E_Hand` 参数 + **双连招计数器**（`int[] m_comboCount = new int[2]`）
   —— 否则"右手砍两刀 → 右键盾击 → 变成盾击第 3 段"
2. `E_AttackHitbox.Shield` + `EnableHitbox` 分支（**这里才把 0.2 关掉的盾碰撞体重新接上**）
3. `LeftAttack` action（建议 **V** 键）+ `GetLeftAttackInput`
4. 盾击三段的 `AttackData`（含**实测**判定窗口）
5. `AttackData` 增加 `impactStrength` / `displacementScale` 两条 `AnimationCurve`

**⚠️ 不要建第二个 `MeleeFighter`**：它持有 `currentWeapon` / `WeaponCollider` / `Health` /
`GameEvents` 订阅，建两个会产生**双份伤害判定路径**，违反"致死只走 `DamageRouter` 唯一路径"。

**⚠️ 连招必须走 `PlayerStamina.TryConsume`**（设计文档 §11.17）：
借 CBTFM 的"窗口化输入缓冲"（时序），**不借**它的"绕过能力门禁"（权限）——
否则会出现"第一刀扣耐力 → 第二刀不扣 → 无限连招，耐力形同虚设"。

**验收**：
1. 盾剑状态：左键 → 剑三段连斩；V → 盾三段连击
2. 两套连招进度互不干扰
3. 盾击命中敌人 → 掉血；**判定窗口外 → 不掉血**

---

### 步骤 11 — 举盾 + 格挡判定 + 精力

| 项 | 内容 |
|---|---|
| 文件 | `PlayerMove.controller` + C# + `ParrySystem` |
| 依赖 | 步骤 10 |
| 预估 | 大 |

**类魂核心闭环**：

```
攻击消耗精力 → 格挡受击消耗精力 → 精力耗尽 → 破防硬直 → 挨打
```

- 5.1 `Block_Start/Loop/End` 接线（**先只播动画**）
- 5.2 格挡受击扣精力（`guardStaminaCost`）—— **这里需要先把 `guardAbsorption` 等
  3 个字段加到 `WeaponConfig`（步骤 5c 的 `ShieldConfig` 只用了 `isShield`）**
- 5.3 破防判定（累计 `guardBreakPoise`）+ 硬直
- 5.4 `Aim` / `Block` 互斥（右键双功能：持弓时瞄准，否则举盾）

**格挡判定用最简形式**（`Health` + 一个 `isBlocking` 分支），不做效果列表
（Q12：只有玩家一个可格挡单位，建列表不划算）。

**验收**：
1. 按住右键 → 播 `Block_Loop`；松开 → `Block_End`
2. 举盾被攻击 → 不掉血，精力下降
3. 精力归零 → 破防硬直
4. 持弓时右键 → 瞄准而非举盾

---

### 步骤 12 — 弹反 + 处决

资产现成：`Paired_Attack_ShieldParryStab_Attacker/Victim`。
`MeleeFighter` 已有 `inCounter` 状态与 `PerformCounterAttack` 框架（含一处已修的无敌残留 bug）。

---

## 五、风险与注意事项（按步汇总）

| 步骤 | 风险 | 等级 | 缓解 |
|---|---|---|---|
| 2 | `allowedType` → 数组会**清空 prefab 原值** | 🟠 中 | 步骤 3 里重新勾选，别忘 |
| 3 | `holdPoint` 是跨 prefab 的 stripped 引用 | 🟠 中 | 照现有槽的 YAML 形式写，不按名字找 |
| 4 | **`SyncFighterWeapon` 把盾塞给 `MeleeFighter`** | 🔴 高 | 主手槽排除盾；装盾后确认 `WeaponCollider` 仍指向剑 |
| 5 | `Item.txt` 的 `Type=3` 可能不被解析器接受 | 🟠 中 | 动手前先读 `DataRepository` 解析代码 |
| 5 | 盾预制体是设计文档**遗漏项**，容易漏做 | 🟠 中 | 已列入本节 5b |
| 6/7 | 换覆盖器导致 animator 参数重置 | 🔴 高 | 步骤 7 的快照回灌；Gate 必须有"跑步中换武器不跳帧" |
| 8 | 跨骨架动画重定向手型不匹配 | 🟠 中 | 角色是 `Skin_1_4`（mixamo 命名），素材是 `Android_SkeletalMesh`；两者均 Humanoid 可重定向，但需目视检查盾是否"握在手里" |
| 9 | 重命名漏改引用 → **静默不播** | 🟠 中 | 全局搜 `Melee_Attack` / `Melee_Impact` |
| 10 | 连招绕过耐力门禁 | 🟠 中 | 连招仍走 `TryConsume` |
| 通用 | `using UnityEditor;` 混进运行时程序集 | 🔴 高 | 本项目**无 asmdef**，混入会**直接导致打包失败**；一律包 `#if UNITY_EDITOR` |
| 通用 | `HitStopManager` 是 `Time.timeScale` 唯一所有者 | 🟠 中 | 任何地方都不许直接写 `timeScale` |

---

## 六、每步的完成定义（DoD）

一步算"完成"，必须同时满足：

1. **编译零非环境错误**（1.2 的方法，只剩 4 条 `CS1705`）
2. **编码正确**（UTF-8 无 BOM + CRLF，`bareLF = 0`）
3. **Play 验收项全部打勾**（本节列出的验收）
4. **Console 无 `NullReferenceException` / `MissingReferenceException` / mojibake**
5. **设计文档与本计划的对应条目已回填状态**

---

## 七、当前状态与下一步

| 项 | 值 |
|---|---|
| 已完成 | 步骤 1 |
| 下一步 | **步骤 2 —— `WeaponSlot.allowedType` → `allowedTypes[]`** |
| 步骤 2 的前置确认 | ① 关掉 Unity 里打开的 prefab ② 确认 `Male.prefab` 是唯一使用方 |
| 步骤 2 的风险提示 | 改完 `Male.prefab` 的 `allowedTypes` 会变空，**这是预期的**，步骤 3 补 |

---

**文档结束**

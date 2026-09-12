using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 战斗底座自检工具（P0-Framework 验收用）。
/// 菜单位置：Tools → 底座自检
///
/// 【为什么是一个菜单工具，而不是 Unity Test Framework 的 EditMode 测试】
///   本工程的游戏脚本全部位于预定义程序集 Assembly-CSharp（GameMain 下没有 asmdef）。
///   而 UTF 的测试程序集（asmdef）**无法引用 Assembly-CSharp** —— 这是 Unity 的固定行为：
///   预定义程序集自动引用所有 asmdef 程序集，反向引用不被支持。
///   因此要把这 6 条断言做成标准 UTF 测试，必须先给 GameMain 加 asmdef（属项目结构变更，见方案 §14.3）。
///   本工具放在 Assets/Editor/ 下，编译进 Assembly-CSharp-Editor，可直接访问游戏类型，零结构变更。
///
/// 【定位】
///   1. 现在就能用：每次改完承伤通道/耐力/卡肉，点一次菜单即可回归；
///   2. 将来可平移：断言体与 UTF 写法一致，加 asmdef 后可逐条搬进 [Test] 方法。
///
/// ⚠️ 运行前提：被测类型必须已存在。P0-Framework 之前运行会提示"类型尚未落地"。
/// </summary>
public static class CombatFoundationVerifier
{
    // ==================== 类型解析（编译期软引用） ====================
    // 说明：以下 3 个类型是 P0-Framework 计划新增的。此处用反射取，而不是 using 直接引用，
    //       好处是【文件在类型落地前也能编译通过】，菜单可随时打开看进度，不会因缺类型报编译错。

    private static Type FindType(string typeName)
    {
        foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type t = asm.GetType(typeName, false);
            if (t != null) return t;
        }
        return null;
    }

    private static Type T_Health => FindType("Health");
    private static Type T_PlayerStamina => FindType("PlayerStamina");
    private static Type T_HitStopManager => FindType("HitStopManager");

    // ==================== 菜单入口 ====================

    [MenuItem("Tools/底座自检/运行全部战斗底座自检 %#v")]
    public static void RunAll()
    {
        var results = new List<(string name, bool passed, string detail)>();

        RunCase(results, "① DamageInfo 默认值与来源分类", Test_DamageInfo);
        RunCase(results, "② Health 扣血 clamp 到最大血 + 治疗可回血", Test_HealthClamp);
        RunCase(results, "③ Health 多来源无敌互不覆盖", Test_InvulnerableMultiSource);
        RunCase(results, "④ PlayerStamina 耐力不足时不扣", Test_StaminaInsufficient);
        RunCase(results, "⑤ PlayerStamina 力竭后拒绝消耗", Test_StaminaExhausted);
        RunCase(results, "⑥ HitStopManager 暂停期间不被卡肉解除", Test_HitStopPause);

        PrintSummary(results);
    }

    private static void PrintSummary(List<(string name, bool passed, string detail)> results)
    {
        int passed = 0;
        var sb = new StringBuilder();
        sb.AppendLine("<b>===== 战斗底座自检结果 =====</b>");

        foreach (var r in results)
        {
            if (r.passed) passed++;
            string mark = r.passed ? "<color=green>✅ PASS</color>" : "<color=red>❌ FAIL</color>";
            sb.AppendLine($"{mark}  {r.name}");
            if (!string.IsNullOrEmpty(r.detail))
            {
                sb.AppendLine($"        <color=#aaaaaa>{r.detail}</color>");
            }
        }

        sb.AppendLine($"<b>合计：{passed}/{results.Count} 通过</b>");

        if (passed == results.Count)
        {
            Debug.Log(sb.ToString());
        }
        else
        {
            Debug.LogError(sb.ToString());
        }
    }

    // 统一处理同步与协程两类用例，并保证异常不中断整批
    private static void RunCase(List<(string, bool, string)> results, string name, Func<IEnumerator> test)
    {
        IEnumerator routine;
        try
        {
            routine = test();
        }
        catch (Exception e)
        {
            results.Add((name, false, $"用例启动异常：{e.Message}"));
            return;
        }

        // 同步用例（未 yield）会在这里一次性跑完
        while (true)
        {
            bool moved;
            try
            {
                moved = routine.MoveNext();
            }
            catch (Exception e)
            {
                results.Add((name, false, $"断言异常：{e.Message}"));
                return;
            }

            if (!moved)
            {
                results.Add((name, true, null));
                return;
            }

            // 用例要求等待真实帧 → 必须转交 EditorApplication.update 异步驱动
            if (routine.Current is AsyncWait)
            {
                EditorApplication.CallbackFunction pump = null;
                pump = () =>
                {
                    bool advanced;
                    try
                    {
                        advanced = routine.MoveNext();
                    }
                    catch (Exception e)
                    {
                        EditorApplication.update -= pump;
                        // ⚠️ 必须先写入结果再打印汇总，否则这条失败不会被计入
                        results.Add((name, false, $"断言异常：{e.Message}"));
                        PrintSummary(results);
                        return;
                    }

                    if (!advanced)
                    {
                        EditorApplication.update -= pump;
                        results.Add((name, true, null));   // 同上：先记录再汇总
                        PrintSummary(results);
                    }
                };
                EditorApplication.update += pump;
                return;
            }

            // 其他 yield（如 yield return null 之外的类型）不支持，明确报错而不是静默通过
            results.Add((name, false, $"不支持的 yield 类型：{routine.Current?.GetType().Name ?? "null"}"));
            return;
        }
    }

    /// <summary>用例内用 yield return new AsyncWait() 表示"需要真实帧推进"</summary>
    private sealed class AsyncWait { }

    // ==================== 反射辅助 ====================

    private static void SetField(object target, string fieldName, object value)
    {
        FieldInfo f = target.GetType().GetField(fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        if (f == null)
        {
            throw new Exception($"找不到字段 {target.GetType().Name}.{fieldName}，" +
                                "请核对方案 §4 的代码是否与工程一致");
        }
        f.SetValue(target, value);
    }

    private static T GetField<T>(object target, string fieldName)
    {
        FieldInfo f = target.GetType().GetField(fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        if (f == null) throw new Exception($"找不到字段 {target.GetType().Name}.{fieldName}");
        return (T)f.GetValue(target);
    }

    /// <summary>
    /// 手动调用组件的 Awake()。
    ///
    /// 【为什么必须手动调】在编辑器工具里 AddComponent 后，Unity **不会**在编辑模式下调用 Awake —— 
    /// Awake 只在进入 Play、或对象被载入场景时触发。若跳过这一步，Health.currentHealth 会停留在
    /// 字段默认值、PlayerStamina.current 也是 0，断言会得出错误结论（假失败）。
    /// 手动调用既真实（这正是运行时第一次执行的东西），又能立刻拿到正确初值。
    /// </summary>
    private static void InvokeAwake(Component component)
    {
        MethodInfo awake = component.GetType().GetMethod("Awake",
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

        // Awake 可能被声明为 private void Awake()（本工程约定），也可能不存在
        if (awake == null || awake.GetParameters().Length != 0) return;

        try
        {
            awake.Invoke(component, null);
        }
        catch (TargetInvocationException e)
        {
            // Awake 内部抛错（如缺引用）时，直接把真实原因抛出来，别让它退化成一个费解的断言失败
            throw new Exception($"{component.GetType().Name}.Awake() 执行异常：" +
                                $"{e.InnerException?.Message ?? e.Message}");
        }
    }

    private static void AssertTrue(bool condition, string failMessage)
    {
        if (!condition) throw new Exception(failMessage);
    }

    private static void RequireType(Type t, string typeName)
    {
        if (t == null)
        {
            throw new Exception($"类型 {typeName} 尚未落地 —— 请先完成方案 §4 对应代码");
        }
    }

    /// <summary>安全销毁：区分运行/编辑模式</summary>
    private static void SafeDestroy(UnityEngine.Object obj)
    {
        if (obj == null) return;
        UnityEngine.Object.DestroyImmediate(obj);
    }

    // ==================== 用例 ①：DamageInfo ====================

    private static IEnumerator Test_DamageInfo()
    {
        Type t = FindType("DamageInfo");
        RequireType(t, "DamageInfo");

        var go = new GameObject("~verifier_source");
        try
        {
            // 定位 Create(float, GameObject, Vector3, E_DamageSource, bool, string, float, bool)
            MethodInfo create = null;
            foreach (MethodInfo m in t.GetMethods(BindingFlags.Public | BindingFlags.Static))
            {
                if (m.Name != "Create") continue;
                ParameterInfo[] ps = m.GetParameters();
                if (ps.Length >= 3 && ps[0].ParameterType == typeof(float)) { create = m; break; }
            }
            AssertTrue(create != null, "DamageInfo.Create(...) 未找到 —— 应为 static 且首参为 float");

            object[] args = new object[create.GetParameters().Length];
            args[0] = 12.5f;              // amount
            args[1] = go;                 // source
            args[2] = Vector3.one;        // hitPoint
            for (int i = 3; i < args.Length; i++)
            {
                Type pt = create.GetParameters()[i].ParameterType;
                args[i] = pt.IsValueType ? Activator.CreateInstance(pt) : null;
            }

            object info = create.Invoke(null, args);

            float amount = GetField<float>(info, "Amount");
            AssertTrue(Mathf.Approximately(amount, 12.5f),
                $"Amount 应为 12.5，实际 {amount}");

            object parryable = GetField<object>(info, "IsParryable");
            AssertTrue((bool)parryable, "IsParryable 默认应为 true（不填即视为可弹反）");

            object attackId = GetField<object>(info, "AttackId");
            AssertTrue(attackId == null, "AttackId 未填时应为 null");
        }
        finally
        {
            SafeDestroy(go);
        }

        yield break;
    }

    // ==================== 用例 ②：Health clamp（锁死 v1 的 bug） ====================

    private static IEnumerator Test_HealthClamp()
    {
        Type t = T_Health;
        RequireType(t, "Health");
        Type damageInfoType = FindType("DamageInfo");
        RequireType(damageInfoType, "DamageInfo");

        var go = new GameObject("~verifier_health");
        try
        {
            Component health = go.AddComponent(t);
            SetField(health, "maxHealth", 25f);
            SetField(health, "refillOnEnable", true);

            // 编辑模式下 AddComponent 不会触发 Awake，必须手动调一次，否则 currentHealth 是 0
            InvokeAwake(health);

            float max = GetField<float>(health, "maxHealth");
            AssertTrue(Mathf.Approximately(max, 25f), $"最大血应为 25，实际 {max}");

            float init = GetField<float>(health, "currentHealth");
            AssertTrue(Mathf.Approximately(init, 25f),
                $"Awake 后当前血应等于最大血 25，实际 {init}（检查 Awake 里的 ResetToFull 调用）");

            // --- 受 5 点伤害 → 20 ---
            object info5 = BuildDamageInfo(damageInfoType, 5f, go);
            t.GetMethod("TakeDamage", BindingFlags.Instance | BindingFlags.Public)
             .Invoke(health, new[] { info5 });

            float afterHit = GetField<float>(health, "currentHealth");
            AssertTrue(Mathf.Approximately(afterHit, 20f),
                $"25 血受 5 伤后应为 20，实际 {afterHit}");

            // --- 治疗 3 → 必须是 23，而不是 20 ---
            // 这正是 v1 的 bug 所在：v1 写 Mathf.Clamp(Health - damage, 0, Health)，
            // 上限用了"当前血"而非"最大血"，导致受伤后最大血塌缩，任何治疗/升级都被夹死。
            // 若实现里误写成 clamp(…, 0, currentHealth)，下面这条会得到 20 → 失败。
            t.GetMethod("Heal", BindingFlags.Instance | BindingFlags.Public)
             .Invoke(health, new object[] { 3f });

            float afterHeal = GetField<float>(health, "currentHealth");
            AssertTrue(Mathf.Approximately(afterHeal, 23f),
                $"20 血治疗 3 应为 23，实际 {afterHeal}。" +
                "若为 20，说明 clamp 上限用了当前血而非最大血 —— v1 的 bug 回归了");

            // --- 超量治疗应被夹到最大血 25 ---
            t.GetMethod("Heal", BindingFlags.Instance | BindingFlags.Public)
             .Invoke(health, new object[] { 999f });

            float capped = GetField<float>(health, "currentHealth");
            AssertTrue(Mathf.Approximately(capped, 25f),
                $"超量治疗应夹到最大血 25，实际 {capped}");

            // --- 致死伤害：应夹到 0 并标记死亡 ---
            object lethal = BuildDamageInfo(damageInfoType, 999f, go);
            t.GetMethod("TakeDamage", BindingFlags.Instance | BindingFlags.Public)
             .Invoke(health, new[] { lethal });

            float dead = GetField<float>(health, "currentHealth");
            AssertTrue(Mathf.Approximately(dead, 0f), $"致死伤害后应为 0，实际 {dead}");

            PropertyInfo isDeadProp = t.GetProperty("IsDead", BindingFlags.Instance | BindingFlags.Public);
            AssertTrue(isDeadProp != null, "Health.IsDead 属性未找到");
            AssertTrue((bool)isDeadProp.GetValue(health), "致死伤害后 IsDead 应为 true");
        }
        finally
        {
            SafeDestroy(go);
        }

        yield break;
    }

    // ==================== 用例 ③：多来源无敌 ====================

    private static IEnumerator Test_InvulnerableMultiSource()
    {
        Type t = T_Health;
        RequireType(t, "Health");

        var go = new GameObject("~verifier_invuln");
        try
        {
            Component health = go.AddComponent(t);
            SetField(health, "maxHealth", 25f);
            SetField(health, "refillOnEnable", true);
            InvokeAwake(health);   // 编辑模式下必须手动触发 Awake

            MethodInfo setInv = t.GetMethod("SetInvulnerable",
                BindingFlags.Instance | BindingFlags.Public);
            AssertTrue(setInv != null, "Health.SetInvulnerable(reason, value) 未找到");

            PropertyInfo isInv = t.GetProperty("IsInvulnerable", BindingFlags.Instance | BindingFlags.Public);
            AssertTrue(isInv != null, "Health.IsInvulnerable 属性未找到");

            // 开两个来源
            setInv.Invoke(health, new object[] { "dodge_iframe", true });
            setInv.Invoke(health, new object[] { "counter", true });
            AssertTrue((bool)isInv.GetValue(health), "两个来源都开时应为无敌");

            // 关掉一个 —— 仍应无敌（这正是用 HashSet 而非 bool 的意义）
            setInv.Invoke(health, new object[] { "dodge_iframe", false });
            AssertTrue((bool)isInv.GetValue(health),
                "关掉 dodge_iframe 后仍应无敌（counter 还开着）—— " +
                "若此处失败，说明无敌用的是单个 bool，多来源会互相覆盖");

            // 都关掉 —— 不再无敌
            setInv.Invoke(health, new object[] { "counter", false });
            AssertTrue(!(bool)isInv.GetValue(health), "所有来源都关后应不再是无敌");

            // 空 reason 应被拒绝（只警告不改变状态）
            setInv.Invoke(health, new object[] { null, true });
            AssertTrue(!(bool)isInv.GetValue(health), "空 reason 不应改变无敌状态");
        }
        finally
        {
            SafeDestroy(go);
        }

        yield break;
    }

    // ==================== 用例 ④⑤：PlayerStamina ====================

    private static IEnumerator Test_StaminaInsufficient()
    {
        Type t = T_PlayerStamina;
        RequireType(t, "PlayerStamina");
        Type cfgType = FindType("StaminaConfig");
        RequireType(cfgType, "StaminaConfig");

        var go = new GameObject("~verifier_stamina");
        ScriptableObject cfg = null;
        try
        {
            Component stamina = go.AddComponent(t);
            cfg = ScriptableObject.CreateInstance(cfgType);
            SetField(cfg, "maxStamina", 100f);
            SetField(cfg, "exhaustedLockDuration", 2f);
            SetField(stamina, "config", cfg);

            // ⚠️ 顺序要紧：必须先注入 config 再调 Awake，
            //    因为 Awake 里是 current = Max，而 Max 读的是 config.maxStamina。
            InvokeAwake(stamina);

            // 初始应为满
            PropertyInfo curProp = t.GetProperty("Current", BindingFlags.Instance | BindingFlags.Public);
            float cur = (float)curProp.GetValue(stamina);
            AssertTrue(Mathf.Approximately(cur, 100f),
                $"初始耐力应为 100，实际 {cur}（检查 Awake 是否设了 current = Max）");

            MethodInfo tryConsume = t.GetMethod("TryConsume", BindingFlags.Instance | BindingFlags.Public);
            AssertTrue(tryConsume != null, "PlayerStamina.TryConsume(float) 未找到");

            // 消耗 10 → 成功，剩 90
            bool ok = (bool)tryConsume.Invoke(stamina, new object[] { 10f });
            AssertTrue(ok, "耐力 100 消耗 10 应成功");
            cur = (float)curProp.GetValue(stamina);
            AssertTrue(Mathf.Approximately(cur, 90f), $"消耗 10 后应为 90，实际 {cur}");

            // 消耗 200 → 失败，且【不扣】（这是 v1 契约里写明的行为）
            bool ok2 = (bool)tryConsume.Invoke(stamina, new object[] { 200f });
            AssertTrue(!ok2, "耐力不足时应返回 false");

            float after = (float)curProp.GetValue(stamina);
            AssertTrue(Mathf.Approximately(after, 90f),
                $"耐力不足时不应扣除，期望仍为 90，实际 {after}");
        }
        finally
        {
            SafeDestroy(go);
            if (cfg != null) SafeDestroy(cfg);
        }

        yield break;
    }

    private static IEnumerator Test_StaminaExhausted()
    {
        Type t = T_PlayerStamina;
        RequireType(t, "PlayerStamina");
        Type cfgType = FindType("StaminaConfig");
        RequireType(cfgType, "StaminaConfig");

        var go = new GameObject("~verifier_stamina2");
        ScriptableObject cfg = null;
        try
        {
            Component stamina = go.AddComponent(t);
            cfg = ScriptableObject.CreateInstance(cfgType);
            SetField(cfg, "maxStamina", 50f);
            SetField(cfg, "exhaustedLockDuration", 2f);
            SetField(stamina, "config", cfg);
            InvokeAwake(stamina);   // 先注入 config 再 Awake（Awake 里 current = Max）

            MethodInfo tryConsume = t.GetMethod("TryConsume", BindingFlags.Instance | BindingFlags.Public);
            AssertTrue(tryConsume != null, "PlayerStamina.TryConsume(float) 未找到");
            PropertyInfo isExhausted = t.GetProperty("IsExhausted", BindingFlags.Instance | BindingFlags.Public);
            PropertyInfo curProp = t.GetProperty("Current", BindingFlags.Instance | BindingFlags.Public);

            // 一次耗尽
            bool ok = (bool)tryConsume.Invoke(stamina, new object[] { 50f });
            AssertTrue(ok, "消耗满额耐力应成功（50/50）");

            float cur = (float)curProp.GetValue(stamina);
            AssertTrue(Mathf.Approximately(cur, 0f), $"耗尽后应为 0，实际 {cur}");
            AssertTrue((bool)isExhausted.GetValue(stamina), "耐力归零后 IsExhausted 应为 true");

            // 力竭锁定期间再消耗 → 必须失败
            bool ok2 = (bool)tryConsume.Invoke(stamina, new object[] { 1f });
            AssertTrue(!ok2, "力竭锁定期间应拒绝消耗（即使只需 1 点）");
        }
        finally
        {
            SafeDestroy(go);
            if (cfg != null) SafeDestroy(cfg);
        }

        yield break;
    }

    // ==================== 用例 ⑥：卡肉不得解除暂停（锁死 §6 的核心 bug） ====================

    private static IEnumerator Test_HitStopPause()
    {
        Type t = T_HitStopManager;
        RequireType(t, "HitStopManager");

        var go = new GameObject("~verifier_hitstop");
        float originalTimeScale = Time.timeScale;
        try
        {
            Component mgr = go.AddComponent(t);

            PropertyInfo instanceProp = t.GetProperty("Instance",
                BindingFlags.Static | BindingFlags.Public);
            AssertTrue(instanceProp != null, "HitStopManager.Instance 未找到");

            MethodInfo pause = t.GetMethod("Pause", BindingFlags.Instance | BindingFlags.Public);
            MethodInfo play = t.GetMethod("Play", BindingFlags.Instance | BindingFlags.Public);
            MethodInfo resume = t.GetMethod("Resume", BindingFlags.Instance | BindingFlags.Public);
            AssertTrue(pause != null, "HitStopManager.Pause(...) 未找到");
            AssertTrue(play != null, "HitStopManager.Play(...) 未找到");
            AssertTrue(resume != null, "HitStopManager.Resume() 未找到");

            // 1) 暂停
            PauseWithDefaults(pause, mgr);
            AssertTrue(Mathf.Approximately(Time.timeScale, 0f),
                $"Pause() 后 timeScale 应为 0，实际 {Time.timeScale}");

            // 2) 暂停期间请求卡肉 —— 应被忽略
            PlayWithDefaults(play, mgr);

            // 3) 等一段真实时间，让"卡肉协程若真的启动"必然结束
            yield return new AsyncWait();

            // 4) 关键断言：timeScale 必须仍是 0（暂停未被解除）
            AssertTrue(Mathf.Approximately(Time.timeScale, 0f),
                $"暂停期间触发卡肉后，timeScale 应仍为 0，实际 {Time.timeScale}。" +
                "若变成 1，说明卡肉协程保存/恢复了 prev 值 —— §6 描述的 bug 复现了");

            // 5) 恢复
            resume.Invoke(mgr, null);
            AssertTrue(Mathf.Approximately(Time.timeScale, 1f),
                $"Resume() 后 timeScale 应恢复为 1，实际 {Time.timeScale}");
        }
        finally
        {
            Time.timeScale = originalTimeScale;
            SafeDestroy(go);
        }
    }

    /// <summary>按签名长度自动补默认参数调用 Pause</summary>
    private static void PauseWithDefaults(MethodInfo pause, Component mgr)
    {
        ParameterInfo[] ps = pause.GetParameters();
        object[] args = new object[ps.Length];
        for (int i = 0; i < ps.Length; i++)
        {
            args[i] = ps[i].HasDefaultValue
                ? ps[i].DefaultValue
                : (ps[i].ParameterType.IsValueType ? Activator.CreateInstance(ps[i].ParameterType) : null);
        }
        pause.Invoke(mgr, args);
    }

    /// <summary>按签名长度自动补默认参数调用 Play（用显式短时长，保证必然结束）</summary>
    private static void PlayWithDefaults(MethodInfo play, Component mgr)
    {
        ParameterInfo[] ps = play.GetParameters();
        object[] args = new object[ps.Length];
        for (int i = 0; i < ps.Length; i++)
        {
            if (ps[i].ParameterType == typeof(float))
            {
                args[i] = 0.01f;   // 极短时长：若真启动也会很快结束，从而暴露"解除暂停"的问题
            }
            else
            {
                args[i] = ps[i].HasDefaultValue
                    ? ps[i].DefaultValue
                    : (ps[i].ParameterType.IsValueType ? Activator.CreateInstance(ps[i].ParameterType) : null);
            }
        }
        play.Invoke(mgr, args);
    }

    // ==================== DamageInfo 构造辅助 ====================

    private static object BuildDamageInfo(Type damageInfoType, float amount, GameObject source)
    {
        MethodInfo create = null;
        foreach (MethodInfo m in damageInfoType.GetMethods(BindingFlags.Public | BindingFlags.Static))
        {
            if (m.Name != "Create") continue;
            ParameterInfo[] ps = m.GetParameters();
            if (ps.Length >= 3 && ps[0].ParameterType == typeof(float)) { create = m; break; }
        }
        AssertTrue(create != null, "DamageInfo.Create(...) 未找到");

        object[] args = new object[create.GetParameters().Length];
        args[0] = amount;
        args[1] = source;
        args[2] = source != null ? source.transform.position : Vector3.zero;
        for (int i = 3; i < args.Length; i++)
        {
            Type pt = create.GetParameters()[i].ParameterType;
            args[i] = pt.IsValueType ? Activator.CreateInstance(pt) : null;
        }
        return create.Invoke(null, args);
    }
}

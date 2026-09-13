// =============================================================================
//  ARPGWarriorRuntimeProbe.cs
//  ---------------------------------------------------------------------------
//  运行期诊断：找出「角色陷到地面以下」的真实原因。
//
//  为什么必须运行期做：
//    陷地只在 Play 模式出现，是「骨骼实际位置」和「地面判定点」不一致造成的。
//    光看资源看不出来 —— 得把 Transform / 碰撞胶囊 / 骨骼的实际 Y 值打出来对比。
//
//  它会把下面这些量都打出来：
//    · 角色根节点 Transform 的 Y
//    · CharacterController 胶囊的 center / height / 底面 Y
//    · Animator 的 Avatar 是否合法、缩放多少
//    · Hips / 左右脚 / 脚趾 的**世界** Y 坐标
//    · 结论：脚是穿了地，还是悬空了，差多少
//
//  用法：进 Play 模式后按 F9（快捷键可在下方 ToggleKey 改）。
//        想开局就自动打一次，把 RunOnceOnStart 改成 true。
// =============================================================================

using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

/// <summary>
/// 运行期骨骼/碰撞体高度诊断。
/// </summary>
public class ARPGWarriorRuntimeProbe : MonoBehaviour
{
    // ------------------------------------------------------------------ 配置

    /// <summary>触发诊断的按键。</summary>
    private const KeyCode ToggleKey = KeyCode.F9;

    /// <summary>true = 进 Play 后自动诊断一次（不用按键）。</summary>
    private const bool RunOnceOnStart = false;

    /// <summary>自动诊断的延迟秒数（等角色落地稳定）。</summary>
    private const float AutoRunDelay = 2.0f;

    // -------------------------------------------------------------- 运行时

    private float startTime;

    /// <summary>
    /// 自动挂载：不需要手动往场景里加 GameObject，进 Play 就能用 F9。
    /// 不想要这个自启动行为，把下面这个特性注释掉、改成手动挂脚本即可。
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoBootstrap()
    {
        var go = new GameObject("[ARPGWarriorRuntimeProbe]");
        go.AddComponent<ARPGWarriorRuntimeProbe>();
        DontDestroyOnLoad(go);
    }

    private void Start()
    {
        startTime = Time.time;
    }

    private void Update()
    {
        if (Input.GetKeyDown(ToggleKey))
        {
            Diagnose();
        }

        if (RunOnceOnStart && Time.time - startTime >= AutoRunDelay)
        {
            // 只跑一次
            enabled = false;
            Diagnose();
        }
    }

    // ---------------------------------------------------------------- 诊断

    private static void Diagnose()
    {
        var log = new StringBuilder();
        log.AppendLine("========== 运行期骨骼高度诊断 ==========");

        // ---- 找到玩家：优先带 PlayerController 的 ----
        var player = FindPlayer();
        if (player == null)
        {
            log.AppendLine("✘ 场景里找不到玩家（没有带 PlayerController 的对象）。");
            Debug.LogWarning(log.ToString());
            return;
        }

        var t = player.transform;
        log.AppendLine($"玩家对象：{GetPath(t)}");
        log.AppendLine($"  根节点世界坐标：{t.position}");
        log.AppendLine($"  根节点缩放    ：{t.lossyScale}");

        // ---- CharacterController ----
        var cc = player.GetComponent<CharacterController>();
        if (cc != null)
        {
            float bottom = cc.center.y - cc.height * 0.5f;
            log.AppendLine();
            log.AppendLine("  CharacterController：");
            log.AppendLine($"    center      = {cc.center}");
            log.AppendLine($"    height      = {cc.height}");
            log.AppendLine($"    radius      = {cc.radius}");
            log.AppendLine($"    胶囊底面(本地Y) = {bottom:F4}");
            log.AppendLine($"    胶囊底面(世界Y) = {t.position.y + bottom * t.lossyScale.y:F4}");
            log.AppendLine($"    是否着地        = {(cc.isGrounded ? "是" : "否")}");
            log.AppendLine($"    ★ 这个「胶囊底面世界Y」才是游戏认定的脚底位置。");
        }
        else
        {
            log.AppendLine("  （没有 CharacterController）");
        }

        // ---- Animator / Avatar ----
        var animator = player.GetComponentInChildren<Animator>();
        if (animator != null)
        {
            log.AppendLine();
            log.AppendLine("  Animator：");
            log.AppendLine($"    Avatar      = {(animator.avatar != null ? animator.avatar.name : "<null>")}");

            if (animator.avatar != null)
            {
                log.AppendLine($"    isValid     = {animator.avatar.isValid}");
                log.AppendLine($"    isHuman     = {animator.avatar.isHuman}");

                // ★ 关键：Avatar 的缩放系数。
                // Unity 重定向时，髋部位置 = 动画里的 RootT × 这个系数。
                // 如果这个值异常（比如 0.05 而不是 ~1），角色就会被整体压扁到地面。
                DumpAvatarInternals(animator.avatar, log);
            }

            log.AppendLine($"    applyRootMotion = {animator.applyRootMotion}");
            log.AppendLine($"    Animator 所在对象 = {GetPath(animator.transform)}");
            log.AppendLine($"    Animator 世界Y   = {animator.transform.position.y:F4}");

            var state = animator.GetCurrentAnimatorStateInfo(0);
            log.AppendLine($"    当前状态(层0)    = hash {state.fullPathHash}  normalizedTime {state.normalizedTime:F3}");

            // ---- 关键骨骼的世界 Y ----
            log.AppendLine();
            log.AppendLine("  关键骨骼世界 Y：");

            var bones = new (HumanBodyBones Bone, string Label)[]
            {
                (HumanBodyBones.Hips,          "Hips（髋）"),
                (HumanBodyBones.Spine,         "Spine（脊）"),
                (HumanBodyBones.Head,          "Head（头）"),
                (HumanBodyBones.LeftUpperLeg,  "LeftUpperLeg"),
                (HumanBodyBones.LeftLowerLeg,  "LeftLowerLeg"),
                (HumanBodyBones.LeftFoot,      "LeftFoot（左脚）"),
                (HumanBodyBones.LeftToes,      "LeftToes（左趾）"),
                (HumanBodyBones.RightFoot,     "RightFoot（右脚）"),
                (HumanBodyBones.RightToes,     "RightToes（右趾）"),
            };

            Transform hips = null, leftToe = null, rightToe = null;
            float lowestY = float.MaxValue;
            string lowestName = "";

            foreach (var (bone, label) in bones)
            {
                var bt = SafeGetBone(animator, bone);
                if (bt == null)
                {
                    log.AppendLine($"    {label,-20} <映射不到>");
                    continue;
                }

                float y = bt.position.y;
                log.AppendLine($"    {label,-20} {y,9:F4}   (本地Y {bt.localPosition.y,8:F4})");

                if (bone == HumanBodyBones.Hips) hips = bt;
                if (bone == HumanBodyBones.LeftToes) leftToe = bt;
                if (bone == HumanBodyBones.RightToes) rightToe = bt;

                if (y < lowestY) { lowestY = y; lowestName = label; }
            }

            // ---- 结论 ----
            log.AppendLine();
            log.AppendLine("  ── 结论 ──");

            if (cc != null)
            {
                float capsuleBottom = t.position.y + (cc.center.y - cc.height * 0.5f) * t.lossyScale.y;

                float footY = float.MaxValue;
                if (leftToe != null) footY = Mathf.Min(footY, leftToe.position.y);
                if (rightToe != null) footY = Mathf.Min(footY, rightToe.position.y);

                bool haveFoot = footY < float.MaxValue;

                log.AppendLine($"    胶囊底面世界Y = {capsuleBottom:F4}");
                if (haveFoot)
                {
                    log.AppendLine($"    脚趾最低世界Y = {footY:F4}");
                    float diff = footY - capsuleBottom;
                    log.AppendLine($"    差值（脚 - 胶囊底）= {diff:F4}");

                    if (diff < -0.03f)
                    {
                        log.AppendLine($"    ✘ 脚陷进地面约 {Mathf.Abs(diff):F3} 个单位。");
                        log.AppendLine("       → 动画的髋部高度 < 模型自身髋高。重定向没配对，");
                        log.AppendLine("         需要调 Avatar 的 Hips 位置或给动画加 Y 偏移。");
                    }
                    else if (diff > 0.05f)
                    {
                        log.AppendLine($"    ✘ 脚悬空约 {diff:F3} 个单位。");
                    }
                    else
                    {
                        log.AppendLine("    ✔ 脚底与胶囊底面基本对齐，高度没问题。");
                    }
                }
                else
                {
                    log.AppendLine("    （脚趾骨映射不到，无法比对脚底高度）");
                }

                if (hips != null)
                {
                    log.AppendLine($"    Hips 世界Y = {hips.position.y:F4}");
                    log.AppendLine($"    Hips 相对根节点 = {hips.position.y - t.position.y:F4}");
                }
            }

            // ---- 骨架比例体检 ----
            // 这是判断「重定向把骨架拉变形了」的关键：
            // 人形骨骼的「骨长」应该是稳定的，如果腿被拉长/躯干被压扁，
            // 说明 Avatar 的参考姿态（reference pose）不对。
            DumpSkeletonProportions(animator, log);
        }
        else
        {
            log.AppendLine("  （找不到 Animator）");
        }

        log.AppendLine("=======================================");
        Debug.Log(log.ToString());
    }

    // ---------------------------------------------------------------- 工具

    /// <summary>
    /// 通过反射把 Avatar 内部的关键数值打出来。
    ///
    /// 重点是 humanDescription.globalScale —— Unity 重定向时：
    ///     目标髋部位置 = 动画 RootT × (目标 Avatar 尺度 / 源 Avatar 尺度)
    /// 这个值一旦不对，角色就会整体被压扁（陷地）或拉高（悬空）。
    ///
    /// Avatar 的这些人形数据没有公开的 C# API 可读，只能反射。
    /// </summary>
    private static void DumpAvatarInternals(Avatar avatar, StringBuilder log)
    {
        log.AppendLine();
        log.AppendLine("  ── Avatar 内部数值（反射读取）──");

        object humanDesc = null;

        // 1) 试 Avatar.humanDescription（部分版本可用）
        try
        {
            var p = typeof(Avatar).GetProperty("humanDescription",
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.NonPublic);

            if (p != null) humanDesc = p.GetValue(avatar);
        }
        catch { /* 忽略，下面还有别的路 */ }

        if (humanDesc != null)
        {
            var t = humanDesc.GetType();
            foreach (var f in t.GetFields(System.Reflection.BindingFlags.Instance |
                                          System.Reflection.BindingFlags.Public |
                                          System.Reflection.BindingFlags.NonPublic))
            {
                if (f.Name.Contains("Scale") || f.Name.Contains("Twist") ||
                    f.Name.Contains("Stretch") || f.Name.Contains("Spacing") ||
                    f.Name.Contains("TranslationDoF"))
                {
                    object v = null;
                    try { v = f.GetValue(humanDesc); } catch { }
                    if (v != null) log.AppendLine($"    {f.Name,-24} = {v}");
                }
            }
        }
        else
        {
            log.AppendLine("    （humanDescription 取不到，改用反射列出 Avatar 的全部数值成员）");
        }

        // 2) 兜底：把 Avatar 和它内部数据对象的所有数值成员列出来
        DumpNumericMembers(avatar, "Avatar", log, 3);

        log.AppendLine();
        log.AppendLine("    ★ 重点看有没有一个约 0.05 的值（或换算后 ≈0.05）。");
        log.AppendLine("      动画里 RootT = 0.898，实测髋部本地Y = 0.0447，");
        log.AppendLine("      0.0447 / 0.898 ≈ 0.0498 —— 这个系数就是问题所在。");
    }

    /// <summary>递归列出对象上的数值型字段/属性（最多 maxDepth 层）。</summary>
    private static void DumpNumericMembers(object obj, string label, StringBuilder log, int maxDepth)
    {
        if (obj == null || maxDepth <= 0) return;

        var t = obj.GetType();
        int shown = 0;

        var flags = System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.Public |
                    System.Reflection.BindingFlags.NonPublic;

        foreach (var f in t.GetFields(flags))
        {
            if (f.FieldType != typeof(float) && f.FieldType != typeof(double)) continue;
            object v = null;
            try { v = f.GetValue(obj); } catch { }
            if (v == null) continue;

            double dv = System.Convert.ToDouble(v);
            if (System.Math.Abs(dv) < 1e-9) continue;   // 跳过 0

            log.AppendLine($"    [{label}] {f.Name,-28} = {dv:F6}");
            shown++;
            if (shown > 40) break;
        }
    }

    /// <summary>
    /// 打印骨架各段的实际长度，用来判断重定向有没有把骨架拉变形。
    ///
    /// 原理：人形骨骼的「骨长」在动画播放期间是常量（只有旋转在变）。
    /// 如果量出来的腿长明显大于身高，或者各段比例离谱，
    /// 就说明 Avatar 的参考姿态（reference pose）有问题 —— 那是陷地的根因。
    /// </summary>
    private static void DumpSkeletonProportions(Animator animator, StringBuilder log)
    {
        log.AppendLine();
        log.AppendLine("  ── 骨架比例体检（各骨骼与其父骨骼的距离）──");

        // 父 → 子 的配对，覆盖主要骨链
        var pairs = new (HumanBodyBones Parent, HumanBodyBones Child, string Label)[]
        {
            (HumanBodyBones.Hips,         HumanBodyBones.Spine,         "髋→脊"),
            (HumanBodyBones.Spine,        HumanBodyBones.Chest,         "脊→胸"),
            (HumanBodyBones.Chest,        HumanBodyBones.Neck,          "胸→颈"),
            (HumanBodyBones.Neck,         HumanBodyBones.Head,          "颈→头"),
            (HumanBodyBones.Hips,         HumanBodyBones.LeftUpperLeg,  "髋→左大腿"),
            (HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg,  "左大腿→左小腿"),
            (HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot,      "左小腿→左脚"),
            (HumanBodyBones.LeftFoot,     HumanBodyBones.LeftToes,      "左脚→左趾"),
            (HumanBodyBones.Hips,         HumanBodyBones.RightUpperLeg, "髋→右大腿"),
            (HumanBodyBones.RightUpperLeg,HumanBodyBones.RightLowerLeg, "右大腿→右小腿"),
            (HumanBodyBones.RightLowerLeg,HumanBodyBones.RightFoot,     "右小腿→右脚"),
            (HumanBodyBones.RightFoot,    HumanBodyBones.RightToes,     "右脚→右趾"),
            (HumanBodyBones.Chest,        HumanBodyBones.LeftShoulder,  "胸→左肩"),
            (HumanBodyBones.LeftShoulder, HumanBodyBones.LeftUpperArm,  "左肩→左上臂"),
            (HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm,  "左上臂→左前臂"),
            (HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand,      "左前臂→左手"),
        };

        var got = new Dictionary<string, Transform>();
        foreach (var (p, c, label) in pairs)
        {
            var pt = SafeGetBone(animator, p);
            var ct = SafeGetBone(animator, c);
            got[label] = ct;

            if (pt == null || ct == null)
            {
                log.AppendLine($"    {label,-18} <映射不到>");
                continue;
            }

            float len = Vector3.Distance(pt.position, ct.position);
            log.AppendLine($"    {label,-18} {len,8:F4}");
        }

        // ---- 汇总：腿总长 vs 躯干高度 ----
        float LegTotal(Animator a, HumanBodyBones upper, HumanBodyBones lower, HumanBodyBones foot, HumanBodyBones toes)
        {
            var t1 = SafeGetBone(a, upper);
            var t2 = SafeGetBone(a, lower);
            var t3 = SafeGetBone(a, foot);
            var t4 = SafeGetBone(a, toes);
            if (t1 == null || t2 == null || t3 == null) return -1f;

            float s = Vector3.Distance(t1.position, t2.position)
                    + Vector3.Distance(t2.position, t3.position);
            if (t4 != null) s += Vector3.Distance(t3.position, t4.position);
            return s;
        }

        float leftLeg = LegTotal(animator, HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg,
                                          HumanBodyBones.LeftFoot, HumanBodyBones.LeftToes);
        float rightLeg = LegTotal(animator, HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg,
                                           HumanBodyBones.RightFoot, HumanBodyBones.RightToes);

        var hipsT = SafeGetBone(animator, HumanBodyBones.Hips);
        var headT = SafeGetBone(animator, HumanBodyBones.Head);

        log.AppendLine();
        if (leftLeg > 0f || rightLeg > 0f)
        {
            log.AppendLine($"    左腿总长（大腿+小腿+脚+趾）= {leftLeg:F4}");
            log.AppendLine($"    右腿总长                    = {rightLeg:F4}");
        }

        if (hipsT != null && headT != null)
        {
            float torso = Vector3.Distance(hipsT.position, headT.position);
            log.AppendLine($"    髋→头 距离                  = {torso:F4}");

            float leg = Mathf.Max(leftLeg, rightLeg);
            if (leg > 0f)
            {
                log.AppendLine($"    ★ 腿长 / 髋头距 = {leg / torso:F3}   （正常人形约 1.3 ~ 1.7）");
                if (leg / torso > 2.2f)
                {
                    log.AppendLine("      → 比例严重异常：腿被拉长了。Avatar 参考姿态有问题。");
                }
            }
        }

        log.AppendLine("    提示：把这份数字发出来，就能判断是 Avatar 参考姿态错了，");
        log.AppendLine("          还是单纯动画的髋部高度不匹配。");

        // ---- 根骨缩放实验：判断是不是「骨骼被压缩」----
        // 如果整条骨链的段长都被同比例压缩了，那单纯加 Y 偏移是不够的
        // （人物会变得很矮），需要缩放根骨或修 Avatar。
        var armRoot = animator.transform;
        var hipsBone = SafeGetBone(animator, HumanBodyBones.Hips);
        if (hipsBone != null && hipsBone != armRoot)
        {
            // 找 Hips 到 Animator 之间的第一层（通常就是 Armature）
            var first = hipsBone;
            while (first.parent != null && first.parent != armRoot) first = first.parent;

            log.AppendLine();
            log.AppendLine($"    根骨候选：{first.name}");
            log.AppendLine($"      当前 localScale = {first.localScale}");

            if (hipsT != null && headT != null)
            {
                float torso = Vector3.Distance(hipsT.position, headT.position);
                float leg = Mathf.Max(leftLeg, rightLeg);
                if (leg > 0f && torso > 0f)
                {
                    // 正常人形：身高 1.85 的角色，髋到脚（含趾）约 0.85~0.95
                    log.AppendLine();
                    log.AppendLine($"      参考：身高 1.85 的角色，腿长应约 0.85~0.95");
                    log.AppendLine($"      实测腿长 {leg:F3} → 估计需要放大 {0.90f / leg:F2} 倍左右");
                    log.AppendLine("      但注意：如果只是加根骨缩放，脚底对齐仍要另外调 Y 偏移。");
                }
            }
        }
    }

    /// <summary>找场景里的玩家对象。</summary>
    private static GameObject FindPlayer()
    {
        // 按 PlayerController 找（最可靠）
        var pc = Object.FindObjectOfType<PlayerController>();
        if (pc != null) return pc.gameObject;

        // 退路：按 Animator + CharacterController 的组合找
        foreach (var cc in Object.FindObjectsOfType<CharacterController>())
        {
            if (cc.GetComponentInChildren<Animator>() != null)
            {
                return cc.gameObject;
            }
        }

        return null;
    }

    /// <summary>Avatar 有时会抛异常，包一层。</summary>
    private static Transform SafeGetBone(Animator animator, HumanBodyBones bone)
    {
        try
        {
            return animator.GetBoneTransform(bone);
        }
        catch
        {
            return null;
        }
    }

    private static string GetPath(Transform t)
    {
        var parts = new List<string>();
        var cur = t;
        while (cur != null) { parts.Add(cur.name); cur = cur.parent; }
        parts.Reverse();
        return string.Join("/", parts);
    }
}

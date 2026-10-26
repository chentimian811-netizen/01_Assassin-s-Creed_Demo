using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

/// <summary>
/// 一键导出与校验：Excel → Luban → 生成 C#/JSON → AssetDatabase 刷新 → 引用校验。
///
/// 【为什么导出失败必须中止】
///   Luban 生成器会清空代码与数据输出目录，失败时容易留下不完整的生成物。
///   如果继续往下走，Unity 会带着半套配置进入编译/Play，症状会非常难查。
///   所以这里一旦退出码非 0 就立刻返回，把 Luban 的输出原样打出来。
///
/// 【用法】Unity 菜单 ACDemo/Config/Export And Validate（或 batchmode 调用 ExportAndValidate）。
/// </summary>
public static class ConfigExportMenu
{
    /// <summary>导出脚本相对工程根目录的路径</summary>
    private const string BatRelativePath = "Config/Luban/gen_gameplay_json.bat";

    /// <summary>导出超时（毫秒）。单表也要几秒，留足余量给首次冷启动的 dotnet</summary>
    private const int TimeoutMilliseconds = 180000;

    [MenuItem("ACDemo/Config/Export And Validate", priority = 0)]
    public static void ExportAndValidate()
    {
        if (!RunExport(out string output))
        {
            Debug.LogError("[ConfigExportMenu] Luban 导出失败，已中止后续引用校验（不自动进入 Play）。\n" + output);
            ShowDialog("Luban 导出失败", "修表后重新导出；失败时不会继续校验。详情见 Console。");
            return;
        }

        Debug.Log("[ConfigExportMenu] Luban 导出成功，开始刷新资源并校验引用\n" + output);

        // 生成物是直接写到 Assets 里的，必须强制刷新，否则校验读到的是旧资源
        AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
        DataRepository.ResetForReload();

        bool ok = ConfigReferenceValidator.ValidateAndLog();
        if (ok)
        {
            ShowDialog("导出并校验通过", "生成 C#/JSON 已刷新，引用校验全部通过。");
        }
        else
        {
            ShowDialog("导出成功但校验失败", "数值已生成，但引用校验报错。请按 Console 里的路径与 ID 逐条修复。");
        }
    }

    [MenuItem("ACDemo/Config/Export Only", priority = 1)]
    public static void ExportOnly()
    {
        if (RunExport(out string output))
        {
            Debug.Log("[ConfigExportMenu] Luban 导出成功\n" + output);
            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
            DataRepository.ResetForReload();
            ShowDialog("导出成功", "生成 C#/JSON 已刷新（未做引用校验）。");
        }
        else
        {
            Debug.LogError("[ConfigExportMenu] Luban 导出失败\n" + output);
            ShowDialog("Luban 导出失败", "详情见 Console。");
        }
    }

    /// <summary>
    /// 运行导出批处理并收集输出。返回是否成功（退出码 0）。
    /// 用 cmd /c 是因为 Process 不能直接执行 .bat；工作目录交给脚本自己 cd，避免路径写死。
    /// </summary>
    private static bool RunExport(out string output)
    {
        string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        string batPath = Path.Combine(projectRoot, BatRelativePath);
        if (!File.Exists(batPath))
        {
            output = $"未找到导出脚本：{batPath}";
            return false;
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = "/c \"\"" + batPath + "\"\"",
            WorkingDirectory = projectRoot,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };

        var captured = new StringBuilder();
        try
        {
            using (var process = new Process())
            {
                process.StartInfo = startInfo;
                // 异步读取，避免 stdout/stderr 缓冲区写满导致子进程卡死
                process.OutputDataReceived += (sender, args) =>
                {
                    if (args.Data == null) return;
                    lock (captured) captured.AppendLine(args.Data);
                };
                process.ErrorDataReceived += (sender, args) =>
                {
                    if (args.Data == null) return;
                    lock (captured) captured.AppendLine(args.Data);
                };

                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                if (!process.WaitForExit(TimeoutMilliseconds))
                {
                    try { process.Kill(); }
                    catch (Exception e) { Debug.LogWarning($"[ConfigExportMenu] 超时后结束导出进程失败：{e.Message}"); }
                    lock (captured) captured.AppendLine($"[ConfigExportMenu] 导出超时（>{TimeoutMilliseconds}ms）");
                    output = captured.ToString();
                    return false;
                }

                // WaitForExit(ms) 返回后还要再等一次，确保异步输出全部读完
                process.WaitForExit();

                output = captured.ToString();
                if (process.ExitCode != 0)
                {
                    return false;
                }
                return true;
            }
        }
        catch (Exception e)
        {
            output = $"启动导出脚本失败：{e}";
            return false;
        }
    }

    /// <summary>batchmode 下没有可交互窗口，弹窗会阻塞，必须跳过</summary>
    private static void ShowDialog(string title, string message)
    {
        if (Application.isBatchMode) return;
        EditorUtility.DisplayDialog(title, message, "好");
    }
}

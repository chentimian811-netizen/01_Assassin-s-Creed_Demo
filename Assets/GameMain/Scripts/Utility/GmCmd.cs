using System;
using System.Collections;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
using static PackageLocalData;

public class GmCmd
{
    #if UNITY_EDITOR
    [MenuItem("GmCmd/读取表格")]
    #endif
    public static void ReadTable()
    {
        foreach (var kv in DataRepository.ItemTable)
        {
           Debug.Log($"【id】: {kv.Key},【name】:{kv.Value.Name}");
        }
    }
    #if UNITY_EDITOR
    [MenuItem("GmCmd/创建背包测试数据")]
    #endif
    public static void CreateLocalPackageData()
    {
        PackageLocalData.Instance.items = new List<PackageLocalItem>();
        for(int i = 1; i <= 3; i++)
        {
            PackageLocalItem packageLocalItem = new()
            {
                uid = Guid.NewGuid().ToString(),
                id = i,
                num = i,
                level = i,
                isNew = i / 2 == 1
            };
            PackageLocalData.Instance.items.Add(packageLocalItem);
        }
        PackageLocalData.Instance.SavePackage();
    }
    #if UNITY_EDITOR
    [MenuItem("GmCmd/读取背包测试数据")]
    #endif
    public static void ReadLocalPackageData()
    {
        List<PackageLocalItem> readitems = PackageLocalData.Instance.LoadPackage();
        foreach (PackageLocalItem item in readitems)
        {
            Debug.Log(item);
        }
    }
    #if UNITY_EDITOR
    [MenuItem("GmCmd/打开背包主界面")]
    #endif
    public static void OpenPackagePanel()
    {
        #if UNITY_EDITOR
        // GF 的 UI 组件只在运行时存在，编辑器里直接调 UIRouter 会拿不到 GameEntry.UI
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[GmCmd] 打开背包需要在 Play 状态下执行");
            return;
        }
        #endif

        UIRouter.Open(UIPaths.PackageForm, "Popup");
    }

    /// <summary>
    /// 打印 UI 层状态快照。GF-2 验收用：
    ///   - 连续开关 5 次后，「已打开界面」「UI 暂停」「光标锁」都应回到 0；
    ///   - 同时开背包 + 商店时，计数应为 2；关掉一个应变成 1（不是 0）。
    /// </summary>
    #if UNITY_EDITOR
    [MenuItem("GmCmd/UI 状态快照（暂停/光标/已开界面）")]
    #endif
    public static void DumpUIState()
    {
        HitStopManager hitStop = HitStopManager.Instance;
        bool paused = hitStop != null && hitStop.IsPaused;
        bool hitStopping = hitStop != null && hitStop.IsHitStopping;

        Debug.Log($"[UI 状态] timeScale={Time.timeScale:F2} 暂停={paused} 卡肉={hitStopping} " +
                  $"IsGameplayFocused={CursorManager.Instance.IsGameplayFocused}");
        Debug.Log($"[UI 状态] {UIPauseService.GetDebugInfo()}");
        Debug.Log($"[UI 状态] {UICursorService.GetDebugInfo()}");
        Debug.Log($"[UI 状态] {UIRouter.GetDebugInfo()}");
    }

    #if UNITY_EDITOR
    [MenuItem("GmCmd/创建双持测试数据（剑10001+盾10008）")]
    #endif
    public static void CreateDualWieldPackageData()
    {
        var existing = PackageLocalData.Instance.LoadPackage();
        if (existing == null) existing = new List<PackageLocalItem>();
        PackageLocalData.Instance.items = existing;

        EnsureItem(10001);
        EnsureItem(10008);
        PackageLocalData.Instance.SavePackage();
        Debug.Log("[GmCmd] 双持测试数据已写入：10001 剑 + 10008 盾");
    }

    static void EnsureItem(int itemId)
    {
        foreach (var it in PackageLocalData.Instance.items)
        {
            if (it != null && it.id == itemId) return;
        }
        PackageLocalData.Instance.items.Add(new PackageLocalItem
        {
            uid = Guid.NewGuid().ToString(),
            id = itemId,
            num = 1,
            level = 1,
            isNew = true,
            isEquipped = false
        });
    }

    #if UNITY_EDITOR
    [MenuItem("GmCmd/Gate：装备剑（右手）+盾（左手）")]
    #endif
    public static void EquipDualWieldForGate()
    {
        CreateDualWieldPackageData();

        int swordId = 10001, shieldId = 10008;
        string swordUid = null, shieldUid = null;
        foreach (var it in PackageLocalData.Instance.LoadPackage())
        {
            if (it == null) continue;
            if (it.id == swordId) swordUid = it.uid;
            if (it.id == shieldId) shieldUid = it.uid;
        }

        var im = UnityEngine.Object.FindObjectOfType<InventoryManager>();
        if (im == null)
        {
            Debug.LogWarning("[GmCmd] 场景中无 InventoryManager，请先 Play 再执行");
            return;
        }
        if (GameManager.Instance == null)
        {
            Debug.LogWarning("[GmCmd] GameManager.Instance 为空：场景中可能缺少 GameManager，或其未 Awake");
        }
        if (DataRepository.ItemTable == null)
        {
            Debug.LogWarning("[GmCmd] DataRepository.ItemTable 为空，尝试直接 Initialize…");
            DataRepository.Initialize();
        }
        if (DataRepository.ItemTable == null)
        {
            Debug.LogError("[GmCmd] DataRepository.Initialize 后 ItemTable 仍为空，装备类型解析会退化为默认 Sword");
        }

        if (!string.IsNullOrEmpty(swordUid))
        {
            bool ok = im.EquipWeapon(swordUid);
            Debug.Log($"[GmCmd] 装备剑 10001 → {(ok ? "成功" : "失败")}");
        }
        if (!string.IsNullOrEmpty(shieldUid))
        {
            bool ok = im.EquipWeapon(shieldUid);
            Debug.Log($"[GmCmd] 装备盾 10008 → {(ok ? "成功" : "失败")}");
        }
        Debug.Log("[GmCmd] Gate 装备流程结束：请检查右手剑 + 左手盾是否同时可见，盾 Collider.enabled 应为 false");
    }
}
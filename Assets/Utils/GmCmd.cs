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
        PackageTables packageTables = Resources.Load<PackageTables>("TableDate/PackageTable");
        foreach (PackageTableItem packageItem in packageTables.DataList)
        {
            Debug.Log(string.Format("【id】: {0},【name】:{1}", packageItem.id, packageItem.name));
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
        UIManager.Instance.OpenPanel(UIconst.PackagePanel);
    }
}
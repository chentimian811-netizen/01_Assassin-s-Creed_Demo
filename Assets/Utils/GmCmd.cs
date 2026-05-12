using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using static PackageLocalData;

public class GmCmd
{
    [MenuItem("GmCmd/读取表格")]

    public static void ReadTable()
    {
        PackageTables packageTables = Resources.Load<PackageTables>("TableDate/PackageTable");

        foreach (PackageTableItem packageItem in packageTables.DataList)
        {
            Debug.Log(string.Format("【id】: {0},【name】:{1}", packageItem.id, packageItem.name));
        }
    }


    [MenuItem("GmCmd/创建背包测试数据")]
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

    [MenuItem("GmCmd/读取背包测试数据")]
    public static void ReadLocalPackageData()
    {
        List<PackageLocalItem> readitems = PackageLocalData.Instance.LoadPackage();
        foreach (PackageLocalItem item in readitems)
        {
            Debug.Log(item);
        }
    }

    [MenuItem("GmCmd/打开背包主界面")]
    public static void OpenPackagePanel()
    {
        UIManager.Instance.OpenPanel(UIconst.PackagePanel);
    }
}

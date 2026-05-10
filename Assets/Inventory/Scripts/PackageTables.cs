using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName ="Package/PackageTables", fileName = "PackageTables")]
public class PackageTables : ScriptableObject
{
    public List<PackageTableItem> DataList = new List<PackageTableItem>();
}

[System.Serializable]
public class PackageTableItem//物体的静态数据
{
    public int id;

    public int type;

    public int star;

    public string name;

    public string description;

    public string skillDescription;

    public string imagePath;

    public int Num;
}

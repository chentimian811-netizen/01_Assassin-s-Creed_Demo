using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static PackageLocalData;

public enum E_SortMode
{
    Default,
    ByLevel,
    ByName,
    ByAcquisition
}


public class PackageItemComparer : IComparer<PackageLocalItem>
{
    public E_SortMode sorMode = E_SortMode.Default;

    public int Compare(PackageLocalItem a,PackageLocalItem b)
    {
        if(a.isEquipped != b.isEquipped)
            return a.isEquipped ?-1:1;

        DRItem x = GameManager.Instance.GetPackageItemById(a.id);
        DRItem y = GameManager.Instance.GetPackageItemById(b.id);

        switch (sorMode)
        {
            case E_SortMode.Default:
                return CompareDefault(x,y,a,b);
            case E_SortMode.ByLevel:
                return CompareByLevel(x,y,a,b);
            case E_SortMode.ByName:
                return CompareByName(x,y);
            case E_SortMode.ByAcquisition:
                return CompareByAcquisition(x,y,a,b);
            default:
                return 0;
        }
    }   
    int CompareDefault(DRItem x,DRItem y,PackageLocalItem a,PackageLocalItem b)
    {
        int star = y.Star.CompareTo(x.Star);
        if(star != 0)return star;
        int id = y.Id.CompareTo(x.Id);
        if(id != 0)return id ;
        return b.level.CompareTo(a.level);   
    }
    int CompareByLevel(DRItem x,DRItem y,PackageLocalItem a,PackageLocalItem b)
    {
        int Level = b.level.CompareTo(a.level);
        if(Level !=0)return Level;
        int star = y.Star.CompareTo(x.Star);
        if(star!=0)return star;
        return y.Id.CompareTo(x.Id);
    }
    int CompareByName(DRItem x,DRItem y)
    {
        string na = x?.Name ?? "";
        string nb = y?.Name ?? "";
        return string.Compare(na,nb);
    }
    int CompareByAcquisition(DRItem x,DRItem y,PackageLocalItem a,PackageLocalItem b)
    {
        int id = y.Id.CompareTo(x.Id);
        if(id!= 0)return id;
        return b.level.CompareTo(a.level);
    }
}

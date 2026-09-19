using UnityEngine;

[System.Serializable]
public class WeaponSlot
{
    public string slotName;

    public Transform holdPoint;

    // 单值 → 数组：一个槽位可接受多种武器类型（双持地基，步骤 2）
    // ⚠️ Unity 序列化字段类型变更会清空 prefab 上的旧值；Male.prefab 的
    //    allowedTypes 变空是预期的，步骤 3 在 prefab 上重新勾选
    public E_WeaponType[] allowedTypes;

    [HideInInspector] public WeaponConfig currentConfig;

    [HideInInspector] public GameObject currentModel;

    [HideInInspector] public string equippedUid;

    /// <summary>该槽位是否接受指定武器类型</summary>
    public bool Accepts(E_WeaponType type)
    {
        if (allowedTypes == null || allowedTypes.Length == 0) return false;
        for (int i = 0; i < allowedTypes.Length; i++)
        {
            if (allowedTypes[i] == type) return true;
        }
        return false;
    }
}

using UnityEngine;

[System.Serializable]
public class WeaponSlot
{
    public string slotName;

    public Transform holdPoint;

    public E_WeaponType allowedType;

    [HideInInspector] public WeaponConfig currentConfig;

    [HideInInspector] public GameObject currentModel;

    [HideInInspector] public string equippedUid;
}

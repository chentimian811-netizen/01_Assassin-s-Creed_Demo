using JetBrains.Annotations;
using System.Collections;
using System.Collections.Generic;
using System.Xml.Schema;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem;

public class CombatController : MonoBehaviour
{
    MeleeFighter meleeFighter;

   

    private void Awake()
    {
        meleeFighter = GetComponent<MeleeFighter>();
    }

    private void Update()
    {
        
    }

}

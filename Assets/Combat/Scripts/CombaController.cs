using JetBrains.Annotations;
using System.Collections;
using System.Collections.Generic;
using System.Xml.Schema;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem;

public class CombaController : MonoBehaviour
{
    MeeleFighter meeleFighter;

   

    private void Awake()
    {
        meeleFighter = GetComponent<MeeleFighter>();
    }

    private void Update()
    {
        
    }

}

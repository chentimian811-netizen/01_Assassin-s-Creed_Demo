using System.Collections;
using System.Collections.Generic;
using UnityEngine;
 
public class VersionSensor : MonoBehaviour
{
    [SerializeField] EnemyController enemy;

    private void Awake()
    {
        enemy.VersionSensor = this;
    }
    
    private void OnTriggerEnter(Collider other)
    {
        var fighter = other.GetComponent<MeeleFighter>();
        if (fighter != null)
        {
            enemy.TargetsInRange.Add(fighter);
            EnemyManager.i.AddEnemyInRange(enemy);
        }

    }

    private void OnTriggerExit(Collider other)
    {
        var fighter = other.GetComponent<MeeleFighter>();
        if (fighter != null)
        {
            enemy.TargetsInRange.Remove(fighter);
            EnemyManager.i.RemoveEnemyInRange(enemy);
        }
    }
}

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum E_WeaponType
{
    Sword,
    Dagger,
    Axe,
    Bow,
    Staff,

    // 盾：不是武器，但走武器通道（装备、动作集、判定都复用武器那一套）
    // ⚠️ 必须追加在末尾：Item.txt 的 WeaponType 列是整数，插值会让整列语义偏移
    Shield,
}

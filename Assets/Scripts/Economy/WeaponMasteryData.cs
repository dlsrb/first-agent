using System.Collections.Generic;
using UnityEngine;

// [C-4] 무기 숙련도 규칙 데이터

[System.Serializable]
public class TraitTier
{
    [Tooltip("이 숙련도 레벨에 도달하면 둘 중 하나를 고른다")]
    public int level = 10;
    public TraitData optionA;
    public TraitData optionB;
}

// 무기 유형 1개의 숙련도 규칙 (에셋 1개, 첫 출시는 주사위만)
[CreateAssetMenu(fileName = "Mastery_New", menuName = "요원/성장/무기 숙련도 (WeaponMasteryData)")]
public class WeaponMasteryData : ScriptableObject
{
    public WeaponType weaponType = WeaponType.Dice;
    public string displayName = "주사위";
    [Tooltip("공격 1회당 숙련도 경험치")]
    public int expPerAttack = 1;
    [Tooltip("레벨당 필요 경험치")]
    public int expPerLevel = 12;
    public int maxLevel = 30;
    [Tooltip("레벨당 치명 확률")]
    public float critPerLevel = 0.005f;
    [Tooltip("이 레벨마다 공격 +1")]
    public int attackEveryLevels = 5;
    public List<TraitTier> traitTiers = new List<TraitTier>();
}

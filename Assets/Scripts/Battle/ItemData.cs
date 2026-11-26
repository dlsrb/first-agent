using System.Collections.Generic;
using UnityEngine;

// 값 순서는 에셋에 숫자로 저장되므로 바꾸지 말고 끝에만 추가한다
public enum ItemType
{
    Consumable,
    Weapon,
    Armor,
    Special,
    Material   // C-1: 강화석 등 강화 재료
}

public enum ItemGrade
{
    Common,    // 일반: 스탯만
    Advanced,  // 고급: 무기 스킬 1개
    Rare       // 희귀: 무기 스킬 2개
}

public enum WeaponType
{
    Dice       // 첫 출시 유일한 무기 계열
}

// 아이템 (에셋 1개 = 아이템 1종). 소비·재료는 개수로, 무기·방어구는 한 개씩(강화 단계 포함) 보관한다.
[CreateAssetMenu(fileName = "Item_New", menuName = "요원/아이템 (ItemData)")]
public class ItemData : ScriptableObject
{
    public string itemId = "";
    public string itemName = "[아이템 이름 미정]";

    [TextArea(2, 4)]
    public string description = "[아이템 설명 미정]";

    public Sprite icon;

    public ItemType type = ItemType.Consumable;
    public ItemGrade grade = ItemGrade.Common;

    [Header("가격 (C-1)")]
    [Tooltip("구매가. 0이면 상점에서 팔지 않는 아이템")]
    public int price = 0;
    [Tooltip("판매가. 0이면 구매가 × EconomySettings 판매 비율")]
    public int sellPriceOverride = 0;

    [Header("소비 아이템")]
    public int healHp = 0;
    public int healEnergy = 0;

    [Header("무기")]
    public WeaponType weaponType = WeaponType.Dice;
    public int attackBonus = 0;
    [Range(0, 1)] public float critBonus = 0f;
    [Tooltip("무기 스킬 (일반 0개, 고급 1개, 희귀 2개)")]
    public List<SkillData> skills = new List<SkillData>();
    [Tooltip("강화 1단계당 공격")]
    public int attackPerEnhance = 1;

    [Header("방어구")]
    public int defenseBonus = 0;
    [Tooltip("강화 1단계당 방어")]
    public int defensePerEnhance = 2;

    public bool IsEquipment => type == ItemType.Weapon || type == ItemType.Armor;

    public static int SkillSlotsFor(ItemGrade grade)
    {
        return grade == ItemGrade.Common ? 0 : grade == ItemGrade.Advanced ? 1 : 2;
    }

    void OnValidate()
    {
        if (type == ItemType.Weapon && skills.Count != SkillSlotsFor(grade))
        {
            Debug.LogWarning(name + ": " + grade + " 무기의 스킬 수는 " + SkillSlotsFor(grade) + "개여야 합니다 (현재 " + skills.Count + "개).", this);
        }
    }
}

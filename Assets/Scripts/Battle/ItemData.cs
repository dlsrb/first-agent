using UnityEngine;

public enum ItemType
{
    Consumable,
    Weapon,
    Armor,
    Special
}

// 아이템 (에셋 1개 = 아이템 1종). B단계는 소비 아이템(음식)만 사용, 장비 칸은 C단계에서 사용.
[CreateAssetMenu(fileName = "Item_New", menuName = "요원/아이템 (ItemData)")]
public class ItemData : ScriptableObject
{
    public string itemId = "";
    public string itemName = "[아이템 이름 미정]";

    [TextArea(2, 4)]
    public string description = "[아이템 설명 미정]";

    public Sprite icon;

    public ItemType type = ItemType.Consumable;

    [Header("소비 아이템")]
    public int healHp = 0;
    public int healEnergy = 0;

    [Header("장비 (C단계)")]
    public int attackBonus = 0;
    public int defenseBonus = 0;
}

using System;

// 주인공의 계산된 수치를 한곳에서 다시 채운다 (설계안 2번).
// 기본값(BattleSettings.playerBase) + 장착 장비 + 강화. C-4에서 스탯·재능·숙련도를 여기에 더한다.
// 장착·강화·레벨 업 등 원본이 바뀔 때마다, 그리고 전투 시작 전에 호출한다.
public static class PlayerStats
{
    public static void Recalculate(PlayerState p, BattleSettings settings, GameDatabase db)
    {
        if (p == null || settings == null)
        {
            return;
        }

        var b = settings.playerBase;

        p.maxHp = b.maxHp;
        p.maxEnergy = b.maxEnergy;
        p.attack = b.attack;
        p.defense = b.defense;
        p.critChance = b.critChance;
        p.agility = b.agility;
        p.diceCount = b.diceCount;
        p.diceFaces = b.diceFaces;
        p.skillIds.Clear();

        foreach (var skill in b.startSkills)
        {
            if (skill != null) p.skillIds.Add(skill.skillId);
        }

        var weapon = EquippedItem(p, p.equippedWeaponUid, db, ItemType.Weapon);

        if (weapon != null)
        {
            int enhance = p.FindEquipment(p.equippedWeaponUid).enhance;
            p.attack += weapon.attackBonus + weapon.attackPerEnhance * enhance;
            p.critChance += weapon.critBonus;

            foreach (var skill in weapon.skills)
            {
                if (skill != null && !p.skillIds.Contains(skill.skillId)) p.skillIds.Add(skill.skillId);
            }
        }

        var armor = EquippedItem(p, p.equippedArmorUid, db, ItemType.Armor);

        if (armor != null)
        {
            int enhance = p.FindEquipment(p.equippedArmorUid).enhance;
            p.defense += armor.defenseBonus + armor.defensePerEnhance * enhance;
        }

        p.critChance = Math.Min(1f, Math.Max(0f, p.critChance));
        p.currentHp = Math.Min(p.currentHp, p.maxHp);
        p.currentEnergy = Math.Min(p.currentEnergy, p.maxEnergy);
    }

    // 장착 칸의 아이템 (종류가 맞지 않으면 null)
    public static ItemData EquippedItem(PlayerState p, int uid, GameDatabase db, ItemType type)
    {
        var owned = p.FindEquipment(uid);
        var item = owned != null && db != null ? db.FindItem(owned.itemId) : null;
        return item != null && item.type == type ? item : null;
    }

    // 장착 (무기·방어구 칸은 아이템 종류로 정한다). 성공하면 수치를 다시 계산
    public static bool Equip(PlayerState p, int uid, BattleSettings settings, GameDatabase db)
    {
        var owned = p.FindEquipment(uid);
        var item = owned != null && db != null ? db.FindItem(owned.itemId) : null;

        if (item == null || !item.IsEquipment)
        {
            return false;
        }

        if (item.type == ItemType.Weapon) p.equippedWeaponUid = uid;
        else p.equippedArmorUid = uid;

        Recalculate(p, settings, db);
        return true;
    }

    // [C-3] 해제 (무기·방어구 칸)
    public static void Unequip(PlayerState p, ItemType slot, BattleSettings settings, GameDatabase db)
    {
        if (slot == ItemType.Weapon) p.equippedWeaponUid = 0;
        else if (slot == ItemType.Armor) p.equippedArmorUid = 0;
        Recalculate(p, settings, db);
    }
}

using System;

// 주인공의 계산된 수치를 한곳에서 다시 채운다 (설계안 2번).
// 기본값(BattleSettings.playerBase) + 스탯(찍은 포인트 + 재능) + 재능 + 장착 장비·강화 + 무기 숙련도·특성.
// 성장 규칙은 GameDatabase.economy (비어 있으면 장비만 계산 — 테스트용).
// 장착·강화·레벨 업·스탯·재능·특성이 바뀔 때마다, 그리고 전투 시작 전에 호출한다.
public static class PlayerStats
{
    public static void Recalculate(PlayerState p, BattleSettings settings, GameDatabase db)
    {
        if (p == null || settings == null)
        {
            return;
        }

        var b = settings.playerBase;
        var eco = db != null ? db.economy : null;

        p.maxHp = b.maxHp;
        p.maxEnergy = b.maxEnergy;
        p.attack = b.attack;
        p.defense = b.defense;
        p.critChance = b.critChance;
        p.agility = b.agility;
        p.diceCount = b.diceCount;
        p.diceFaces = b.diceFaces;
        p.skillIds.Clear();
        p.debuffResist = p.infoChance = p.shopDiscount = p.expBonus = p.dropBonus = p.enhanceDiscount = p.comboBonus = 0f;
        p.critExtraDie = false;

        foreach (var skill in b.startSkills)
        {
            if (skill != null) p.skillIds.Add(skill.skillId);
        }

        // ---- [C-4] 스탯 + 재능 ----
        var total = p.stats.Copy();
        float talentDebuff = 0f;

        if (eco != null)
        {
            foreach (var id in p.talentIds)
            {
                var t = eco.FindTalent(id);
                if (t == null) continue;

                foreach (var m in t.effects)
                {
                    switch (m.target)
                    {
                        case ModTarget.MaxHp: p.maxHp += (int)m.value; break;
                        case ModTarget.Attack: p.attack += (int)m.value; break;
                        case ModTarget.CritPoints: p.critChance += m.value / 100f; break;
                        case ModTarget.AllStats:
                            // 매력은 히든 스탯이라 제외 (힘·민첩·정신력·지능)
                            foreach (StatType st in Enum.GetValues(typeof(StatType))) if (st != StatType.Charm) total.Add(st, (int)m.value);
                            break;
                        case ModTarget.EnhanceGoldPercent: p.enhanceDiscount += m.value / 100f; break;
                        case ModTarget.DropRatePercent: p.dropBonus += m.value / 100f; break;
                        case ModTarget.ExpPercent: p.expBonus += m.value / 100f; break;
                        case ModTarget.DebuffResistPoints: talentDebuff += m.value / 100f; break;
                    }
                }
            }

            p.attack += total.strength * eco.attackPerStrength;
            p.agility += total.agility;
            p.critChance += total.agility * eco.critPerAgility;
            p.maxHp += total.spirit * eco.hpPerSpirit;
            p.debuffResist = Math.Min(eco.debuffResistMax, total.spirit * eco.debuffResistPerSpirit) + talentDebuff;
            p.maxEnergy += total.intellect * eco.energyPerIntellect;
            p.infoChance = total.intellect * eco.infoChancePerIntellect;
            p.shopDiscount = Math.Min(eco.discountMax, total.charm * eco.discountPerCharm);
        }

        p.totalStats = total;

        // ---- 장비 ----
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

            // ---- [C-4] 장착 무기 유형의 숙련도·특성 ----
            var rule = eco != null ? eco.MasteryFor(weapon.weaponType) : null;

            if (rule != null)
            {
                var m = p.MasteryOf(weapon.weaponType);
                p.critChance += m.level * rule.critPerLevel;
                if (rule.attackEveryLevels > 0) p.attack += m.level / rule.attackEveryLevels;

                foreach (var id in m.traitIds)
                {
                    var tr = eco.FindTrait(id);
                    if (tr == null) continue;

                    switch (tr.effect)
                    {
                        case TraitEffect.DiceCount: p.diceCount += (int)tr.value; break;
                        case TraitEffect.DiceFaces: p.diceFaces += (int)tr.value; break;
                        case TraitEffect.ComboMultiplierPercent: p.comboBonus += tr.value / 100f; break;
                        case TraitEffect.CritExtraDie: p.critExtraDie = true; break;
                    }
                }
            }
        }

        var armor = EquippedItem(p, p.equippedArmorUid, db, ItemType.Armor);

        if (armor != null)
        {
            int enhance = p.FindEquipment(p.equippedArmorUid).enhance;
            p.defense += armor.defenseBonus + armor.defensePerEnhance * enhance;
        }

        p.critChance = Math.Min(1f, Math.Max(0f, p.critChance));
        p.debuffResist = Math.Min(1f, Math.Max(0f, p.debuffResist));
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

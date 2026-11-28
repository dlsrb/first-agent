using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

// C-4 성장: 경험치·레벨·스탯 포인트, 스탯 효과, 직급·재능, 숙련도·특성, 매력 숨김·할인·드러남
public class GrowthTests
{
    BattleSettings settings;
    GameDatabase db;
    EconomySettings eco;
    ItemData weapon, armor;
    TalentData sturdy, allRounder, handy, ironMind;
    TraitData diceCount, diceFaces, combo, critDie;
    WeaponMasteryData mastery;
    GameState state;
    PlayerState p;

    [SetUp]
    public void SetUp()
    {
        weapon = ScriptableObject.CreateInstance<ItemData>(); weapon.itemId = "w0"; weapon.type = ItemType.Weapon; weapon.attackBonus = 2;
        armor = ScriptableObject.CreateInstance<ItemData>(); armor.itemId = "a0"; armor.type = ItemType.Armor; armor.defenseBonus = 4;

        sturdy = Talent("sturdy", 1, ModTarget.MaxHp, 20);
        allRounder = Talent("all", 1, ModTarget.AllStats, 1);
        handy = Talent("handy", 1, ModTarget.EnhanceGoldPercent, 30);
        ironMind = Talent("iron", 2, ModTarget.DebuffResistPoints, 15);

        diceCount = Trait("count", TraitEffect.DiceCount, 1);
        diceFaces = Trait("faces", TraitEffect.DiceFaces, 2);
        combo = Trait("combo", TraitEffect.ComboMultiplierPercent, 50);
        critDie = Trait("crit", TraitEffect.CritExtraDie, 1);

        mastery = ScriptableObject.CreateInstance<WeaponMasteryData>();
        mastery.traitTiers.Add(new TraitTier { level = 10, optionA = diceCount, optionB = diceFaces });
        mastery.traitTiers.Add(new TraitTier { level = 20, optionA = combo, optionB = critDie });

        eco = ScriptableObject.CreateInstance<EconomySettings>();
        eco.startTalents.AddRange(new[] { sturdy, allRounder, handy });
        eco.ranks[1].talentChoices.Add(ironMind);
        eco.ranks[1].condition.all.Add(new Condition { kind = ConditionKind.FlagSet, key = StateKeys.BossDefeated("boss_1") });
        eco.masteries.Add(mastery);

        db = ScriptableObject.CreateInstance<GameDatabase>();
        db.items.Add(weapon); db.items.Add(armor);
        db.economy = eco;

        settings = ScriptableObject.CreateInstance<BattleSettings>();
        settings.playerBase.attack = 3;
        settings.playerBase.defense = 6;
        settings.playerBase.startWeapon = weapon;
        settings.playerBase.startArmor = armor;

        state = new GameState();
        state.player = PlayerState.CreateFrom(settings.playerBase);
        p = state.player;
        PlayerStats.Recalculate(p, settings, db);
    }

    TalentData Talent(string id, int tier, ModTarget target, float value)
    {
        var t = ScriptableObject.CreateInstance<TalentData>();
        t.talentId = id; t.talentName = id; t.tier = tier;
        t.effects.Add(new StatModifier { target = target, value = value });
        return t;
    }

    TraitData Trait(string id, TraitEffect effect, float value)
    {
        var t = ScriptableObject.CreateInstance<TraitData>();
        t.traitId = id; t.traitName = id; t.effect = effect; t.value = value;
        return t;
    }

    // ---------- 경험치·레벨 ----------

    [Test]
    public void Exp_LevelCurve_PointsPerLevel()
    {
        Assert.AreEqual(20, eco.ExpToNext(1));
        Assert.AreEqual(30, eco.ExpToNext(2));

        Assert.AreEqual(0, GrowthLogic.AddExp(p, eco, 19));
        Assert.AreEqual(2, GrowthLogic.AddExp(p, eco, 31), "19 + 31 = 50 → Lv2(20) → Lv3(30)");
        Assert.AreEqual(3, p.level);
        Assert.AreEqual(0, p.exp);
        Assert.AreEqual(2, p.statPoints);
    }

    [Test]
    public void AssignPoint_UsesPoints()
    {
        Assert.IsFalse(GrowthLogic.AssignPoint(p, StatType.Strength), "포인트 없음");
        p.statPoints = 1;
        Assert.IsTrue(GrowthLogic.AssignPoint(p, StatType.Strength));
        Assert.AreEqual(0, p.statPoints);
        Assert.AreEqual(1, p.stats.strength);
    }

    // ---------- 스탯 효과 ----------

    [Test]
    public void StatEffects_AllFive()
    {
        p.stats.strength = 3; p.stats.agility = 4; p.stats.spirit = 10; p.stats.intellect = 2; p.stats.charm = 7;
        PlayerStats.Recalculate(p, settings, db);

        Assert.AreEqual(5 + 3, p.attack, "힘 1 = 공격 1");
        Assert.AreEqual(4, p.agility, "민첩 → 도망");
        Assert.AreEqual(0.05f + 0.02f, p.critChance, 1e-4, "민첩 1 = 치명 0.5%p");
        Assert.AreEqual(100 + 50, p.maxHp, "정신력 1 = 체력 5");
        Assert.AreEqual(0.10f, p.debuffResist, 1e-4, "정신력 1 = 디버프 무시 1%p");
        Assert.AreEqual(10 + 2, p.maxEnergy, "지능 1 = 기력 1");
        Assert.AreEqual(0.06f, p.infoChance, 1e-4, "지능 1 = 정보 3%p");
        Assert.AreEqual(0.07f, p.shopDiscount, 1e-4, "매력 1 = 할인 1%");
    }

    [Test]
    public void StatCaps_DebuffResistAndDiscount()
    {
        p.stats.spirit = 50; p.stats.charm = 50;
        PlayerStats.Recalculate(p, settings, db);
        Assert.AreEqual(0.3f, p.debuffResist, 1e-4);
        Assert.AreEqual(0.2f, p.shopDiscount, 1e-4);
    }

    // ---------- 재능·직급 ----------

    [Test]
    public void StartTalent_PickOne_AppliesEffect()
    {
        Assert.AreEqual(3, GrowthLogic.TalentOptions(p, eco).Count, "새 게임: 시작 재능 후보");
        Assert.IsFalse(GrowthLogic.ChooseTalent(p, eco, "iron"), "승급 재능은 아직 못 고름");
        Assert.IsTrue(GrowthLogic.ChooseTalent(p, eco, "all"));
        PlayerStats.Recalculate(p, settings, db);

        Assert.AreEqual(1, p.totalStats.charm, "팔방미인: 스탯 모두 +1");
        Assert.AreEqual(5 + 1, p.attack);
        Assert.AreEqual(0, GrowthLogic.TalentOptions(p, eco).Count);
        Assert.IsFalse(GrowthLogic.HasPending(p, eco));
    }

    [Test]
    public void Promotion_NeedsLevelAndBoss_GivesTalentPick()
    {
        GrowthLogic.ChooseTalent(p, eco, "sturdy");
        p.level = 10;
        Assert.AreEqual(0, GrowthLogic.TryPromote(state, eco).Count, "보스 격파 전");

        state.flags.Add(StateKeys.BossDefeated("boss_1"));
        var up = GrowthLogic.TryPromote(state, eco);
        Assert.AreEqual(1, up.Count);
        Assert.AreEqual("agent", p.rankId);
        Assert.AreEqual(100, eco.HospitalCost(p), "요원 병원비");

        var options = GrowthLogic.TalentOptions(p, eco);
        Assert.AreEqual(1, options.Count);
        Assert.AreEqual("iron", options[0].talentId);
        Assert.IsTrue(GrowthLogic.ChooseTalent(p, eco, "iron"));
        PlayerStats.Recalculate(p, settings, db);
        Assert.AreEqual(0.15f, p.debuffResist, 1e-4);
    }

    [Test]
    public void HandyTalent_EnhanceGoldDiscount()
    {
        GrowthLogic.ChooseTalent(p, eco, "handy");
        PlayerStats.Recalculate(p, settings, db);
        Assert.AreEqual(70, ShopLogic.EnhanceGold(new EnhanceStep { gold = 100, materials = 1 }, p));
    }

    // ---------- 숙련도·특성 ----------

    [Test]
    public void Mastery_LevelsAndStatBonus()
    {
        Assert.AreEqual(1, GrowthLogic.AddMasteryExp(p, eco, WeaponType.Dice, 12));
        Assert.AreEqual(4, GrowthLogic.AddMasteryExp(p, eco, WeaponType.Dice, 48));
        PlayerStats.Recalculate(p, settings, db);

        Assert.AreEqual(5, p.MasteryOf(WeaponType.Dice).level);
        Assert.AreEqual(5 + 1, p.attack, "5레벨마다 공격 +1");
        Assert.AreEqual(0.05f + 5 * 0.005f, p.critChance, 1e-4, "레벨마다 치명 0.5%p");
        Assert.IsNull(GrowthLogic.PendingTraitType(p, eco));
    }

    [Test]
    public void Mastery_Level10_TraitPick_ChangesDice()
    {
        GrowthLogic.AddMasteryExp(p, eco, WeaponType.Dice, 12 * 10);
        Assert.AreEqual(WeaponType.Dice, GrowthLogic.PendingTraitType(p, eco));
        Assert.IsTrue(GrowthLogic.HasPending(p, eco));

        var tier = GrowthLogic.NextTraitTier(p, eco, WeaponType.Dice);
        Assert.AreEqual(diceCount, tier.optionA);
        Assert.IsFalse(GrowthLogic.ChooseTrait(p, eco, WeaponType.Dice, "combo"), "20레벨 특성은 아직");
        Assert.IsTrue(GrowthLogic.ChooseTrait(p, eco, WeaponType.Dice, "count"));
        PlayerStats.Recalculate(p, settings, db);

        Assert.AreEqual(3, p.diceCount);
        Assert.IsNull(GrowthLogic.PendingTraitType(p, eco));
    }

    [Test]
    public void Traits_OnlyWithWeaponOfThatType()
    {
        GrowthLogic.AddMasteryExp(p, eco, WeaponType.Dice, 120);
        GrowthLogic.ChooseTrait(p, eco, WeaponType.Dice, "faces");
        PlayerStats.Recalculate(p, settings, db);
        Assert.AreEqual(8, p.diceFaces);

        PlayerStats.Unequip(p, ItemType.Weapon, settings, db);
        Assert.AreEqual(6, p.diceFaces, "무기를 빼면 특성도 꺼짐");
    }

    // ---------- 매력 ----------

    [Test]
    public void Charm_HidesUntilMet_DiscountFromOne()
    {
        var set = new ConditionSet();
        set.all.Add(new Condition { kind = ConditionKind.StatAtLeast, stat = StatType.Charm, value = 3 });
        Assert.IsTrue(ConditionChecker.Hidden(state, set), "hideIfUnmet가 꺼져 있어도 매력 조건은 숨김");

        var entry = new ShopEntry { item = weapon, priceOverride = 600 };
        p.stats.charm = 1;
        PlayerStats.Recalculate(p, settings, db);
        Assert.AreEqual(594, ShopLogic.BuyPrice(entry, p), "매력 1부터 1% 할인");
        Assert.IsTrue(ConditionChecker.Hidden(state, set));

        p.stats.charm = 3;
        PlayerStats.Recalculate(p, settings, db);
        Assert.IsFalse(ConditionChecker.Hidden(state, set));
        Assert.AreEqual(582, ShopLogic.BuyPrice(entry, p));
    }

    [Test]
    public void RevealScanner_FlagsOnce()
    {
        var region = ScriptableObject.CreateInstance<RegionData>();
        var area = ScriptableObject.CreateInstance<AreaData>();
        var place = ScriptableObject.CreateInstance<PlaceData>(); place.locationId = "hidden";
        var pe = new PlaceEntry { place = place };
        pe.condition.all.Add(new Condition { kind = ConditionKind.StatAtLeast, stat = StatType.Charm, value = 3 });
        area.places.Add(pe);
        region.areas.Add(new AreaEntry { area = area });

        Assert.AreEqual(0, RevealScanner.Scan(state, region));
        p.stats.charm = 3;
        PlayerStats.Recalculate(p, settings, db);
        Assert.AreEqual(1, RevealScanner.Scan(state, region));
        Assert.IsTrue(state.flags.Contains(StateKeys.Revealed("place:hidden")));
        Assert.AreEqual(0, RevealScanner.Scan(state, region), "알림은 한 번만");
    }

    // ---------- 보상 보너스 ----------

    [Test]
    public void RewardApply_ExpBonusAndLevelUp()
    {
        p.expBonus = 0.25f;
        var r = new BattleReward { gold = 10, exp = 20 };
        int levels = RewardCalculator.Apply(p, r, eco);

        Assert.AreEqual(25, r.exp);
        Assert.AreEqual(1, levels);
        Assert.AreEqual(2, p.level);
        Assert.AreEqual(5, p.exp);
    }

    [Test]
    public void JsonRoundTrip_KeepsGrowth()
    {
        p.level = 4; p.statPoints = 2; p.stats.charm = 3;
        GrowthLogic.ChooseTalent(p, eco, "sturdy");
        GrowthLogic.AddMasteryExp(p, eco, WeaponType.Dice, 125);

        var loaded = JsonUtility.FromJson<GameState>(JsonUtility.ToJson(state)).player;
        PlayerStats.Recalculate(loaded, settings, db);

        Assert.AreEqual(4, loaded.level);
        Assert.AreEqual(3, loaded.totalStats.charm);
        CollectionAssert.AreEqual(new List<string> { "sturdy" }, loaded.talentIds);
        Assert.AreEqual(10, loaded.MasteryOf(WeaponType.Dice).level);
        Assert.AreEqual(1, loaded.MasteryOf(WeaponType.Dice).pendingTraitPicks);
        Assert.AreEqual(120, loaded.maxHp, "튼튼한 육체 +20");
    }
}

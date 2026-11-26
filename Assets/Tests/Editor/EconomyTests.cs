using NUnit.Framework;
using UnityEngine;

// C-1 경제 기반 검증: 장비·강화 수치 계산, 보상·드롭, 판매가, JSON 저장 호환 (docs/성장경제설계안.md)
public class EconomyTests
{
    BattleSettings settings;
    GameDatabase db;
    EconomySettings eco;
    SkillData reroll, lockDie;
    ItemData weaponCommon, weaponAdv, weaponRare, armorCommon, armorAdv, armorRare, stone, food;
    MonsterData normal, boss;

    [SetUp]
    public void SetUp()
    {
        reroll = ScriptableObject.CreateInstance<SkillData>(); reroll.skillId = "reroll";
        lockDie = ScriptableObject.CreateInstance<SkillData>(); lockDie.skillId = "lock";

        weaponCommon = Item("w0", ItemType.Weapon, ItemGrade.Common, 150, atk: 2);
        weaponAdv = Item("w1", ItemType.Weapon, ItemGrade.Advanced, 600, atk: 7); weaponAdv.skills.Add(reroll);
        weaponRare = Item("w2", ItemType.Weapon, ItemGrade.Rare, 2000, atk: 10); weaponRare.critBonus = 0.03f; weaponRare.skills.Add(reroll); weaponRare.skills.Add(lockDie);
        armorCommon = Item("a0", ItemType.Armor, ItemGrade.Common, 150, def: 4);
        armorAdv = Item("a1", ItemType.Armor, ItemGrade.Advanced, 600, def: 14);
        armorRare = Item("a2", ItemType.Armor, ItemGrade.Rare, 2000, def: 20);
        stone = Item("stone", ItemType.Material, ItemGrade.Common, 0); stone.sellPriceOverride = 20;
        food = Item("food", ItemType.Consumable, ItemGrade.Common, 30); food.healHp = 30;

        db = ScriptableObject.CreateInstance<GameDatabase>();
        db.skills.Add(reroll); db.skills.Add(lockDie);
        db.items.AddRange(new[] { weaponCommon, weaponAdv, weaponRare, armorCommon, armorAdv, armorRare, stone, food });

        settings = ScriptableObject.CreateInstance<BattleSettings>();
        settings.playerBase.attack = 3;
        settings.playerBase.defense = 6;
        settings.playerBase.startWeapon = weaponCommon;
        settings.playerBase.startArmor = armorCommon;
        settings.playerBase.startItems.Add(new StartItem { item = food, count = 3 });

        eco = ScriptableObject.CreateInstance<EconomySettings>();
        eco.materialItem = stone;
        eco.advancedPool.Add(weaponAdv); eco.advancedPool.Add(armorAdv);
        eco.rarePool.Add(weaponRare); eco.rarePool.Add(armorRare);

        normal = ScriptableObject.CreateInstance<MonsterData>(); normal.goldMin = 35; normal.goldMax = 50; normal.exp = 10;
        boss = ScriptableObject.CreateInstance<MonsterData>(); boss.goldMin = 500; boss.goldMax = 500; boss.exp = 100;
    }

    ItemData Item(string id, ItemType type, ItemGrade grade, int price, int atk = 0, int def = 0)
    {
        var i = ScriptableObject.CreateInstance<ItemData>();
        i.itemId = id; i.itemName = id; i.type = type; i.grade = grade; i.price = price; i.attackBonus = atk; i.defenseBonus = def;
        return i;
    }

    PlayerState NewPlayer()
    {
        var p = PlayerState.CreateFrom(settings.playerBase);
        PlayerStats.Recalculate(p, settings, db);
        return p;
    }

    // ---------- 장비·수치 계산 ----------

    [Test]
    public void StartPlayer_HasCommonGearEquipped_SameAsBStageStart()
    {
        var p = NewPlayer();

        Assert.AreEqual(5, p.attack, "기본 3 + 일반 무기 2");
        Assert.AreEqual(10, p.defense, "기본 6 + 일반 방어구 4");
        Assert.AreEqual(0, p.skillIds.Count, "일반 무기는 스킬 없음");
        Assert.AreEqual(2, p.equipment.Count);
        Assert.AreEqual(3, p.CountOf("food"));
        Assert.AreEqual(100, p.currentHp);
    }

    [Test]
    public void AdvancedGear_MatchesBossAssumption_WithOneSkill()
    {
        var p = NewPlayer();
        Assert.IsTrue(PlayerStats.Equip(p, p.AddEquipment("w1"), settings, db));
        Assert.IsTrue(PlayerStats.Equip(p, p.AddEquipment("a1"), settings, db));

        Assert.AreEqual(10, p.attack, "B단계 적정 장비 공격 +5");
        Assert.AreEqual(20, p.defense, "B단계 적정 장비 방어 +10");
        CollectionAssert.AreEqual(new[] { "reroll" }, p.skillIds);
    }

    [Test]
    public void RareWeapon_TwoSkillsAndCrit()
    {
        var p = NewPlayer();
        PlayerStats.Equip(p, p.AddEquipment("w2"), settings, db);

        Assert.AreEqual(13, p.attack);
        Assert.AreEqual(0.08f, p.critChance, 0.0001f);
        CollectionAssert.AreEqual(new[] { "reroll", "lock" }, p.skillIds);
    }

    [Test]
    public void Enhance_AddsPerLevelBonus()
    {
        var p = NewPlayer();
        p.FindEquipment(p.equippedWeaponUid).enhance = 2;
        p.FindEquipment(p.equippedArmorUid).enhance = 3;
        PlayerStats.Recalculate(p, settings, db);

        Assert.AreEqual(5 + 2, p.attack, "무기 강화 1단계당 공격 +1");
        Assert.AreEqual(10 + 6, p.defense, "방어구 강화 1단계당 방어 +2");
    }

    [Test]
    public void Equip_RejectsMaterial_AndRemoveUnequips()
    {
        var p = NewPlayer();
        Assert.IsFalse(PlayerStats.Equip(p, 999, settings, db), "없는 uid");

        int weaponUid = p.equippedWeaponUid;
        Assert.IsTrue(p.RemoveEquipment(weaponUid));
        PlayerStats.Recalculate(p, settings, db);

        Assert.AreEqual(0, p.equippedWeaponUid);
        Assert.AreEqual(3, p.attack, "무기 없음 = 기본 공격");
    }

    [Test]
    public void Recalculate_ClampsCurrentHpToMax()
    {
        var p = NewPlayer();
        p.currentHp = 999;
        PlayerStats.Recalculate(p, settings, db);
        Assert.AreEqual(p.maxHp, p.currentHp);
    }

    // ---------- 보상 ----------

    [Test]
    public void NormalReward_GoldInRange_Exp()
    {
        var rng = new System.Random(3);

        for (int i = 0; i < 500; i++)
        {
            var r = RewardCalculator.Roll(eco, normal, RewardKind.Normal, rng);
            Assert.That(r.gold, Is.InRange(35, 50));
            Assert.AreEqual(10, r.exp);
        }
    }

    [Test]
    public void StoryRetry_GoldAndExpAre30Percent()
    {
        var rng = new System.Random(3);
        var first = RewardCalculator.Roll(eco, boss, RewardKind.StoryFirst, rng);
        var retry = RewardCalculator.Roll(eco, boss, RewardKind.StoryRetry, rng);

        Assert.AreEqual(500, first.gold);
        Assert.AreEqual(100, first.exp);
        Assert.AreEqual(150, retry.gold);
        Assert.AreEqual(30, retry.exp);
    }

    [Test]
    public void DropRates_MatchSettings()
    {
        Rates(RewardKind.Normal, 0.15, 0.05, 0.0);
        Rates(RewardKind.StoryFirst, 0.20, 0.10, 0.03);
        Rates(RewardKind.StoryRetry, 0.20, 0.10, 0.03);
    }

    void Rates(RewardKind kind, double mat, double adv, double rare)
    {
        var rng = new System.Random(42);
        int n = 40000, m = 0, a = 0, r = 0;

        for (int i = 0; i < n; i++)
        {
            var rw = RewardCalculator.Roll(eco, normal, kind, rng);
            m += rw.materials.Count;
            foreach (var e in rw.equipment)
            {
                if (e.grade == ItemGrade.Advanced) a++;
                if (e.grade == ItemGrade.Rare) r++;
            }
        }

        Assert.AreEqual(mat, (double)m / n, 0.01, kind + " 재료");
        Assert.AreEqual(adv, (double)a / n, 0.006, kind + " 고급");
        Assert.AreEqual(rare, (double)r / n, 0.004, kind + " 희귀");
    }

    [Test]
    public void Apply_AddsGoldExpItems_EquipmentNotEquipped()
    {
        var p = NewPlayer();
        var rw = new BattleReward { gold = 40, exp = 10 };
        rw.materials.Add(stone);
        rw.equipment.Add(weaponAdv);

        int weaponBefore = p.equippedWeaponUid;
        RewardCalculator.Apply(p, rw);

        Assert.AreEqual(40, p.gold);
        Assert.AreEqual(10, p.exp);
        Assert.AreEqual(1, p.CountOf("stone"));
        Assert.AreEqual(3, p.equipment.Count);
        Assert.AreEqual(weaponBefore, p.equippedWeaponUid, "드롭 장비는 자동 장착하지 않음");
    }

    [Test]
    public void SellPrice_HalfOrOverride()
    {
        Assert.AreEqual(300, eco.SellPrice(weaponAdv));
        Assert.AreEqual(20, eco.SellPrice(stone));
        Assert.AreEqual(15, eco.SellPrice(food));
    }

    // ---------- 저장 호환 ----------

    [Test]
    public void GameState_JsonRoundTrip_KeepsEquipmentAndItems()
    {
        var state = new GameState();
        state.player = NewPlayer();
        state.player.gold = 123;
        state.player.exp = 45;
        int adv = state.player.AddEquipment("w1", 2);
        PlayerStats.Equip(state.player, adv, settings, db);
        state.player.AddItem("stone", 4);

        var loaded = JsonUtility.FromJson<GameState>(JsonUtility.ToJson(state));
        var p = loaded.player;
        PlayerStats.Recalculate(p, settings, db);

        Assert.AreEqual(123, p.gold);
        Assert.AreEqual(45, p.exp);
        Assert.AreEqual(adv, p.equippedWeaponUid);
        Assert.AreEqual(2, p.FindEquipment(adv).enhance);
        Assert.AreEqual(4, p.CountOf("stone"));
        Assert.AreEqual(3 + 7 + 2, p.attack);
        Assert.AreEqual(state.player.nextEquipUid, p.nextEquipUid);
    }
}

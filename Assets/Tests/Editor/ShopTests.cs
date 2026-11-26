using NUnit.Framework;
using UnityEngine;

// C-2 상점·병원·강화 규칙 (ShopLogic)과 범용 조건 구조 (ConditionChecker)
public class ShopTests
{
    BattleSettings settings;
    GameDatabase db;
    EconomySettings eco;
    ItemData weaponCommon, weaponAdv, armorCommon, stone, food;
    GameState state;
    PlayerState p;

    [SetUp]
    public void SetUp()
    {
        weaponCommon = Item("w0", ItemType.Weapon, 150); weaponCommon.attackBonus = 2;
        weaponAdv = Item("w1", ItemType.Weapon, 600); weaponAdv.attackBonus = 7; weaponAdv.grade = ItemGrade.Advanced;
        armorCommon = Item("a0", ItemType.Armor, 150); armorCommon.defenseBonus = 4;
        stone = Item("stone", ItemType.Material, 0); stone.sellPriceOverride = 20;
        food = Item("food", ItemType.Consumable, 30); food.healHp = 30;

        db = ScriptableObject.CreateInstance<GameDatabase>();
        db.items.AddRange(new[] { weaponCommon, weaponAdv, armorCommon, stone, food });

        settings = ScriptableObject.CreateInstance<BattleSettings>();
        settings.playerBase.attack = 3;
        settings.playerBase.defense = 6;
        settings.playerBase.startWeapon = weaponCommon;
        settings.playerBase.startArmor = armorCommon;

        eco = ScriptableObject.CreateInstance<EconomySettings>();
        eco.materialItem = stone;

        state = new GameState();
        state.player = PlayerState.CreateFrom(settings.playerBase);
        p = state.player;
        PlayerStats.Recalculate(p, settings, db);
    }

    ItemData Item(string id, ItemType type, int price)
    {
        var i = ScriptableObject.CreateInstance<ItemData>();
        i.itemId = id; i.itemName = id; i.type = type; i.price = price;
        return i;
    }

    // ---------- 구매 ----------

    [Test]
    public void BuyConsumable_TakesGold_AddsStack()
    {
        p.gold = 100;
        var r = ShopLogic.Buy(state, new ShopEntry { item = food });

        Assert.IsTrue(r.ok);
        Assert.AreEqual(70, p.gold);
        Assert.AreEqual(1, p.CountOf("food"));
    }

    [Test]
    public void BuyEquipment_AddsOneUnequippedPiece()
    {
        p.gold = 600;
        var r = ShopLogic.Buy(state, new ShopEntry { item = weaponAdv });

        Assert.IsTrue(r.ok);
        Assert.AreEqual(0, p.gold);
        Assert.IsNotNull(p.FindEquipment(r.equipmentUid));
        Assert.IsFalse(p.IsEquipped(r.equipmentUid));

        PlayerStats.Equip(p, r.equipmentUid, settings, db);
        Assert.AreEqual(10, p.attack);
    }

    [Test]
    public void Buy_NotEnoughGold_Fails_NoChange()
    {
        p.gold = 599;
        var r = ShopLogic.Buy(state, new ShopEntry { item = weaponAdv });

        Assert.IsFalse(r.ok);
        Assert.AreEqual(599, p.gold);
        Assert.AreEqual(2, p.equipment.Count);
    }

    [Test]
    public void Buy_PriceOverride()
    {
        p.gold = 100;
        ShopLogic.Buy(state, new ShopEntry { item = food, priceOverride = 5 });
        Assert.AreEqual(95, p.gold);
    }

    [Test]
    public void Buy_LockedByCondition_Fails()
    {
        p.gold = 5000;
        var entry = new ShopEntry { item = weaponAdv };
        entry.condition.all.Add(new Condition { kind = ConditionKind.StatAtLeast, stat = StatType.Charm, value = 5 });

        var r = ShopLogic.Buy(state, entry);

        Assert.IsFalse(r.ok);
        StringAssert.Contains("매력 5", r.message);
        Assert.AreEqual(5000, p.gold);
    }

    // ---------- 판매 ----------

    [Test]
    public void SellEquipment_HalfPrice_EquippedRefused()
    {
        int equipped = p.equippedWeaponUid;
        Assert.IsFalse(ShopLogic.SellEquipment(p, equipped, eco, db).ok, "장착 중은 판매 불가");

        int uid = p.AddEquipment("w1");
        var r = ShopLogic.SellEquipment(p, uid, eco, db);

        Assert.IsTrue(r.ok);
        Assert.AreEqual(300, p.gold);
        Assert.IsNull(p.FindEquipment(uid));
    }

    [Test]
    public void SellItem_OneAtATime()
    {
        p.AddItem("stone", 2);
        ShopLogic.SellItem(p, "stone", eco, db);

        Assert.AreEqual(20, p.gold);
        Assert.AreEqual(1, p.CountOf("stone"));
    }

    // ---------- 강화 ----------

    [Test]
    public void Enhance_PaysGoldAndStones_RaisesStat()
    {
        p.gold = 1000;
        p.AddItem("stone", 5);
        int uid = p.equippedWeaponUid;

        Assert.IsTrue(ShopLogic.Enhance(p, uid, eco, settings, db).ok);   // +1: 100G, 1개
        Assert.IsTrue(ShopLogic.Enhance(p, uid, eco, settings, db).ok);   // +2: 150G, 1개

        Assert.AreEqual(2, p.FindEquipment(uid).enhance);
        Assert.AreEqual(750, p.gold);
        Assert.AreEqual(3, p.CountOf("stone"));
        Assert.AreEqual(5 + 2, p.attack, "장착 무기 강화가 바로 반영");
    }

    [Test]
    public void Enhance_MissingStones_Fails()
    {
        p.gold = 1000;
        var r = ShopLogic.Enhance(p, p.equippedArmorUid, eco, settings, db);

        Assert.IsFalse(r.ok);
        Assert.AreEqual(1000, p.gold);
        Assert.AreEqual(0, p.FindEquipment(p.equippedArmorUid).enhance);
    }

    [Test]
    public void Enhance_StopsAtMax()
    {
        p.gold = 99999;
        p.AddItem("stone", 99);
        int uid = p.equippedArmorUid;

        for (int i = 0; i < 5; i++) Assert.IsTrue(ShopLogic.Enhance(p, uid, eco, settings, db).ok);

        Assert.IsFalse(ShopLogic.Enhance(p, uid, eco, settings, db).ok);
        Assert.AreEqual(5, p.FindEquipment(uid).enhance);
        Assert.AreEqual(10 + 5 * 2, p.defense);
        Assert.AreEqual(99999 - (100 + 150 + 200 + 300 + 400), p.gold);
    }

    // ---------- 병원 ----------

    [Test]
    public void Heal_FullHpAndEnergy_RankCost()
    {
        p.gold = 50;
        p.currentHp = 10;
        p.currentEnergy = 1;

        Assert.IsTrue(ShopLogic.Heal(p, eco).ok);
        Assert.AreEqual(40, p.gold, "수습 요원 10G");
        Assert.AreEqual(p.maxHp, p.currentHp);
        Assert.AreEqual(p.maxEnergy, p.currentEnergy);

        p.rankId = "agent";
        p.currentHp = 10;
        Assert.IsFalse(ShopLogic.Heal(p, eco).ok, "요원 100G, 골드 부족");
        Assert.AreEqual(10, p.currentHp);

        p.gold = 1000;
        p.rankId = "senior";
        Assert.IsTrue(ShopLogic.Heal(p, eco).ok);
        Assert.AreEqual(0, p.gold, "선임 요원 1,000G");
    }

    [Test]
    public void Heal_AlreadyFull_NoCharge()
    {
        p.gold = 50;
        Assert.IsFalse(ShopLogic.Heal(p, eco).ok);
        Assert.AreEqual(50, p.gold);
    }

    // ---------- 조건 ----------

    [Test]
    public void Conditions_FlagLevelItem_AndStatIsZeroUntilC4()
    {
        var set = new ConditionSet();
        Assert.IsTrue(ConditionChecker.Check(state, set), "비어 있으면 통과");

        set.all.Add(new Condition { kind = ConditionKind.FlagSet, key = "intro:hospital" });
        Assert.IsFalse(ConditionChecker.Check(state, set));
        state.flags.Add("intro:hospital");
        Assert.IsTrue(ConditionChecker.Check(state, set));

        set.all.Add(new Condition { kind = ConditionKind.LevelAtLeast, value = 2 });
        Assert.IsFalse(ConditionChecker.Check(state, set));
        p.level = 2;
        Assert.IsTrue(ConditionChecker.Check(state, set));

        set.all.Add(new Condition { kind = ConditionKind.HasItem, key = "w0" });
        Assert.IsTrue(ConditionChecker.Check(state, set), "장비 보유");

        var charm = new ConditionSet();
        charm.all.Add(new Condition { kind = ConditionKind.StatAtLeast, stat = StatType.Charm, value = 5 });
        Assert.IsFalse(ConditionChecker.Check(state, charm), "스탯은 C-4 전까지 0");
        Assert.AreEqual("[매력 5 필요]", ConditionChecker.LockedText(charm));
    }
}

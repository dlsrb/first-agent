using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

// BattleEngine 계산 규칙 검증 (docs/전투규칙.md). 난수는 ScriptedRandom으로 정해 둔다.
public class BattleEngineTests
{
    // 정해 둔 값을 순서대로 돌려주는 난수
    class ScriptedRandom : System.Random
    {
        public readonly Queue<int> dice = new Queue<int>();      // Next(min, max): 주사위
        public readonly Queue<int> picks = new Queue<int>();     // Next(max): 괴이 행동 선택
        public readonly Queue<double> doubles = new Queue<double>(); // 치명타·방어·도망 판정

        public override int Next(int minValue, int maxValue) { return dice.Count > 0 ? dice.Dequeue() : minValue; }
        public override int Next(int maxValue) { return picks.Count > 0 ? picks.Dequeue() : 0; }
        public override double NextDouble() { return doubles.Count > 0 ? doubles.Dequeue() : 0.99; }
    }

    BattleSettings settings;
    GameDatabase db;
    StatusEffectData taunt, morale, poison, cough;
    SkillData reroll, lockDie;
    ItemData food;
    ScriptedRandom rng;

    [SetUp]
    public void SetUp()
    {
        settings = ScriptableObject.CreateInstance<BattleSettings>();
        taunt = Effect("taunt", 3, atk: 30, def: -30);
        morale = Effect("morale", 3, atk: -30, blockedBy: SpecialAction.CoverEars);
        poison = Effect("poison", 3, dpt: 5, blockedBy: SpecialAction.HoldBreath);
        cough = Effect("cough", 2, chance: -20, blockedBy: SpecialAction.CoverMouth);
        settings.tauntEffect = taunt;

        reroll = ScriptableObject.CreateInstance<SkillData>(); reroll.skillId = "reroll"; reroll.kind = SkillKind.Reroll; reroll.energyCost = 2;
        lockDie = ScriptableObject.CreateInstance<SkillData>(); lockDie.skillId = "lock"; lockDie.kind = SkillKind.LockDie; lockDie.energyCost = 3;
        food = ScriptableObject.CreateInstance<ItemData>(); food.itemId = "food"; food.healHp = 30;

        db = ScriptableObject.CreateInstance<GameDatabase>();
        db.skills.Add(reroll); db.skills.Add(lockDie); db.items.Add(food);

        rng = new ScriptedRandom();
    }

    StatusEffectData Effect(string id, int duration, float atk = 0, float def = 0, float chance = 0, int dpt = 0, SpecialAction blockedBy = SpecialAction.None)
    {
        var e = ScriptableObject.CreateInstance<StatusEffectData>();
        e.effectId = id; e.effectName = id; e.duration = duration;
        e.attackPercent = atk; e.defensePercent = def; e.defenseChancePoints = chance; e.damagePerTurn = dpt; e.blockedBy = blockedBy;
        return e;
    }

    MonsterData Monster(int hp = 1000, int atk = 20, int def = 0, params MonsterAction[] actions)
    {
        var m = ScriptableObject.CreateInstance<MonsterData>();
        m.monsterId = "m"; m.monsterName = "m"; m.maxHp = hp; m.attack = atk; m.defense = def;
        m.actions.AddRange(actions.Length > 0 ? actions : new[] { Attack(1f) });
        return m;
    }

    static MonsterAction Attack(float mult, string id = "atk")
    {
        return new MonsterAction { actionId = id, actionName = id, kind = MonsterActionKind.Attack, damageMultiplier = mult };
    }

    static MonsterAction Debuff(StatusEffectData e)
    {
        return new MonsterAction { actionId = "debuff", actionName = "debuff", kind = MonsterActionKind.Debuff, damageMultiplier = 0f, effect = e };
    }

    PlayerState Player(int hp = 100, int atk = 5, int def = 10)
    {
        var p = PlayerState.CreateFrom(settings.playerBase);
        p.maxHp = hp; p.currentHp = hp; p.attack = atk; p.defense = def; p.critChance = 0.05f;
        p.skillIds.Add("reroll"); p.skillIds.Add("lock");
        return p;
    }

    BattleEngine Engine(PlayerState p, MonsterData m, bool allowFlee = true)
    {
        var e = new BattleEngine(settings, db, p, m, false, allowFlee, rng);
        e.Start();
        return e;
    }

    void Attack(BattleEngine e, int d1, int d2, bool crit = false)
    {
        rng.dice.Enqueue(d1); rng.dice.Enqueue(d2);
        e.RollDice();
        rng.doubles.Enqueue(crit ? 0.0 : 0.99);
        e.ConfirmAttack();
    }

    // ---------- 피해 계산 ----------

    [Test]
    public void PlayerDamage_Formula_NoCombo()
    {
        var e = Engine(Player(), Monster(def: 0));
        Attack(e, 3, 5);                       // (8 + 5) = 13
        Assert.AreEqual(1000 - 13, e.MonsterHp);
    }

    [Test]
    public void PlayerDamage_MonsterDefense100_Halves_RoundsAwayFromZero()
    {
        var e = Engine(Player(), Monster(def: 100));
        Attack(e, 3, 5);                       // 13 × 0.5 = 6.5 → 7
        Assert.AreEqual(1000 - 7, e.MonsterHp);
    }

    [Test]
    public void Combo_TwoSame_Doubles_DiceSum()
    {
        var e = Engine(Player(), Monster(def: 0));
        Attack(e, 4, 4);                       // 8×2 + 5 = 21
        Assert.AreEqual(1000 - 21, e.MonsterHp);
        Assert.AreEqual(1, e.comboCount);
    }

    [Test]
    public void ComboTable_ByDiceCount_UsesLastBeyondTable()
    {
        var e = Engine(Player(), Monster());
        Assert.AreEqual(2f, e.ComboMultiplier(2));
        Assert.AreEqual(5f, e.ComboMultiplier(3));
        Assert.AreEqual(10f, e.ComboMultiplier(4));
        Assert.AreEqual(20f, e.ComboMultiplier(5));
        Assert.AreEqual(20f, e.ComboMultiplier(7));
        Assert.AreEqual(1f, e.ComboMultiplier(1));
    }

    [Test]
    public void Crit_Multiplies_15()
    {
        var e = Engine(Player(), Monster(def: 0));
        Attack(e, 3, 5, crit: true);           // 13 × 1.5 = 19.5 → 20
        Assert.AreEqual(1000 - 20, e.MonsterHp);
    }

    [Test]
    public void MinDamage_IsOne()
    {
        var e = Engine(Player(), Monster(def: 1000000));
        Attack(e, 1, 2);
        Assert.AreEqual(1000 - 1, e.MonsterHp);
    }

    [Test]
    public void MonsterDamage_Formula_WithActionMultiplier()
    {
        var p = Player(def: 10);
        var e = Engine(p, Monster(atk: 20, actions: Attack(1.5f)));
        Attack(e, 1, 2);                       // 20 × 1.5 × 100/110 = 27.27 → 27
        Assert.AreEqual(100 - 27, p.currentHp);
    }

    // ---------- 방어 ----------

    [Test]
    public void TouchBonusMax_StrongerMonster_SmallerBonus()
    {
        Assert.AreEqual(0.15f, Engine(Player(), Monster(atk: 10)).TouchBonusMax, 1e-4);
        Assert.AreEqual(0.10f, Engine(Player(), Monster(atk: 20)).TouchBonusMax, 1e-4);
        Assert.AreEqual(0.05f, Engine(Player(), Monster(atk: 30)).TouchBonusMax, 1e-4);
    }

    [Test]
    public void DefenseChance_TouchBonusClamped_AndCappedAt95()
    {
        var e = Engine(Player(), Monster(atk: 10));
        Assert.AreEqual(0.45f, e.DefenseChance(1f), 1e-4);        // 30% + 최대 15%
        e.playerEffects.Add(new ActiveEffect { data = Effect("x", 3, chance: 200), turnsLeft = 3 });
        Assert.AreEqual(0.95f, e.DefenseChance(0f), 1e-4);
    }

    [Test]
    public void Defend_Success_NegatesAttack()
    {
        var p = Player();
        var e = Engine(p, Monster());
        rng.doubles.Enqueue(0.0);              // 성공
        Assert.IsTrue(e.Defend(0f));
        Assert.AreEqual(100, p.currentHp);
    }

    [Test]
    public void Defend_DoesNotBlockDebuff()
    {
        var p = Player();
        var e = Engine(p, Monster(actions: Debuff(morale)));
        rng.doubles.Enqueue(0.0);
        e.Defend(0f);
        Assert.AreEqual(1, e.playerEffects.Count);
    }

    // ---------- 특수 행동·디버프 ----------

    [Test]
    public void Debuff_BlockedByMatchingSpecial_NotByWrongOne()
    {
        var e = Engine(Player(), Monster(actions: Debuff(morale)));
        e.Special(SpecialAction.CoverEars);
        Assert.AreEqual(0, e.playerEffects.Count, "귀막기로 사기 저하를 막아야 함");

        e.Special(SpecialAction.HoldBreath);
        Assert.AreEqual(1, e.playerEffects.Count, "호흡참기로는 못 막음");
    }

    [Test]
    public void MoraleDown_Reduces_PlayerDamage30Percent()
    {
        var e = Engine(Player(), Monster(def: 0, actions: Debuff(morale)));
        e.Special(SpecialAction.Wait);         // 사기 저하 걸림
        Attack(e, 3, 5);                       // 13 × 0.7 = 9.1 → 9
        Assert.AreEqual(1000 - 9, e.MonsterHp);
    }

    [Test]
    public void Poison_TicksFromNextTurn_ThreeTimes()
    {
        var p = Player();
        var idle = new MonsterAction { actionId = "idle", kind = MonsterActionKind.Reflect };
        // 행동 선택: 0 = 독기(1턴), 이후 1 = 대기 (다음 턴 행동은 턴 끝에 미리 고르므로 순서대로 넣어 둠)
        rng.picks.Enqueue(0); rng.picks.Enqueue(1); rng.picks.Enqueue(1); rng.picks.Enqueue(1); rng.picks.Enqueue(1); rng.picks.Enqueue(1);
        var e = Engine(p, Monster(actions: new[] { Debuff(poison), idle }));
        e.Special(SpecialAction.Wait);         // 1턴: 중독 걸림 (이 턴은 피해 없음)
        Assert.AreEqual(100, p.currentHp);
        e.Special(SpecialAction.Wait); Assert.AreEqual(95, p.currentHp);
        e.Special(SpecialAction.Wait); Assert.AreEqual(90, p.currentHp);
        e.Special(SpecialAction.Wait); Assert.AreEqual(85, p.currentHp);
        e.Special(SpecialAction.Wait); Assert.AreEqual(85, p.currentHp, "3턴 뒤 끝");
        Assert.AreEqual(0, e.playerEffects.Count);
    }

    [Test]
    public void Reflect_AttackHurtsPlayer_WaitIsSafe()
    {
        var p = Player();
        var reflect = new MonsterAction { actionId = "reflect", kind = MonsterActionKind.Reflect };
        var e = Engine(p, Monster(def: 0, actions: reflect));
        Assert.IsTrue(e.MonsterReflecting);
        Attack(e, 3, 5);                       // 13 반사
        Assert.AreEqual(1000, e.MonsterHp);
        Assert.AreEqual(100 - 13, p.currentHp);

        e.Special(SpecialAction.Wait);
        Assert.AreEqual(100 - 13, p.currentHp);
    }

    [Test]
    public void Taunt_MonsterDefenseMinus30_AttackPlus30_NoStack_ThreeTurns()
    {
        var p = Player(hp: 1000, def: 0);
        var e = Engine(p, Monster(atk: 20, def: 50));
        e.Special(SpecialAction.Taunt);        // 이번 턴 괴이 공격부터 26
        Assert.AreEqual(1000 - 26, p.currentHp);
        Assert.AreEqual(35, e.MonsterDefense);
        Assert.AreEqual(26, e.MonsterAttack);

        e.Special(SpecialAction.Taunt);        // 다시 걸어도 중첩 없음
        Assert.AreEqual(1, e.monsterEffects.Count);
        Assert.AreEqual(26, e.MonsterAttack);

        e.Special(SpecialAction.Wait);
        e.Special(SpecialAction.Wait);         // 다시 건 턴 포함 3턴 뒤 끝
        Assert.AreEqual(0, e.monsterEffects.Count);
        Assert.AreEqual(20, e.MonsterAttack);
    }

    [Test]
    public void EatFood_Heals_AndConsumes()
    {
        var p = Player();
        p.currentHp = 50;
        p.items.Clear(); p.AddItem("food", 1);
        var e = Engine(p, Monster(actions: new MonsterAction { actionId = "r", kind = MonsterActionKind.Reflect }));
        Assert.IsTrue(e.CanSpecial(SpecialAction.EatFood));
        e.Special(SpecialAction.EatFood);
        Assert.AreEqual(80, p.currentHp);
        Assert.AreEqual(0, p.CountOf("food"));
        Assert.IsFalse(e.CanSpecial(SpecialAction.EatFood));
    }

    // ---------- 도망 ----------

    [Test]
    public void Flee_Fail_DoublesDamage_Success_Ends()
    {
        var p = Player(def: 0);
        var e = Engine(p, Monster(atk: 10));
        rng.doubles.Enqueue(0.99);             // 실패
        Assert.IsFalse(e.Flee());
        Assert.AreEqual(100 - 20, p.currentHp);

        rng.doubles.Enqueue(0.0);              // 성공
        Assert.IsTrue(e.Flee());
        Assert.AreEqual(BattleOutcome.Fled, e.Outcome);
    }

    [Test]
    public void Flee_NotAllowed_ForBossOrStory()
    {
        var m = Monster(); m.canFlee = false;
        Assert.IsFalse(Engine(Player(), m).Flee());
        Assert.IsFalse(Engine(Player(), Monster(), allowFlee: false).Flee());
    }

    // ---------- 스킬·기력 ----------

    [Test]
    public void Reroll_CostsEnergy_OncePerAttack_EnergyPersists()
    {
        var p = Player();
        p.currentEnergy = 3;
        var e = Engine(p, Monster(def: 0));
        rng.dice.Enqueue(1); rng.dice.Enqueue(2);
        e.RollDice();
        rng.dice.Enqueue(6); rng.dice.Enqueue(6);
        Assert.IsTrue(e.UseReroll(reroll));
        Assert.AreEqual(1, p.currentEnergy);
        Assert.IsFalse(e.UseReroll(reroll), "한 공격에 한 번");
        Assert.IsTrue(e.PendingRoll.combo);
        rng.doubles.Enqueue(0.99);
        e.ConfirmAttack();                     // 12×2 + 5 = 29
        Assert.AreEqual(1000 - 29, e.MonsterHp);
        Assert.AreEqual(1, p.currentEnergy, "전투 중 자동 회복 없음");
    }

    [Test]
    public void LockDie_NextAttackFirstDieFixed()
    {
        var p = Player();
        var e = Engine(p, Monster(def: 0));
        rng.dice.Enqueue(6); rng.dice.Enqueue(2);
        e.RollDice();
        Assert.IsTrue(e.UseLockDie(lockDie, 0));
        Assert.AreEqual(10 - 3, p.currentEnergy);
        rng.doubles.Enqueue(0.99);
        e.ConfirmAttack();

        rng.dice.Enqueue(6);                   // 두 번째 주사위만 굴림
        var roll = e.RollDice();
        Assert.AreEqual(6, roll.faces[0]);
        Assert.AreEqual(0, roll.lockedIndex);
        Assert.IsTrue(roll.combo);
    }

    [Test]
    public void Skill_NotUsable_WithoutEnergy()
    {
        var p = Player();
        p.currentEnergy = 1;
        var e = Engine(p, Monster());
        e.RollDice();
        Assert.IsFalse(e.CanUseSkill(reroll));
        Assert.IsFalse(e.UseReroll(reroll));
    }

    // ---------- 보스·선공 ----------

    [Test]
    public void BossPhase_TriggersAtHalfHp_AppliesMultipliers_AndPattern()
    {
        var m = Monster(hp: 40, atk: 20, def: 0);
        var comic = ScriptableObject.CreateInstance<ComicData>();
        m.phases.Add(new BossPhase { trigger = PhaseTrigger.HpBelow, hpPercent = 50, attackMultiplier = 1.3f, defenseMultiplier = 0.7f, comic = comic,
                                     actions = new List<MonsterAction> { Attack(2f, "smash") } });
        var e = Engine(Player(def: 0), m);
        Attack(e, 6, 5);                       // 16 → 24 남음 (60%)
        Assert.AreEqual(-1, e.PhaseIndex);
        Attack(e, 1, 2);                       // 8 → 16 남음 (40%)
        Assert.AreEqual(0, e.PhaseIndex);
        Assert.AreEqual(26, e.MonsterAttack);
        Assert.AreSame(comic, e.PendingComic);
        Assert.AreEqual("smash", e.Intent.actionId, "다음 턴부터 새 패턴");
    }

    [Test]
    public void FirstStrike_MonsterActsBeforeTurnOne()
    {
        var p = Player(def: 0);
        var m = Monster(atk: 10); m.firstStrike = true;
        var e = Engine(p, m);
        Assert.AreEqual(90, p.currentHp);
        Assert.AreEqual(1, e.Turn);
    }

    [Test]
    public void Win_And_Lose_Outcomes()
    {
        var e = Engine(Player(), Monster(hp: 5, def: 0));
        Attack(e, 3, 5);
        Assert.AreEqual(BattleOutcome.Win, e.Outcome);

        var p = Player(hp: 10, def: 0);
        var e2 = Engine(p, Monster(atk: 50));
        e2.Special(SpecialAction.Wait);
        Assert.AreEqual(BattleOutcome.Lose, e2.Outcome);
        Assert.AreEqual(0, p.currentHp);
    }
}

using System;
using System.Collections.Generic;
using System.Text;

// [C-6] 경제 시뮬레이션 (설계안 7번을 실제 데이터·규칙으로). 새 게임부터 일반 전투를 반복하는 봇:
// 전투 전 체력 70% 미만이면 병원(직급별 비용), 보상·드롭·레벨 업(힘·정신력 번갈아)·숙련도는 실제 규칙,
// 병원비를 남기고 고급 무기 → 고급 방어구 구매·장착, 강화석·중복 장비는 판매.
// 메뉴 "요원 > 경제 시뮬레이션 (1500명)" → docs/밸런스기록.md
public static class EconomySimulator
{
    public class Result
    {
        public int runs;
        public double avgBothAt, within25, within30, within35;
        public double goldPerBattle, hospitalPerBattle, hospitalGoldPerBattle, loseRate;
        public double levelAt28, masteryAt28, goldAt28;
        public int neverBoth;
    }

    public static Result Run(BattleSettings s, GameDatabase db, RegionData region, int runs, int seed, bool drops, int maxBattles = 60)
    {
        var eco = db.economy;
        var rng = new Random(seed);
        var pools = new List<List<EncounterEntry>>();
        foreach (var ae in region.areas) if (ae.area != null && ae.area.encounters.Count > 0) pools.Add(ae.area.encounters);

        var advWeapon = db.items.Find(i => i != null && i.type == ItemType.Weapon && i.grade == ItemGrade.Advanced);
        var advArmor = db.items.Find(i => i != null && i.type == ItemType.Armor && i.grade == ItemGrade.Advanced);
        var opt = new BattleSimulator.Options { policy = BattleSimulator.Policy.Smart, useSkills = true, allowFood = true };

        var r = new Result { runs = runs };
        long battles = 0, gross = 0, hosp = 0, hospGold = 0, loses = 0, sumBoth = 0;
        int got = 0, w25 = 0, w30 = 0, w35 = 0;
        double lv28 = 0, m28 = 0, g28 = 0;

        for (int run = 0; run < runs; run++)
        {
            var p = BattleSimulator.NewPlayer(s, db);
            int toggle = 0, bothAt = 0;

            for (int b = 1; b <= maxBattles; b++)
            {
                // 병원
                int cost = eco.HospitalCost(p);
                if (p.currentHp < p.maxHp * 0.7f && p.gold >= cost)
                {
                    p.gold -= cost; p.currentHp = p.maxHp; p.currentEnergy = p.maxEnergy;
                    hosp++; hospGold += cost;
                }

                var pool = pools[rng.Next(pools.Count)];
                var e = BattleSimulator.Fight(s, db, p, Pick(pool, rng), opt, rng);
                battles++;

                if (e.Outcome == BattleOutcome.Win)
                {
                    var reward = RewardCalculator.Roll(eco, e.monster, RewardKind.Normal, rng, drops ? 1f + p.dropBonus : 0f);
                    int before = p.gold;
                    RewardCalculator.Apply(p, reward, eco);
                    gross += p.gold - before;

                    // 재료·중복 장비 판매, 처음 얻은 고급 장비는 장착
                    foreach (var m in reward.materials) { p.RemoveItem(m.itemId, 1); p.gold += eco.SellPrice(m); }
                    foreach (var eq in reward.equipment) TakeEquipment(p, eq, s, db, eco);
                }
                else if (e.Outcome == BattleOutcome.Lose)
                {
                    loses++;
                    p.gold -= (int)(p.gold * s.defeatGoldLossRatio);
                    p.currentHp = Math.Max(1, (int)Math.Round(p.maxHp * s.reviveHpRatio));
                }

                // 성장: 레벨 포인트(힘·정신력 번갈아), 숙련도
                while (p.statPoints > 0) GrowthLogic.AssignPoint(p, toggle++ % 2 == 0 ? StatType.Strength : StatType.Spirit);
                var weapon = PlayerStats.EquippedItem(p, p.equippedWeaponUid, db, ItemType.Weapon);
                if (weapon != null) GrowthLogic.AddMasteryExp(p, eco, weapon.weaponType, e.attackCount);
                PlayerStats.Recalculate(p, s, db);

                // 구매 (병원비는 남긴다)
                int reserve = eco.HospitalCost(p);
                if (!Owns(p, advWeapon) && advWeapon != null && p.gold >= advWeapon.price + reserve) { p.gold -= advWeapon.price; TakeEquipment(p, advWeapon, s, db, eco); }
                else if (Owns(p, advWeapon) && !Owns(p, advArmor) && advArmor != null && p.gold >= advArmor.price + reserve) { p.gold -= advArmor.price; TakeEquipment(p, advArmor, s, db, eco); }

                if (bothAt == 0 && Owns(p, advWeapon) && Owns(p, advArmor)) bothAt = b;

                if (b == 28)
                {
                    lv28 += p.level;
                    m28 += p.MasteryOf(WeaponType.Dice).level;
                    g28 += p.gold;
                }
            }

            if (bothAt > 0)
            {
                got++; sumBoth += bothAt;
                if (bothAt <= 25) w25++;
                if (bothAt <= 30) w30++;
                if (bothAt <= 35) w35++;
            }
        }

        r.avgBothAt = got > 0 ? (double)sumBoth / got : 0;
        r.neverBoth = runs - got;
        r.within25 = (double)w25 / runs; r.within30 = (double)w30 / runs; r.within35 = (double)w35 / runs;
        r.goldPerBattle = (double)gross / battles;
        r.hospitalPerBattle = (double)hosp / battles;
        r.hospitalGoldPerBattle = (double)hospGold / battles;
        r.loseRate = (double)loses / battles;
        r.levelAt28 = lv28 / runs; r.masteryAt28 = m28 / runs; r.goldAt28 = g28 / runs;
        return r;
    }

    static MonsterData Pick(List<EncounterEntry> pool, Random rng)
    {
        int total = 0;
        foreach (var e in pool) total += Math.Max(1, e.weight);
        int pick = rng.Next(total);
        foreach (var e in pool) { pick -= Math.Max(1, e.weight); if (pick < 0) return e.monster; }
        return pool[pool.Count - 1].monster;
    }

    static bool Owns(PlayerState p, ItemData item)
    {
        return item != null && p.equipment.Exists(x => x.itemId == item.itemId);
    }

    // 처음 얻은 고급 장비는 장착, 이미 있으면 판매
    static void TakeEquipment(PlayerState p, ItemData item, BattleSettings s, GameDatabase db, EconomySettings eco)
    {
        if (item.grade != ItemGrade.Advanced || Owns(p, item)) { p.gold += eco.SellPrice(item); return; }
        PlayerStats.Equip(p, p.AddEquipment(item.itemId), s, db);
    }

    public static string Report(BattleSettings s, GameDatabase db, RegionData region, int runs, int seed)
    {
        var sb = new StringBuilder();
        var eco = db.economy;
        sb.AppendLine("새 게임부터 일반 전투 반복, " + runs + "명, 시드 " + seed + ". 병원 체력 70% 미만(수습 " + eco.ranks[0].hospitalCost + "G), 레벨 포인트 힘·정신력 번갈아, 고급 무기 → 고급 방어구 구매");
        sb.AppendLine();
        sb.AppendLine("| 드롭 | 고급 2개까지(평균) | 25전 안 | 30전 안 | 35전 안 | 60전 안 못 갖춤 | 전투당 수입 | 병원/전투 | 병원비/전투 | 패배율 | 28전 레벨 | 28전 숙련도 | 28전 소지금 |");
        sb.AppendLine("|---|---|---|---|---|---|---|---|---|---|---|---|---|");

        foreach (bool drops in new[] { true, false })
        {
            var r = Run(s, db, region, runs, seed, drops);
            sb.AppendLine("| " + (drops ? "있음" : "없음") + " | " + r.avgBothAt.ToString("F1") + "전 | " + Pct(r.within25) + " | " + Pct(r.within30) + " | " + Pct(r.within35) + " | " +
                          r.neverBoth + "명 | " + r.goldPerBattle.ToString("F1") + " | " + r.hospitalPerBattle.ToString("F2") + " | " + r.hospitalGoldPerBattle.ToString("F1") + " | " +
                          Pct(r.loseRate) + " | " + r.levelAt28.ToString("F1") + " | " + r.masteryAt28.ToString("F1") + " | " + r.goldAt28.ToString("F0") + " |");
        }

        return sb.ToString();
    }

    static string Pct(double v)
    {
        return (v * 100).ToString("F0") + "%";
    }
}

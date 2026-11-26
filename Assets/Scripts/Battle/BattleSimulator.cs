using System;
using System.Collections.Generic;
using System.Text;

// 밸런스 시뮬레이션: 실제 게임과 같은 BattleEngine으로 자동 전투를 여러 번 돌려 평균을 낸다.
// 메뉴 "요원 > 전투 시뮬레이션" 또는 BattleSimulator.RunReport(...)로 실행.
public static class BattleSimulator
{
    public enum Policy
    {
        Smart,       // 예고를 읽고 맞는 특수 행동, 체력 30% 이하면 방어, 나머지는 공격
        SmartTaunt,  // Smart + 첫 턴 도발 (보스용 비교)
        AttackOnly   // 무조건 공격 (조합에만 기대는 플레이)
    }

    public class Options
    {
        public Policy policy = Policy.Smart;
        public bool useSkills = false;
        public bool allowFood = false;
        public float touchFraction = 0.5f;   // 연속 터치 보너스를 최대치의 몇 %까지 얻는다고 가정
        public int maxTurns = 300;
    }

    public class Stats
    {
        public int battles, wins, losses, fled;
        public long winTurns, damageTaken, attacks, combos, crits, damageDealt, comboBonus, energyUsed, skillUses;

        public double WinRate => battles == 0 ? 0 : (double)wins / battles;
        public double AvgWinTurns => wins == 0 ? 0 : (double)winTurns / wins;
        public double AvgDamageTaken => battles == 0 ? 0 : (double)damageTaken / battles;
        public double ComboRate => attacks == 0 ? 0 : (double)combos / attacks;
        public double ComboShare => damageDealt == 0 ? 0 : (double)comboBonus / damageDealt;
        public double CritRate => attacks == 0 ? 0 : (double)crits / attacks;
        public double AvgEnergyUsed => battles == 0 ? 0 : (double)energyUsed / battles;
    }

    // ---------- 전투 1회 ----------

    public static BattleEngine Fight(BattleSettings s, GameDatabase db, PlayerState player, MonsterData monster, Options o, Random rng)
    {
        var e = new BattleEngine(s, db, player, monster, true, true, rng);
        e.Start();

        while (e.Outcome == BattleOutcome.None && e.Turn <= o.maxTurns)
        {
            PlayTurn(e, o);
            e.ClearPendingPhase();
        }

        return e;
    }

    static void PlayTurn(BattleEngine e, Options o)
    {
        if (o.policy == Policy.AttackOnly)
        {
            Attack(e, o);
            return;
        }

        var intent = e.Intent;
        var p = e.player;
        float hpRatio = (float)p.currentHp / p.maxHp;

        if (o.policy == Policy.SmartTaunt && e.Turn == 1)
        {
            e.Special(SpecialAction.Taunt);
            return;
        }

        if (intent != null && intent.kind == MonsterActionKind.Reflect)
        {
            e.Special(SpecialAction.Wait);
        }
        else if (intent != null && intent.kind == MonsterActionKind.Debuff && intent.effect != null &&
                 intent.effect.blockedBy != SpecialAction.None)
        {
            e.Special(intent.effect.blockedBy);
        }
        else if (o.allowFood && hpRatio <= 0.35f && e.CanSpecial(SpecialAction.EatFood))
        {
            e.Special(SpecialAction.EatFood);
        }
        else if (intent != null && intent.kind == MonsterActionKind.Attack && hpRatio <= 0.30f)
        {
            e.Defend(e.TouchBonusMax * o.touchFraction);
        }
        else
        {
            Attack(e, o);
        }
    }

    static void Attack(BattleEngine e, Options o)
    {
        var roll = e.RollDice();

        if (o.useSkills)
        {
            SkillData reroll = null, lockDie = null;

            foreach (var sk in e.EquippedSkills)
            {
                if (sk.kind == SkillKind.Reroll) reroll = sk;
                if (sk.kind == SkillKind.LockDie) lockDie = sk;
            }

            // 낮게 나왔고 조합이 아니면 다시 굴리기
            if (!roll.combo && roll.sum <= 5 && e.CanUseSkill(reroll))
            {
                e.UseReroll(reroll);
            }

            // 조합이 아니고 높은 눈이 있으면 다음 공격용으로 고정
            roll = e.PendingRoll;

            if (!roll.combo && e.LockedDieValue == 0 && e.CanUseSkill(lockDie))
            {
                int best = 0;

                for (int i = 1; i < roll.faces.Length; i++)
                {
                    if (roll.faces[i] > roll.faces[best]) best = i;
                }

                if (roll.faces[best] >= e.player.diceFaces - 1)
                {
                    e.UseLockDie(lockDie, best);
                }
            }
        }

        e.ConfirmAttack();
    }

    static void Accumulate(Stats st, BattleEngine e)
    {
        st.battles++;

        switch (e.Outcome)
        {
            case BattleOutcome.Win: st.wins++; st.winTurns += e.Turn; break;
            case BattleOutcome.Lose: st.losses++; break;
            case BattleOutcome.Fled: st.fled++; break;
        }

        st.damageTaken += e.damageTaken;
        st.attacks += e.attackCount;
        st.combos += e.comboCount;
        st.crits += e.critCount;
        st.damageDealt += e.damageDealt;
        st.comboBonus += e.comboBonusDamage;
        st.energyUsed += e.energyUsed;
        st.skillUses += e.skillUses;
    }

    // ---------- 괴이 1종을 매번 새 체력으로 n회 ----------

    public static Stats RunSingle(BattleSettings s, GameDatabase db, PlayerState template, MonsterData monster, Options o, int n, int seed)
    {
        var rng = new Random(seed);
        var st = new Stats();

        for (int i = 0; i < n; i++)
        {
            var p = template.Clone();
            Accumulate(st, Fight(s, db, p, monster, o, rng));
        }

        return st;
    }

    // ---------- 회복 없이 연속 전투 ----------

    public class Endurance
    {
        public int runs;
        public long totalWins;
        public int atLeast2, atLeast3;
        public long energyEmptyBattleSum;   // 기력이 가장 싼 스킬 비용 아래로 떨어진 전투 번호 합
        public int energyEmptyRuns;

        public double AvgWins => runs == 0 ? 0 : (double)totalWins / runs;
        public double Rate2 => runs == 0 ? 0 : (double)atLeast2 / runs;
        public double Rate3 => runs == 0 ? 0 : (double)atLeast3 / runs;
        public double AvgEnergyEmptyAt => energyEmptyRuns == 0 ? 0 : (double)energyEmptyBattleSum / energyEmptyRuns;
    }

    public static Endurance RunEndurance(BattleSettings s, GameDatabase db, PlayerState template, List<EncounterEntry> pool,
                                         Options o, int runs, int seed, int maxBattles = 30)
    {
        var rng = new Random(seed);
        var result = new Endurance { runs = runs };
        int minCost = int.MaxValue;

        foreach (var id in template.skillIds)
        {
            var sk = db.FindSkill(id);
            if (sk != null) minCost = Math.Min(minCost, sk.energyCost);
        }

        for (int r = 0; r < runs; r++)
        {
            var p = template.Clone();
            int wins = 0;
            bool energyMarked = false;

            for (int b = 1; b <= maxBattles; b++)
            {
                var e = Fight(s, db, p, PickWeighted(pool, rng), o, rng);

                if (o.useSkills && !energyMarked && p.currentEnergy < minCost)
                {
                    result.energyEmptyBattleSum += b;
                    result.energyEmptyRuns++;
                    energyMarked = true;
                }

                if (e.Outcome != BattleOutcome.Win)
                {
                    break;
                }

                wins++;
            }

            result.totalWins += wins;
            if (wins >= 2) result.atLeast2++;
            if (wins >= 3) result.atLeast3++;
        }

        return result;
    }

    static MonsterData PickWeighted(List<EncounterEntry> pool, Random rng)
    {
        int total = 0;
        foreach (var e in pool) total += Math.Max(1, e.weight);
        int pick = rng.Next(total);

        foreach (var e in pool)
        {
            pick -= Math.Max(1, e.weight);
            if (pick < 0) return e.monster;
        }

        return pool[pool.Count - 1].monster;
    }

    // ---------- 보고서 (마크다운) ----------

    public static string RunReport(BattleSettings s, GameDatabase db, RegionData region, int n, int seed)
    {
        var sb = new StringBuilder();
        var basePlayer = PlayerState.CreateFrom(s.playerBase);
        var pb = s.playerBase;

        sb.AppendLine("주인공 시작 수치: 체력 " + pb.maxHp + ", 기력 " + pb.maxEnergy + ", 공격 " + pb.attack + ", 방어 " + pb.defense +
                      ", 치명 " + Pct(pb.critChance) + ", 주사위 " + pb.diceCount + "개×" + pb.diceFaces + "면 / 전투마다 " + n + "회, 시드 " + seed);
        sb.AppendLine();
        sb.AppendLine("### 괴이별 (매 전투 체력·기력 가득)");
        sb.AppendLine();
        sb.AppendLine("| 괴이 | 체력/공격/방어 | 플레이 | 승률 | 평균 턴(승리) | 받은 피해 | 조합 발동 | 조합 피해 비중 | 기력 사용 |");
        sb.AppendLine("|---|---|---|---|---|---|---|---|---|");

        var normals = new List<MonsterData>();
        var bosses = new List<MonsterData>();

        foreach (var m in db.monsters)
        {
            if (m == null) continue;
            (m.IsBoss ? bosses : normals).Add(m);
        }

        foreach (var m in normals)
        {
            Row(sb, s, db, basePlayer, m, new Options { policy = Policy.Smart }, "예고 대응", n, seed);
            Row(sb, s, db, basePlayer, m, new Options { policy = Policy.Smart, useSkills = true }, "예고 대응+스킬", n, seed);
            Row(sb, s, db, basePlayer, m, new Options { policy = Policy.SmartTaunt }, "예고 대응+첫 턴 도발", n, seed);
            Row(sb, s, db, basePlayer, m, new Options { policy = Policy.AttackOnly }, "무조건 공격", n, seed);
        }

        sb.AppendLine();
        sb.AppendLine("### 보스");
        sb.AppendLine();
        sb.AppendLine("| 괴이 | 체력/공격/방어 | 플레이 | 승률 | 평균 턴(승리) | 받은 피해 | 조합 발동 | 조합 피해 비중 | 기력 사용 |");
        sb.AppendLine("|---|---|---|---|---|---|---|---|---|");

        var geared = basePlayer.Clone();
        geared.attack += 5;
        geared.defense += 10;

        foreach (var m in bosses)
        {
            Row(sb, s, db, basePlayer, m, new Options { policy = Policy.Smart, useSkills = true, allowFood = true }, "시작 장비, 스킬·음식", n, seed);
            Row(sb, s, db, geared, m, new Options { policy = Policy.Smart, useSkills = true, allowFood = true }, "적정 장비(+5/+10), 스킬·음식", n, seed);
            Row(sb, s, db, geared, m, new Options { policy = Policy.SmartTaunt, useSkills = true, allowFood = true }, "적정 장비, 첫 턴 도발", n, seed);
            Row(sb, s, db, geared, m, new Options { policy = Policy.Smart }, "적정 장비, 스킬·음식 없음", n, seed);
        }

        sb.AppendLine();
        sb.AppendLine("### 연속 버티기 (회복·음식 없이, 체력·기력 이어짐, 구역 조우 목록)");
        sb.AppendLine();
        sb.AppendLine("| 구역 | 플레이 | 평균 승리 수 | 2회 이상 | 3회 이상 | 기력이 바닥난 전투(평균) |");
        sb.AppendLine("|---|---|---|---|---|---|");

        var all = new List<EncounterEntry>();

        foreach (var ae in region.areas)
        {
            var area = ae.area;
            if (area == null || area.encounters.Count == 0) continue;
            all.AddRange(area.encounters);
            EnduranceRow(sb, s, db, basePlayer, area.areaId, area.encounters, n, seed);
        }

        if (all.Count > 0)
        {
            EnduranceRow(sb, s, db, basePlayer, "전체 섞음", all, n, seed);
        }

        return sb.ToString();
    }

    static void Row(StringBuilder sb, BattleSettings s, GameDatabase db, PlayerState player, MonsterData m, Options o, string label, int n, int seed)
    {
        var st = RunSingle(s, db, player, m, o, n, seed);
        sb.AppendLine("| " + m.monsterId + " | " + m.maxHp + "/" + m.attack + "/" + m.defense + " | " + label + " | " + Pct(st.WinRate) + " | " +
                      st.AvgWinTurns.ToString("F1") + " | " + st.AvgDamageTaken.ToString("F1") + " | " + Pct(st.ComboRate) + " | " +
                      Pct(st.ComboShare) + " | " + st.AvgEnergyUsed.ToString("F1") + " |");
    }

    static void EnduranceRow(StringBuilder sb, BattleSettings s, GameDatabase db, PlayerState player, string label, List<EncounterEntry> pool, int n, int seed)
    {
        var noSkill = RunEndurance(s, db, player, pool, new Options { policy = Policy.Smart }, n, seed);
        var skill = RunEndurance(s, db, player, pool, new Options { policy = Policy.Smart, useSkills = true }, n, seed);
        sb.AppendLine("| " + label + " | 스킬 안 씀 | " + noSkill.AvgWins.ToString("F2") + " | " + Pct(noSkill.Rate2) + " | " + Pct(noSkill.Rate3) + " | - |");
        sb.AppendLine("| " + label + " | 스킬 씀 | " + skill.AvgWins.ToString("F2") + " | " + Pct(skill.Rate2) + " | " + Pct(skill.Rate3) + " | " +
                      (skill.energyEmptyRuns > 0 ? skill.AvgEnergyEmptyAt.ToString("F1") + "번째" : "안 바닥남") + " |");
    }

    static string Pct(double v)
    {
        return (v * 100).ToString("F1") + "%";
    }
}

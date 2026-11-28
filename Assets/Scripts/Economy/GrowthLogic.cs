using System.Collections.Generic;

// [C-4] 성장 규칙 (순수 C#): 경험치·레벨·스탯 포인트, 직급 승급, 재능 선택, 무기 숙련도·특성 선택.
// 화면(BattleController·HomeController·PickController)과 테스트가 같이 쓴다. 수치는 EconomySettings.
public static class GrowthLogic
{
    // ---------- 경험치·레벨 ----------

    // 경험치를 더하고 오른 레벨 수를 돌려준다. 레벨마다 스탯 포인트
    public static int AddExp(PlayerState p, EconomySettings eco, int amount)
    {
        if (eco == null || amount <= 0) return 0;

        p.exp += amount;
        int gained = 0;

        while (p.level < eco.maxLevel && p.exp >= eco.ExpToNext(p.level))
        {
            p.exp -= eco.ExpToNext(p.level);
            p.level++;
            p.statPoints += eco.pointsPerLevel;
            gained++;
        }

        if (p.level >= eco.maxLevel) p.exp = 0;
        return gained;
    }

    public static bool AssignPoint(PlayerState p, StatType stat)
    {
        if (p.statPoints <= 0) return false;
        p.statPoints--;
        p.stats.Add(stat, 1);
        return true;
    }

    // ---------- 직급 ----------

    // 조건을 채운 만큼 승급 (한 번에 여러 단계 가능). 오른 직급 목록을 돌려준다. 승급마다 재능 선택 +1
    public static List<RankData> TryPromote(GameState state, EconomySettings eco)
    {
        var promoted = new List<RankData>();
        if (eco == null || eco.ranks.Count == 0) return promoted;

        var p = state.player;

        while (true)
        {
            int cur = eco.ranks.IndexOf(eco.RankOf(p));
            if (cur < 0 || cur + 1 >= eco.ranks.Count) break;

            var next = eco.ranks[cur + 1];
            if (p.level < next.minLevel || !ConditionChecker.Check(state, next.condition)) break;

            p.rankId = next.rankId;
            p.pendingTalentPicks++;
            promoted.Add(next);
        }

        return promoted;
    }

    // ---------- 재능 ----------

    // 지금 고를 재능 후보: 첫 재능은 시작 재능, 그 뒤는 (고른 재능 수)번째 직급의 승급 재능. 이미 가진 것 제외
    public static List<TalentData> TalentOptions(PlayerState p, EconomySettings eco)
    {
        var list = new List<TalentData>();
        if (eco == null || p.pendingTalentPicks <= 0) return list;

        int n = p.talentIds.Count;
        var pool = n == 0 ? eco.startTalents : (n < eco.ranks.Count ? eco.ranks[n].talentChoices : null);
        if (pool == null) return list;

        foreach (var t in pool)
        {
            if (t != null && !p.talentIds.Contains(t.talentId)) list.Add(t);
        }

        return list;
    }

    public static bool ChooseTalent(PlayerState p, EconomySettings eco, string talentId)
    {
        if (!TalentOptions(p, eco).Exists(t => t.talentId == talentId)) return false;
        p.talentIds.Add(talentId);
        p.pendingTalentPicks--;
        return true;
    }

    // ---------- 무기 숙련도·특성 ----------

    // 공격 횟수만큼 숙련도 경험치. 오른 레벨 수를 돌려준다. 특성 레벨에 닿으면 특성 선택 +1
    public static int AddMasteryExp(PlayerState p, EconomySettings eco, WeaponType type, int attacks)
    {
        var rule = eco != null ? eco.MasteryFor(type) : null;
        if (rule == null || attacks <= 0) return 0;

        var m = p.MasteryOf(type);
        m.exp += attacks * rule.expPerAttack;
        int gained = 0;

        while (m.level < rule.maxLevel && m.exp >= rule.expPerLevel)
        {
            m.exp -= rule.expPerLevel;
            m.level++;
            gained++;
            if (rule.traitTiers.Exists(t => t.level == m.level)) m.pendingTraitPicks++;
        }

        if (m.level >= rule.maxLevel) m.exp = 0;
        return gained;
    }

    // 지금 고를 특성 2개 (아직 고르지 않은 가장 낮은 단계)
    public static TraitTier NextTraitTier(PlayerState p, EconomySettings eco, WeaponType type)
    {
        var rule = eco != null ? eco.MasteryFor(type) : null;
        if (rule == null) return null;

        var m = p.MasteryOf(type);
        if (m.pendingTraitPicks <= 0) return null;

        foreach (var tier in rule.traitTiers)
        {
            if (tier.level > m.level) continue;
            bool chosen = (tier.optionA != null && m.traitIds.Contains(tier.optionA.traitId)) ||
                          (tier.optionB != null && m.traitIds.Contains(tier.optionB.traitId));
            if (!chosen) return tier;
        }

        return null;
    }

    public static bool ChooseTrait(PlayerState p, EconomySettings eco, WeaponType type, string traitId)
    {
        var tier = NextTraitTier(p, eco, type);
        if (tier == null) return false;
        if ((tier.optionA == null || tier.optionA.traitId != traitId) && (tier.optionB == null || tier.optionB.traitId != traitId)) return false;

        var m = p.MasteryOf(type);
        m.traitIds.Add(traitId);
        m.pendingTraitPicks--;
        return true;
    }

    // 고를 것이 남아 있는 무기 유형 (없으면 null)
    public static WeaponType? PendingTraitType(PlayerState p, EconomySettings eco)
    {
        foreach (var m in p.masteries)
        {
            if (m.pendingTraitPicks <= 0) continue;
            WeaponType t;
            if (System.Enum.TryParse(m.weaponType, out t) && NextTraitTier(p, eco, t) != null) return t;
        }

        return null;
    }

    // 프로필 빨간 점: 남은 스탯 포인트·재능·특성 선택
    public static bool HasPending(PlayerState p, EconomySettings eco)
    {
        if (p == null) return false;
        return p.statPoints > 0 || TalentOptions(p, eco).Count > 0 || PendingTraitType(p, eco) != null;
    }
}

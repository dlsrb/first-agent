using System;
using System.Collections.Generic;

public enum RewardKind
{
    Normal,      // 일반 전투
    StoryFirst,  // 스토리·보스 첫 클리어
    StoryRetry   // 스토리·보스 재도전 (골드·경험치 × 배율, 드롭 확률은 그대로)
}

// 전투 1회 보상
public class BattleReward
{
    public RewardKind kind;
    public int gold;
    public int exp;
    public readonly List<ItemData> materials = new List<ItemData>();
    public readonly List<ItemData> equipment = new List<ItemData>();
}

// 전투 보상 계산 (순수 C#, 난수 주입). 게임·시뮬레이터·테스트 공용. docs/성장경제규칙.md 1번
public static class RewardCalculator
{
    public static BattleReward Roll(EconomySettings eco, MonsterData monster, RewardKind kind, Random rng)
    {
        var r = new BattleReward { kind = kind };

        if (eco == null || monster == null)
        {
            return r;
        }

        int lo = Math.Min(monster.goldMin, monster.goldMax);
        int hi = Math.Max(monster.goldMin, monster.goldMax);
        r.gold = rng.Next(lo, hi + 1);
        r.exp = monster.exp;

        if (kind == RewardKind.StoryRetry)
        {
            r.gold = (int)Math.Round(r.gold * eco.retryRewardRatio, MidpointRounding.AwayFromZero);
            r.exp = (int)Math.Round(r.exp * eco.retryRewardRatio, MidpointRounding.AwayFromZero);
        }

        var chances = kind == RewardKind.Normal ? eco.normalDrops : eco.storyDrops;

        if (eco.materialItem != null && rng.NextDouble() < chances.material)
        {
            r.materials.Add(eco.materialItem);
        }

        if (rng.NextDouble() < chances.advanced)
        {
            AddFromPool(r, eco.advancedPool, rng);
        }

        if (rng.NextDouble() < chances.rare)
        {
            AddFromPool(r, eco.rarePool, rng);
        }

        return r;
    }

    static void AddFromPool(BattleReward r, List<ItemData> pool, Random rng)
    {
        var valid = pool.FindAll(i => i != null);

        if (valid.Count > 0)
        {
            r.equipment.Add(valid[rng.Next(valid.Count)]);
        }
    }

    // 보상을 주인공에게 넣는다 (장비는 장착하지 않고 소지)
    public static void Apply(PlayerState p, BattleReward r)
    {
        p.gold += r.gold;
        p.exp += r.exp;

        foreach (var m in r.materials)
        {
            p.AddItem(m.itemId, 1);
        }

        foreach (var e in r.equipment)
        {
            p.AddEquipment(e.itemId);
        }
    }

    // 결과 창 문구
    public static string Describe(BattleReward r)
    {
        var lines = new List<string>();

        if (r.kind == RewardKind.StoryFirst) lines.Add("첫 클리어!");
        if (r.kind == RewardKind.StoryRetry) lines.Add("재도전 클리어 (보상 감소)");

        lines.Add("골드 +" + r.gold + "   경험치 +" + r.exp);

        var got = new List<string>();
        foreach (var m in r.materials) got.Add(m.itemName);
        foreach (var e in r.equipment) got.Add(e.itemName);

        if (got.Count > 0)
        {
            lines.Add("획득: " + string.Join(", ", got));
        }

        return string.Join("\n", lines);
    }
}

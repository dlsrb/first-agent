using System.Collections.Generic;

// 범용 조건 (설계안 6번). 선택지·장소·상호작용·상점 품목·직급 어디에나 붙인다.
// [C-2] 구조와 판정 틀만. 스탯(매력 등)·재능 판정은 C-4에서 스탯이 생기면 붙인다 (지금은 스탯 0으로 판정).
public enum ConditionKind
{
    StatAtLeast,   // stat 이 value 이상 (C-4)
    LevelAtLeast,  // 레벨이 value 이상
    FlagSet,       // key 플래그가 있음
    FlagNotSet,    // key 플래그가 없음
    HasItem,       // key 아이템(소비·재료)을 value개 이상, 또는 장비를 1개 이상
    TalentOwned    // key 재능 보유 (C-4)
}

public enum StatType
{
    Strength,   // 힘
    Agility,    // 민첩
    Spirit,     // 정신력
    Intellect,  // 지능
    Charm       // 매력
}

[System.Serializable]
public class Condition
{
    public ConditionKind kind = ConditionKind.StatAtLeast;
    public StatType stat = StatType.Charm;
    public int value = 1;
    public string key = "";
}

[System.Serializable]
public class ConditionSet
{
    [UnityEngine.Tooltip("모두 만족해야 함. 비어 있으면 항상 만족")]
    public List<Condition> all = new List<Condition>();

    [UnityEngine.Tooltip("만족하지 못하면 아예 숨김 (히든 콘텐츠). 끄면 잠긴 채로 보임")]
    public bool hideIfUnmet = false;

    [UnityEngine.Tooltip("잠긴 채로 보일 때 문구. 비어 있으면 조건으로 자동 생성 (예: [매력 5 필요])")]
    public string lockedText = "";

    public bool IsEmpty => all == null || all.Count == 0;
}

public static class ConditionChecker
{
    public static bool Check(GameState state, ConditionSet set)
    {
        if (set == null || set.IsEmpty)
        {
            return true;
        }

        foreach (var c in set.all)
        {
            if (c != null && !Check(state, c))
            {
                return false;
            }
        }

        return true;
    }

    public static bool Check(GameState state, Condition c)
    {
        var p = state != null ? state.player : null;

        switch (c.kind)
        {
            case ConditionKind.StatAtLeast:
                return StatValue(p, c.stat) >= c.value;
            case ConditionKind.LevelAtLeast:
                return p != null && p.level >= c.value;
            case ConditionKind.FlagSet:
                return state != null && state.flags.Contains(c.key);
            case ConditionKind.FlagNotSet:
                return state == null || !state.flags.Contains(c.key);
            case ConditionKind.HasItem:
                return p != null && (p.CountOf(c.key) >= System.Math.Max(1, c.value) || p.equipment.Exists(e => e.itemId == c.key));
            case ConditionKind.TalentOwned:
                return false;   // C-4
        }

        return false;
    }

    // C-4에서 PlayerState의 스탯으로 교체. 지금은 스탯이 없으므로 0
    public static int StatValue(PlayerState p, StatType stat)
    {
        return 0;
    }

    // 잠긴 채 보일 때 문구
    public static string LockedText(ConditionSet set)
    {
        if (set == null) return "";
        if (!string.IsNullOrEmpty(set.lockedText)) return set.lockedText;

        var parts = new List<string>();

        foreach (var c in set.all)
        {
            if (c == null) continue;
            if (c.kind == ConditionKind.StatAtLeast) parts.Add(DisplayNames.Stat(c.stat) + " " + c.value);
            else if (c.kind == ConditionKind.LevelAtLeast) parts.Add("레벨 " + c.value);
        }

        return parts.Count > 0 ? "[" + string.Join(", ", parts) + " 필요]" : "[잠김]";
    }
}

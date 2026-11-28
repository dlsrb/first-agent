using System.Collections.Generic;
using UnityEngine;

// [C-4] 성장 저장 형식: 스탯 묶음, 무기 숙련도 상태 (PlayerState 안에 저장)

// 스탯 5개 (JsonUtility 저장용 — 칸 이름은 출시 후 바꾸지 말 것)
[System.Serializable]
public class StatBlock
{
    public int strength;
    public int agility;
    public int spirit;
    public int intellect;
    public int charm;

    public int Get(StatType t)
    {
        switch (t)
        {
            case StatType.Strength: return strength;
            case StatType.Agility: return agility;
            case StatType.Spirit: return spirit;
            case StatType.Intellect: return intellect;
            default: return charm;
        }
    }

    public void Add(StatType t, int n)
    {
        switch (t)
        {
            case StatType.Strength: strength += n; break;
            case StatType.Agility: agility += n; break;
            case StatType.Spirit: spirit += n; break;
            case StatType.Intellect: intellect += n; break;
            default: charm += n; break;
        }
    }

    public StatBlock Copy()
    {
        return (StatBlock)MemberwiseClone();
    }
}

// [11/28] 스탯 직접 증가 효과 (아이템 사용·대화 선택지 결과에 붙인다. 예: 매력 +1)
[System.Serializable]
public class StatGain
{
    public StatType stat = StatType.Charm;
    public int amount = 1;
}

// [11/28] 스탯 하나의 훈련 기록 (하루 한도 계산용, PlayerState.trainDays)
[System.Serializable]
public class TrainDay
{
    public string stat = "";
    public string date = "";   // yyyy-MM-dd (기기 시계)
    public int count = 0;      // 그날 성공 횟수
}

// 무기 유형 하나의 숙련도 (PlayerState.masteries)
[System.Serializable]
public class MasteryState
{
    public string weaponType = "";
    public int level = 0;
    public int exp = 0;
    public List<string> traitIds = new List<string>();
    public int pendingTraitPicks = 0;

    public MasteryState Copy()
    {
        var m = (MasteryState)MemberwiseClone();
        m.traitIds = new List<string>(traitIds);
        return m;
    }
}


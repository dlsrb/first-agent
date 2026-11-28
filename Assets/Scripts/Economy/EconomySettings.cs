using System.Collections.Generic;
using UnityEngine;

// 성장·경제 수치 한곳 (에셋 1개). docs/성장경제규칙.md·docs/성장경제설계안.md의 임시 값은 전부 여기서 조정한다.
// C-1: 보상·드롭·판매·강화. 직급·병원(C-2), 레벨·스탯·재능·숙련도(C-4), 훈련(C-5)은 해당 단계에서 칸을 추가한다.
[CreateAssetMenu(fileName = "EconomySettings", menuName = "요원/성장·경제 설정 (EconomySettings)")]
public class EconomySettings : ScriptableObject
{
    [Header("전투 보상 확률 (전투 종류별)")]
    public DropChances normalDrops = new DropChances { material = 0.15f, advanced = 0.05f, rare = 0f };
    [Tooltip("스토리·보스 첫 클리어. 재도전도 확률은 그대로")]
    public DropChances storyDrops = new DropChances { material = 0.20f, advanced = 0.10f, rare = 0.03f };
    [Tooltip("스토리·보스 재도전 시 골드·경험치 배율")]
    [Range(0, 1)] public float retryRewardRatio = 0.3f;

    [Header("드롭 아이템")]
    public ItemData materialItem;
    [Tooltip("고급 장비 드롭 목록 (균등 랜덤)")]
    public List<ItemData> advancedPool = new List<ItemData>();
    [Tooltip("희귀 장비 드롭 목록 (균등 랜덤)")]
    public List<ItemData> rarePool = new List<ItemData>();

    [Header("상점")]
    [Tooltip("판매가 = 구매가 × 이 값 (아이템에 판매가가 따로 있으면 그 값)")]
    [Range(0, 1)] public float sellRatio = 0.5f;

    [Header("강화 (0번 = +1 단계 비용)")]
    public List<EnhanceStep> enhanceSteps = new List<EnhanceStep>
    {
        new EnhanceStep { gold = 100, materials = 1 },
        new EnhanceStep { gold = 150, materials = 1 },
        new EnhanceStep { gold = 200, materials = 2 },
        new EnhanceStep { gold = 300, materials = 2 },
        new EnhanceStep { gold = 400, materials = 3 }
    };

    [Header("직급 (C-2: 병원비. 승급 판정은 C-4)")]
    public List<RankData> ranks = new List<RankData>
    {
        new RankData { rankId = "trainee", rankName = "수습 요원", minLevel = 1, hospitalCost = 10 },
        new RankData { rankId = "agent", rankName = "요원", minLevel = 10, hospitalCost = 100 },
        new RankData { rankId = "senior", rankName = "선임 요원", minLevel = 20, hospitalCost = 1000 }
    };

    [Header("[C-4] 레벨 (다음 레벨까지 = 기본 + 증가 × (레벨 − 1))")]
    public int expBase = 20;
    public int expStep = 10;
    public int maxLevel = 30;
    public int pointsPerLevel = 1;

    [Header("[C-4] 스탯 효과 (1포인트당)")]
    public int attackPerStrength = 1;
    [Tooltip("민첩 1 = 도망 확률 +BattleSettings.fleeChancePerAgility, 치명 +이 값")]
    public float critPerAgility = 0.005f;
    public int hpPerSpirit = 5;
    public float debuffResistPerSpirit = 0.01f;
    public float debuffResistMax = 0.3f;
    public int energyPerIntellect = 1;
    [Tooltip("전투 시작 시 괴이 정보 자동 획득 확률")]
    public float infoChancePerIntellect = 0.03f;
    [Tooltip("상점 가격 할인 (매력 1부터)")]
    public float discountPerCharm = 0.01f;
    public float discountMax = 0.2f;

    [Header("[C-6] 게임 시작 흐름")]
    [Tooltip("시작 만화 (비어 있으면 건너뜀)")]
    public ComicData openingComic;
    [Tooltip("시작 흐름 동안 지역 맵에서 열려 있는 구역")]
    public AreaData startArea;
    [Tooltip("시작 흐름 동안 열려 있는 유일한 장소. 들어가면 모든 장소가 열린다")]
    public PlaceData startHospital;

    [Header("[11/28] 훈련 한도 (오버스탯 방지)")]
    [Tooltip("스탯마다 하루(기기 날짜, 자정 초기화)에 성공할 수 있는 훈련 횟수")]
    public int trainDailyLimit = 1;
    [Tooltip("훈련으로 올릴 수 있는 양의 총합 (스탯마다). 레벨 포인트·재능·아이템은 별도")]
    public int trainTotalLimit = 10;

    [Header("[C-4] 재능 (시작 재능. 승급 재능은 직급 칸)")]
    public List<TalentData> startTalents = new List<TalentData>();

    [Header("[C-4] 무기 숙련도")]
    public List<WeaponMasteryData> masteries = new List<WeaponMasteryData>();

    public int MaxEnhance => enhanceSteps.Count;

    public int ExpToNext(int level)
    {
        return expBase + expStep * (level - 1);
    }

    public WeaponMasteryData MasteryFor(WeaponType type)
    {
        return masteries.Find(m => m != null && m.weaponType == type);
    }

    public TalentData FindTalent(string id)
    {
        var t = startTalents.Find(x => x != null && x.talentId == id);
        if (t != null) return t;
        foreach (var r in ranks)
        {
            t = r.talentChoices.Find(x => x != null && x.talentId == id);
            if (t != null) return t;
        }
        return null;
    }

    public TraitData FindTrait(string id)
    {
        foreach (var m in masteries)
        {
            if (m == null) continue;
            foreach (var tier in m.traitTiers)
            {
                if (tier.optionA != null && tier.optionA.traitId == id) return tier.optionA;
                if (tier.optionB != null && tier.optionB.traitId == id) return tier.optionB;
            }
        }
        return null;
    }

    // 주인공의 직급 (rankId가 없거나 비면 첫 직급)
    public RankData RankOf(PlayerState p)
    {
        if (ranks.Count == 0) return null;
        var r = p != null ? ranks.Find(x => x.rankId == p.rankId) : null;
        return r ?? ranks[0];
    }

    public int HospitalCost(PlayerState p)
    {
        var r = RankOf(p);
        return r != null ? r.hospitalCost : 0;
    }

    // 다음 강화 비용 (최대면 null)
    public EnhanceStep NextEnhance(int currentLevel)
    {
        return currentLevel >= 0 && currentLevel < enhanceSteps.Count ? enhanceSteps[currentLevel] : null;
    }

    public int SellPrice(ItemData item)
    {
        if (item == null) return 0;
        return item.sellPriceOverride > 0 ? item.sellPriceOverride : Mathf.FloorToInt(item.price * sellRatio);
    }
}

[System.Serializable]
public class DropChances
{
    [Range(0, 1)] public float material;
    [Range(0, 1)] public float advanced;
    [Range(0, 1)] public float rare;
}

[System.Serializable]
public class EnhanceStep
{
    public int gold;
    public int materials;
}

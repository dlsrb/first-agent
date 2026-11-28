using System.Collections.Generic;
using UnityEngine;

// 전투 수치 한곳 (에셋 1개). 전투규칙.md의 "(임시)" 값은 전부 여기서 조정한다.
[CreateAssetMenu(fileName = "BattleSettings", menuName = "요원/전투/전투 설정 (BattleSettings)")]
public class BattleSettings : ScriptableObject
{
    [Header("주사위 조합 배수 (0번 = 주사위 2개, 1번 = 3개 …, 목록보다 많으면 마지막 값)")]
    public List<float> comboMultipliers = new List<float> { 2f, 5f, 10f, 20f };

    [Header("피해")]
    public float critMultiplier = 1.5f;
    public int minDamage = 1;

    [Header("방어")]
    [Range(0, 1)] public float defenseChanceBase = 0.30f;
    [Range(0, 1)] public float defenseChanceCap = 0.95f;
    public float touchWindowSeconds = 2f;
    [Tooltip("연속 터치 1회당 오르는 확률")]
    public float touchIncrement = 0.015f;
    [Tooltip("연속 터치 보너스 상한: 괴이 공격력이 Low 이하면 Max, High 이상이면 Min, 사이는 직선")]
    public float touchBonusMax = 0.15f;
    public float touchBonusMin = 0.05f;
    public float touchBonusAttackLow = 10f;
    public float touchBonusAttackHigh = 30f;

    [Header("도망")]
    [Range(0, 1)] public float fleeChanceBase = 0.40f;
    [Range(0, 1)] public float fleeChanceMax = 0.90f;
    [Tooltip("민첩 1당 도망 확률")]
    public float fleeChancePerAgility = 0.01f;
    public float fleeFailDamageMultiplier = 2f;

    [Header("특수 행동")]
    [Tooltip("반사 자세 때 공격하면 내 피해 × 이 값을 내가 받음")]
    public float reflectRatio = 1f;
    [Tooltip("도발하기로 괴이에게 거는 효과")]
    public StatusEffectData tauntEffect;
    [Tooltip("예고 대응 성공(맞는 특수 행동으로 디버프를 막거나 반사 자세에 가만히 있기) 시 괴이에게 거는 효과 (빈틈)")]
    public StatusEffectData counterOpeningEffect;

    [Header("전투 후")]
    [Range(0, 1)] public float defeatGoldLossRatio = 0.10f;
    [Range(0, 1)] public float reviveHpRatio = 0.30f;
    [Tooltip("기본 복귀 장소 (게임 시작 병원)")]
    public AreaData defaultRespawnArea;
    public PlaceData defaultRespawnPlace;

    [Header("온라인용 칸 (지금 사용 안 함). 재도전 보상 배율은 EconomySettings")]
    public float battleCooldownSeconds = 0f;

    [Header("[C-5] 화면 가장자리 연출 (UI규칙 3번)")]
    [Tooltip("이 체력 비율 이하부터 빨간 가장자리")]
    [Range(0, 1)] public float lowHpThreshold = 0.3f;
    public Color lowHpColor = new Color(0.85f, 0.05f, 0.05f, 1f);
    [Tooltip("기준 체력일 때 진하기")]
    [Range(0, 1)] public float lowHpAlphaMin = 0.12f;
    [Tooltip("체력 0에 가까울 때 진하기 (글자·버튼을 가리지 않게 낮게)")]
    [Range(0, 1)] public float lowHpAlphaMax = 0.28f;
    [Tooltip("정신 계열 디버프 동안 검은 가장자리")]
    public Color mentalColor = new Color(0f, 0f, 0f, 1f);
    [Range(0, 1)] public float mentalAlpha = 0.4f;
    [Tooltip("나타나고 사라지는 속도 (진하기/초)")]
    public float edgeFadeSpeed = 0.8f;
    [Tooltip("가장자리 두께 (0 = 가장자리만, 1 = 가운데까지)")]
    [Range(0.05f, 1f)] public float edgeWidth = 0.25f;

    [Header("주인공 시작 수치")]
    public PlayerBase playerBase = new PlayerBase();
}

// 장비·스탯을 빼고 남는 주인공 기본 수치 (C-1: 시작 장비 일반 무기 +2·일반 방어구 +4를 더하면 B단계 시작값 공격 5·방어 10)
[System.Serializable]
public class PlayerBase
{
    public int maxHp = 100;
    public int maxEnergy = 10;
    public int attack = 3;
    public int defense = 6;
    [Range(0, 1)] public float critChance = 0.05f;
    public int agility = 0;
    public int diceCount = 2;
    public int diceFaces = 6;
    [UnityEngine.Tooltip("C-1부터 스킬은 무기에서만 얻는다. 시작 스킬은 비워 둔다 (테스트용 칸)")]
    public List<SkillData> startSkills = new List<SkillData>();
    [UnityEngine.Tooltip("시작 장비 (장착 상태로 지급)")]
    public ItemData startWeapon;
    public ItemData startArmor;
    public List<StartItem> startItems = new List<StartItem>();
}

[System.Serializable]
public class StartItem
{
    public ItemData item;
    public int count = 1;
}

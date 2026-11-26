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

    [Header("C단계·온라인용 칸 (지금 사용 안 함)")]
    [Range(0, 1)] public float retryRewardRatio = 0.2f;
    public float battleCooldownSeconds = 0f;

    [Header("주인공 시작 수치")]
    public PlayerBase playerBase = new PlayerBase();
}

[System.Serializable]
public class PlayerBase
{
    public int maxHp = 100;
    public int maxEnergy = 10;
    public int attack = 5;
    public int defense = 10;
    [Range(0, 1)] public float critChance = 0.05f;
    public int agility = 0;
    public int diceCount = 2;
    public int diceFaces = 6;
    public List<SkillData> startSkills = new List<SkillData>();
    public List<StartItem> startItems = new List<StartItem>();
}

[System.Serializable]
public class StartItem
{
    public ItemData item;
    public int count = 1;
}

using System.Collections.Generic;
using UnityEngine;

// [C-4] 재능 데이터 (클래스 이름 = 파일 이름이어야 에셋이 연결됨)

// 재능·특성 효과 대상 (값 순서는 에셋에 숫자로 저장되므로 끝에만 추가)
public enum ModTarget
{
    MaxHp,               // 최대 체력 +값
    Attack,              // 공격 +값
    CritPoints,          // 치명 +값 %p
    AllStats,            // 스탯 5개 모두 +값
    EnhanceGoldPercent,  // 강화 골드 −값 %
    DropRatePercent,     // 드롭 확률 +값 %
    ExpPercent,          // 경험치 +값 %
    DebuffResistPoints   // 디버프 무시 +값 %p
}

[System.Serializable]
public class StatModifier
{
    public ModTarget target = ModTarget.MaxHp;
    public float value = 0f;
}

// 재능 1개 (에셋 1개). 시작 재능(1단계)과 승급 재능(2단계~)
[CreateAssetMenu(fileName = "Talent_New", menuName = "요원/성장/재능 (TalentData)")]
public class TalentData : ScriptableObject
{
    public string talentId = "";
    public string talentName = "[재능 이름 미정]";
    [TextArea(2, 4)] public string description = "[재능 설명 미정]";
    public int tier = 1;
    public List<StatModifier> effects = new List<StatModifier>();
}


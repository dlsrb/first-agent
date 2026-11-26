using UnityEngine;

// 특수 행동 (전투 중 특수 행동 창). None은 "막는 행동 없음" 표시용.
public enum SpecialAction
{
    None,
    Wait,        // 가만히 있기: 반사 자세 대응
    Taunt,       // 도발하기: 괴이에게 도발 상태 효과
    CoverEars,   // 귀막기: 소리 계열
    HoldBreath,  // 호흡참기: 독기·연기 계열
    CoverMouth,  // 입막기: 포자·가루 계열
    EatFood      // 음식먹기: 소비 아이템
}

// 디버프·버프 공용 상태 효과 (에셋 1개 = 효과 1종). 주인공·괴이 모두에게 걸 수 있다.
[CreateAssetMenu(fileName = "Effect_New", menuName = "요원/전투/상태 효과 (StatusEffectData)")]
public class StatusEffectData : ScriptableObject
{
    public string effectId = "";
    public string effectName = "[효과 이름 미정]";
    public Sprite icon;

    public bool isDebuff = true;

    [Tooltip("지속 턴 수")]
    public int duration = 3;

    [Header("효과 (0이면 없음)")]
    [Tooltip("공격 피해 ±%. 주인공: 공격 피해 전체에 적용 / 괴이: 공격력에 적용")]
    public float attackPercent = 0f;

    [Tooltip("방어력 ±% (괴이 방어력·주인공 방어 스탯)")]
    public float defensePercent = 0f;

    [Tooltip("방어 확률 ±%p (주인공 방어 행동)")]
    public float defenseChancePoints = 0f;

    [Tooltip("턴 끝마다 받는 피해")]
    public int damagePerTurn = 0;

    [Tooltip("턴 끝마다 회복")]
    public int healPerTurn = 0;

    [Header("대응")]
    [Tooltip("이 효과를 막는 특수 행동 (그 턴에 쓰면 걸리지 않음)")]
    public SpecialAction blockedBy = SpecialAction.None;
}

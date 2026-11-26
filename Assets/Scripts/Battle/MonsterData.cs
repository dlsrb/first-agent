using System.Collections.Generic;
using UnityEngine;

public enum MonsterActionKind
{
    Attack,   // 피해
    Debuff,   // 주인공에게 상태 효과 (damageMultiplier > 0이면 피해도 줌)
    Reflect,  // 반사 자세: 이번 턴 주인공이 공격하면 피해가 반사됨
    Buff      // 괴이 자신에게 상태 효과
}

public enum ActionCondition
{
    None,
    HpBelow,   // 괴이 체력이 hpBelowPercent% 이하일 때만
    NoRepeat   // 바로 전 턴과 같은 행동이면 고르지 않음
}

// 괴이 행동 1개
[System.Serializable]
public class MonsterAction
{
    public string actionId = "";
    public string actionName = "[행동 이름 미정]";

    public MonsterActionKind kind = MonsterActionKind.Attack;

    [Tooltip("행동 배율 (팔 휘두르기 1.0, 돌진 1.5). Debuff에서 0이면 피해 없음")]
    public float damageMultiplier = 1f;

    [Tooltip("Debuff·Buff일 때 거는 효과")]
    public StatusEffectData effect;

    [Tooltip("선택 가중치")]
    public int weight = 1;

    public ActionCondition condition = ActionCondition.None;

    [Range(0, 100)]
    public float hpBelowPercent = 50f;

    [Tooltip("정보 전 예고 (예: 괴이가 숨을 크게 들이쉰다.)")]
    [TextArea(1, 3)]
    public string telegraphVague = "[예고 미정]";

    [Tooltip("정보 후 예고 (예: 소리를 질러 사기를 떨어뜨리려 한다.)")]
    [TextArea(1, 3)]
    public string telegraphClear = "[정확한 예고 미정]";
}

public enum PhaseTrigger
{
    HpBelow,      // 체력 n% 이하
    AfterAction   // 특정 행동(actionId)을 한 뒤
}

// 보스 페이즈 전환 1개 (phases[0] = 2페이즈로 넘어가는 조건과 효과)
[System.Serializable]
public class BossPhase
{
    public string phaseName = "[페이즈 이름 미정]";

    public PhaseTrigger trigger = PhaseTrigger.HpBelow;

    [Range(0, 100)]
    public float hpPercent = 50f;

    public string afterActionId = "";

    [Tooltip("전환 만화")]
    public ComicData comic;

    [Tooltip("전환 후 공격력·방어력 배율 (기본 수치 기준)")]
    public float attackMultiplier = 1f;
    public float defenseMultiplier = 1f;

    [Tooltip("비어 있지 않으면 행동 패턴을 이 목록으로 교체")]
    public List<MonsterAction> actions = new List<MonsterAction>();

    [Tooltip("전환할 때 메시지 줄에 보여 줄 문구")]
    [TextArea(1, 3)]
    public string phaseMessage = "";

    [Tooltip("[B-4] 이 페이즈 동안 예고 말풍선 맨 위에 항상 붙는 문구 (예: 도망칠 준비를 한다). 텍스트만, 기믹 없음")]
    [TextArea(1, 3)]
    public string intentNote = "";
}

// 괴이 1종 (에셋 1개). 이미지·이름·설명은 비워 두고 나중에 채운다.
[CreateAssetMenu(fileName = "Monster_New", menuName = "요원/전투/괴이 (MonsterData)")]
public class MonsterData : ScriptableObject
{
    [Header("식별·표시")]
    [Tooltip("괴이 고유 ID (영문 소문자, ':' 금지). 정보 플래그 키에 쓰이므로 출시 후 변경 금지")]
    public string monsterId = "";
    public string monsterName = "[괴이 이름 미정]";

    [TextArea(2, 4)]
    public string description = "[괴이 설명 미정]";

    public Sprite image;
    public AudioClip bgm;
    public AudioClip sfx;

    [Header("수치")]
    public int maxHp = 40;
    public int attack = 14;
    public int defense = 10;

    [Header("보상 (C-1, 스토리·보스는 첫 클리어 기준. 재도전은 EconomySettings 배율)")]
    public int goldMin = 35;
    public int goldMax = 50;
    public int exp = 10;

    [Header("전투 규칙")]
    [Tooltip("먼저 행동하는 괴이 (첫 행동은 예고 없이 실행)")]
    public bool firstStrike = false;

    [Tooltip("도망칠 수 있는지 (보스·스토리는 false)")]
    public bool canFlee = true;

    [Header("행동 패턴 (보스는 1페이즈)")]
    public List<MonsterAction> actions = new List<MonsterAction>();

    [Header("보스 페이즈 전환 (비어 있으면 일반 괴이)")]
    public List<BossPhase> phases = new List<BossPhase>();

    public bool IsBoss => phases.Count > 0;

    void OnValidate()
    {
        if (!StateKeys.IsValidId(monsterId))
        {
            Debug.LogWarning(name + ": monsterId가 비었거나 ':'가 들어 있습니다.", this);
        }
    }
}

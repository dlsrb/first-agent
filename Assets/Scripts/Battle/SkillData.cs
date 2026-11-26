using UnityEngine;

public enum SkillKind
{
    Reroll,   // 다시 굴리기: 이번 공격 주사위를 한 번 다시 굴림
    LockDie   // 주사위 고정: 다음 공격에서 주사위 1개를 지금 값으로 고정
}

public enum SkillTiming
{
    AfterRoll,  // 주사위를 굴린 뒤, 피해 확정 전 (공격 기믹 영역에서 사용)
    Turn        // 턴을 쓰는 스킬 (특수 행동 창에서 사용) — 첫 출시 임시 스킬에는 없음
}

// 스킬 (에셋 1개 = 스킬 1종). 무기에서 얻고 기력을 쓴다. 장착 무기 기준 최대 2개.
[CreateAssetMenu(fileName = "Skill_New", menuName = "요원/전투/스킬 (SkillData)")]
public class SkillData : ScriptableObject
{
    public string skillId = "";
    public string skillName = "[스킬 이름 미정]";

    [TextArea(2, 4)]
    public string description = "[스킬 설명 미정]";

    public Sprite icon;

    public int energyCost = 2;

    public SkillKind kind = SkillKind.Reroll;
    public SkillTiming timing = SkillTiming.AfterRoll;
}

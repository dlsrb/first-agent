// 진행 상태(GameState.flags) 키는 반드시 여기서만 만든다. 오타·형식 불일치 방지.
// 형식: "종류:장소ID:상호작용ID[:추가정보]"  — ID에는 ':'를 쓰지 않는다.
public static class StateKeys
{
    // 대화에서 선택지를 골랐음.            예: "talk:normal:clerk"
    public static string TalkChosen(string locationId, string interactionId)
    {
        return "talk:" + locationId + ":" + interactionId;
    }

    // 어떤 선택지를 골랐는지 (0 = A, 1 = B). 예: "talk:normal:clerk:choice=0"
    public static string TalkChoice(string locationId, string interactionId, int choiceIndex)
    {
        return TalkChosen(locationId, interactionId) + ":choice=" + choiceIndex;
    }

    // [B-1] 괴이 정보를 얻었음 (예고 텍스트가 정확해지고 공격력·방어력 공개). 예: "info:monster:urban_1"
    public static string MonsterInfo(string monsterId)
    {
        return "info:monster:" + monsterId;
    }

    // [B-1] 스토리·보스 전투 첫 클리어 (C단계 재도전 보상 감소용). 예: "battle:cleared:mountain_peak"
    public static string BattleCleared(string locationId)
    {
        return "battle:cleared:" + locationId;
    }

    // [B-4] 보스를 처음 쓰러뜨림 (장소와 별개로 괴이 기준). 예: "boss:defeated:boss_1"
    public static string BossDefeated(string monsterId)
    {
        return "boss:defeated:" + monsterId;
    }

    // 키에 쓸 수 있는 ID인지 (비어 있지 않고 ':' 없음)
    public static bool IsValidId(string id)
    {
        return !string.IsNullOrEmpty(id) && !id.Contains(":");
    }
}

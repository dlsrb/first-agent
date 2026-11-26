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

    // 키에 쓸 수 있는 ID인지 (비어 있지 않고 ':' 없음)
    public static bool IsValidId(string id)
    {
        return !string.IsNullOrEmpty(id) && !id.Contains(":");
    }
}

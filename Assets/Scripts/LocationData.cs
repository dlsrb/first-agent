using UnityEngine;

public class LocationData : MonoBehaviour
{
    public enum LocationType
    {
        Normal,
        Safe,
        Danger,
        Special
    }


    [Header("장소 정보")]
    [Tooltip("진행 상태 저장용 고유 ID (영문 소문자, 장소마다 달라야 함). 출시 후에는 바꾸지 말 것 - 세이브가 깨짐")]
    public string locationId = "";

    public string locationName = "새로운 장소";

    public LocationType locationType = LocationType.Normal;

    [TextArea(2, 5)]
    public string description = "장소 설명을 입력하세요.";

    [Header("상호작용 결과")]

    [TextArea(2, 5)]
    public string shelfResult = "주변을 조사했다.";

    [TextArea(2, 5)]
    public string clerkResult = "상대방과 대화를 나눴다.";

    [TextArea(2, 5)]
    public string suspiciousPersonResult = "수상한 인물을 조사했다.";

    [Header("점원 대화 선택지")]

    // [9일차 추가] 대화창에 표시할 NPC 이름
    public string npcName = "점원";

    public string choiceAText = "A. 요즘 이상한 일 없었어요?";

    [TextArea(2, 5)]
    public string choiceAResult = "점원은 잠시 머뭇거리다 고개를 저었다.";

    public string choiceBText = "B. 그냥 둘러볼게요.";

    [TextArea(2, 5)]
    public string choiceBResult = "점원은 고개를 끄덕이고 하던 일을 계속했다.";

    // [10일차 추가] 이미 선택지를 고른 뒤 다시 말을 걸었을 때의 대사
    [TextArea(2, 5)]
    public string clerkRepeatDialogue = "\"또 오셨네요. 필요한 게 있으면 말씀하세요.\"";

    // [A단계] 진행 상태(점원 선택 여부 등)는 GameStateManager로 이동

}

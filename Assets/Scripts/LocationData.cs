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

    // [10일차 추가] 상태: 이 장소에서 점원 선택지를 이미 골랐는지
    // Inspector에 표시되지 않고, Play를 다시 시작하면 false로 돌아간다
    [System.NonSerialized]
    public bool hasChosenClerkChoice = false;

}

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

}

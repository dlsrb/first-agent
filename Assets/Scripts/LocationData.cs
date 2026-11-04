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


}

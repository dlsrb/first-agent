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


}

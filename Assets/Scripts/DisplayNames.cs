// 화면에 표시할 유형 이름은 여기서만 관리한다 (enum 값 → 한글).
// 새 유형을 enum에 추가하면 여기에도 한 줄 추가한다. 빠지면 영어 이름이 그대로 나온다.
public static class DisplayNames
{
    public static string PlaceType(PlaceData.PlaceType type)
    {
        switch (type)
        {
            case PlaceData.PlaceType.Normal: return "일반";
            case PlaceData.PlaceType.Safe: return "안전";
            case PlaceData.PlaceType.Danger: return "위험";
            case PlaceData.PlaceType.Special: return "특별";
            default: return type.ToString();
        }
    }

    public static string AreaType(AreaData.AreaType type)
    {
        switch (type)
        {
            case AreaData.AreaType.Urban: return "도심";
            case AreaData.AreaType.Mountain: return "산";
            case AreaData.AreaType.River: return "강";
            default: return type.ToString();
        }
    }

    // 지도 정보창 유형 줄
    public static string PlaceTypeLine(PlaceData.PlaceType type)
    {
        return "장소 유형: " + PlaceType(type);
    }

    public static string AreaTypeLine(AreaData.AreaType type)
    {
        return "구역 유형: " + AreaType(type);
    }
}

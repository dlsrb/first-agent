using UnityEngine;

// 지도 위 마커 1개 (MapMarker 프리팹). MapView가 데이터대로 만든다.
// 장소(Place) 또는 구역(Area) 중 하나를 가리킨다.
public class MapMarker : MonoBehaviour
{
    [Tooltip("선택 시 켜지는 흰 테두리 (자식 오브젝트)")]
    public GameObject selectionOutline;

    [Tooltip("선택 시 확대 배율")]
    public float selectedScale = 1.15f;

    public PlaceData Place { get; private set; }
    public AreaData Area { get; private set; }

    public bool IsArea => Area != null;

    // [C-6] 시작 흐름 동안 잠긴 마커 (회색, 진입 불가)
    public bool Locked { get; private set; }
    public static readonly Color LockedColor = new Color(0.45f, 0.45f, 0.45f);

    public void SetLocked(bool locked)
    {
        Locked = locked;
        if (!locked) return;
        var sr = GetComponent<SpriteRenderer>();
        if (sr != null) sr.color = LockedColor;
    }

    public string DisplayName => IsArea ? Area.areaName : (Place != null ? Place.placeName : "");
    // [A-5] 한글 표시 (DisplayNames에서 관리)
    public string TypeText => IsArea
        ? DisplayNames.AreaTypeLine(Area.areaType)
        : (Place != null ? DisplayNames.PlaceTypeLine(Place.placeType) : "");
    public string Description => IsArea ? Area.description : (Place != null ? Place.description : "");

    private Vector3 baseScale = Vector3.one;

    public void Setup(PlaceData place, AreaData area, Color color)
    {
        Place = place;
        Area = area;
        baseScale = transform.localScale;

        SpriteRenderer spriteRenderer = GetComponent<SpriteRenderer>();

        if (spriteRenderer != null)
        {
            spriteRenderer.color = color;
        }

        gameObject.name = "Marker_" + (IsArea ? area.areaId : (place != null ? place.locationId : "empty"));
        SetSelected(false);
    }

    // 색을 덧칠하지 않고 테두리 + 확대로 선택 표시 (유형 색과 겹치지 않음)
    public void SetSelected(bool selected)
    {
        if (selectionOutline != null)
        {
            selectionOutline.SetActive(selected);
        }

        transform.localScale = selected ? baseScale * selectedScale : baseScale;
    }
}

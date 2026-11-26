using System.Collections.Generic;
using UnityEngine;

// 지역 맵 / 구역 맵을 데이터대로 그린다 (배경 + 마커). 씬의 MapView 오브젝트에 부착.
// 마커 위치는 데이터의 비율 좌표(0~1)를 mapSize 크기의 지도 영역 안으로 변환한다.
public class MapView : MonoBehaviour
{
    [Header("시작")]
    [Tooltip("게임 시작 시 보여 줄 지역")]
    public RegionData startRegion;

    [Header("지도 영역 (월드 단위, MapView 위치가 중앙)")]
    public Vector2 mapSize = new Vector2(16f, 9f);

    [Header("구성 요소")]
    public SpriteRenderer background;

    [Tooltip("지도 이미지가 없을 때 쓸 단색 스프라이트 (Square)")]
    public Sprite emptyMapSprite;

    public MapMarker markerPrefab;

    [Header("색 (지도 이미지가 없을 때 배경, 마커 유형별)")]
    public Color emptyMapColor = new Color(0.75f, 0.75f, 0.75f);
    public Color normalColor = new Color(0.45f, 0.55f, 0.7f);
    public Color safeColor = new Color(0.1f, 0.8f, 0.2f);
    public Color dangerColor = new Color(0.9f, 0.1f, 0.1f);
    public Color specialColor = new Color(1f, 0.9f, 0f);
    public Color areaColor = new Color(0.92f, 0.92f, 0.92f);

    public RegionData CurrentRegion { get; private set; }

    // null이면 지역 맵을 보는 중
    public AreaData CurrentArea { get; private set; }

    private readonly List<MapMarker> markers = new List<MapMarker>();

    void Start()
    {
        if (startRegion == null)
        {
            Debug.LogWarning("MapView에 시작 지역(startRegion)이 연결되지 않았습니다.");
            return;
        }

        ShowRegion(startRegion);
    }

    public void ShowRegion(RegionData region)
    {
        CurrentRegion = region;
        CurrentArea = null;

        ClearMarkers();
        SetBackground(region != null ? region.mapImage : null);

        if (region == null)
        {
            return;
        }

        foreach (AreaEntry entry in region.areas)
        {
            if (entry == null || entry.area == null)
            {
                Debug.LogWarning(region.name + ": 비어 있는 구역 항목이 있습니다.");
                continue;
            }

            CreateMarker(entry.mapPosition, null, entry.area, areaColor);
        }
    }

    public void ShowArea(AreaData area)
    {
        CurrentArea = area;

        ClearMarkers();
        SetBackground(area != null ? area.mapImage : null);

        if (area == null)
        {
            return;
        }

        foreach (PlaceEntry entry in area.places)
        {
            if (entry == null || entry.place == null)
            {
                Debug.LogWarning(area.name + ": 비어 있는 장소 항목이 있습니다.");
                continue;
            }

            CreateMarker(entry.mapPosition, entry.place, null, ColorFor(entry.place.placeType));
        }
    }

    public Color ColorFor(PlaceData.PlaceType type)
    {
        switch (type)
        {
            case PlaceData.PlaceType.Safe: return safeColor;
            case PlaceData.PlaceType.Danger: return dangerColor;
            case PlaceData.PlaceType.Special: return specialColor;
            default: return normalColor;
        }
    }

    // 비율 좌표(0~1) → 월드 좌표
    public Vector3 ToWorld(Vector2 normalized)
    {
        return transform.position + new Vector3(
            (normalized.x - 0.5f) * mapSize.x,
            (normalized.y - 0.5f) * mapSize.y,
            0f
        );
    }

    private void CreateMarker(Vector2 position, PlaceData place, AreaData area, Color color)
    {
        if (markerPrefab == null)
        {
            Debug.LogWarning("MapView에 markerPrefab이 연결되지 않았습니다.");
            return;
        }

        MapMarker marker = Instantiate(markerPrefab, ToWorld(position), Quaternion.identity, transform);
        marker.Setup(place, area, color);
        markers.Add(marker);
    }

    private void ClearMarkers()
    {
        foreach (MapMarker marker in markers)
        {
            if (marker != null)
            {
                // 같은 프레임에 새 마커를 클릭 판정할 수 있도록 바로 끈 뒤 제거
                marker.gameObject.SetActive(false);
                Destroy(marker.gameObject);
            }
        }

        markers.Clear();
    }

    private void SetBackground(Sprite mapImage)
    {
        if (background == null)
        {
            return;
        }

        Sprite sprite = mapImage != null ? mapImage : emptyMapSprite;
        background.sprite = sprite;
        background.color = mapImage != null ? Color.white : emptyMapColor;
        background.transform.position = transform.position + new Vector3(0f, 0f, 1f);

        // 스프라이트 크기와 상관없이 지도 영역(mapSize)에 맞춘다
        if (sprite != null)
        {
            Vector2 spriteSize = sprite.bounds.size;
            background.transform.localScale = new Vector3(
                mapSize.x / spriteSize.x,
                mapSize.y / spriteSize.y,
                1f
            );
        }
    }
}

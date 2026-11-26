using System.Collections.Generic;
using UnityEngine;

// 지역 1개의 데이터 (에셋 1개 = 지역 1개). 지역 맵에 표시할 구역과 위치를 가진다.
// 첫 출시는 지역 1개. 게임은 MapView의 시작 지역 맵에서 시작한다.
[CreateAssetMenu(fileName = "Region_New", menuName = "요원/지역 데이터 (RegionData)")]
public class RegionData : ScriptableObject
{
    [Header("식별")]
    [Tooltip("지역 고유 ID (영문 소문자, ':' 금지). 출시 후 변경 금지")]
    public string regionId = "";

    [Header("표시")]
    public string regionName = "[지역 이름 미정]";

    [TextArea(2, 5)]
    public string description = "[지역 설명 미정]";

    [Tooltip("지역 맵 이미지. 비어 있으면 회색 배경")]
    public Sprite mapImage;

    [Header("구역 (위치는 지도 안의 비율 좌표 0~1, (0.5, 0.5) = 지도 중앙)")]
    public List<AreaEntry> areas = new List<AreaEntry>();

    void OnValidate()
    {
        if (!StateKeys.IsValidId(regionId))
        {
            Debug.LogWarning(name + ": regionId가 비었거나 ':'가 들어 있습니다.", this);
        }
    }
}

// 지역 맵 위 구역 1개
[System.Serializable]
public class AreaEntry
{
    public AreaData area;

    [Tooltip("지도 안의 비율 좌표 (0~1)")]
    public Vector2 mapPosition = new Vector2(0.5f, 0.5f);
}

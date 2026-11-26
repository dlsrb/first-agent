using System.Collections.Generic;
using UnityEngine;

// 구역 1개의 데이터 (에셋 1개 = 구역 1개). 구역 맵에 표시할 장소와 위치를 가진다.
// 새 장소는 PlaceData 에셋을 만들고 places 목록에 한 줄 추가하면 된다.
[CreateAssetMenu(fileName = "Area_New", menuName = "요원/구역 데이터 (AreaData)")]
public class AreaData : ScriptableObject
{
    // 구역 종류. B단계 서식지별 괴이 조우의 기준
    public enum AreaType
    {
        Urban,
        Mountain,
        River
    }

    [Header("식별")]
    [Tooltip("구역 고유 ID (영문 소문자, ':' 금지). 출시 후 변경 금지")]
    public string areaId = "";

    [Header("표시")]
    public string areaName = "[구역 이름 미정]";

    public AreaType areaType = AreaType.Urban;

    [TextArea(2, 5)]
    public string description = "[구역 설명 미정]";

    [Tooltip("구역 맵 이미지. 비어 있으면 회색 배경")]
    public Sprite mapImage;

    [Header("장소 (위치는 지도 안의 비율 좌표 0~1, (0.5, 0.5) = 지도 중앙)")]
    public List<PlaceEntry> places = new List<PlaceEntry>();

    // [B-1] 일반 전투 장소(BattleMode.Random)에서 나오는 괴이 목록
    [Header("조우 목록 (일반 전투, 가중치 랜덤)")]
    public List<EncounterEntry> encounters = new List<EncounterEntry>();

    void OnValidate()
    {
        if (!StateKeys.IsValidId(areaId))
        {
            Debug.LogWarning(name + ": areaId가 비었거나 ':'가 들어 있습니다.", this);
        }
    }
}

// 조우 목록 1줄
[System.Serializable]
public class EncounterEntry
{
    public MonsterData monster;

    [Tooltip("가중치 (클수록 자주)")]
    public int weight = 1;
}

// 구역 맵 위 장소 1개
[System.Serializable]
public class PlaceEntry
{
    public PlaceData place;

    [Tooltip("지도 안의 비율 좌표 (0~1)")]
    public Vector2 mapPosition = new Vector2(0.5f, 0.5f);
}

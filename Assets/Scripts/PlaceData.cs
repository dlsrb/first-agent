using System.Collections.Generic;
using UnityEngine;

// 장소 1곳의 데이터 (에셋 1개 = 장소 1곳). 읽기 전용 데이터만 두고, 진행 상태는 GameStateManager에 둔다.
// 텍스트·이미지는 비워 두고 나중에 채운다. 새 장소는 이 에셋을 추가해서 만든다.
[CreateAssetMenu(fileName = "Place_New", menuName = "요원/장소 데이터 (PlaceData)")]
public class PlaceData : ScriptableObject
{
    public enum PlaceType
    {
        Normal,
        Safe,
        Danger,
        Special
    }

    [Header("식별")]
    [Tooltip("진행 상태 저장용 고유 ID (영문 소문자, 장소마다 달라야 함, ':' 금지). 출시 후에는 바꾸지 말 것 - 세이브가 깨짐")]
    public string locationId = "";

    [Header("표시")]
    public string placeName = "[장소 이름 미정]";

    public PlaceType placeType = PlaceType.Normal;

    [TextArea(2, 5)]
    public string description = "[장소 설명 미정]";

    [Tooltip("장소 내부 배경 이미지. 비어 있으면 회색 배경")]
    public Sprite interiorBackground;

    [Header("장소 내부 상호작용 (순서 = 버튼 순서)")]
    public List<PlaceInteraction> interactions = new List<PlaceInteraction>();

    // Inspector에서 값을 바꿀 때 ID 실수를 바로 알려 준다
    void OnValidate()
    {
        if (!StateKeys.IsValidId(locationId))
        {
            Debug.LogWarning(name + ": locationId가 비었거나 ':'가 들어 있습니다.", this);
        }

        var seen = new HashSet<string>();

        foreach (var interaction in interactions)
        {
            if (interaction == null)
            {
                continue;
            }

            if (!StateKeys.IsValidId(interaction.interactionId))
            {
                Debug.LogWarning(name + ": 상호작용 '" + interaction.label + "'의 interactionId가 비었거나 ':'가 들어 있습니다.", this);
            }
            else if (!seen.Add(interaction.interactionId))
            {
                Debug.LogWarning(name + ": interactionId '" + interaction.interactionId + "'가 중복됩니다.", this);
            }
        }
    }
}

// 상호작용 종류. D단계에서 Shop 등을 추가한다.
public enum InteractionType
{
    Investigate,
    Talk
}

// 장소 내부 버튼 1개
[System.Serializable]
public class PlaceInteraction
{
    [Tooltip("장소 안에서 고유한 ID (영문 소문자, ':' 금지). 대화 선택 기록 키에 쓰이므로 출시 후 변경 금지")]
    public string interactionId = "";

    [Tooltip("버튼에 표시할 글자")]
    public string label = "[버튼 미정]";

    public InteractionType type = InteractionType.Investigate;

    [Tooltip("Investigate: 결과 텍스트에 표시할 문구")]
    [TextArea(2, 5)]
    public string resultText = "";

    [Tooltip("Talk: 대화 내용")]
    public TalkData talk = new TalkData();
}

// 대화 1개 (NPC + 첫 대사 + 선택지 + 재방문 대사)
[System.Serializable]
public class TalkData
{
    public NpcData npc;

    [TextArea(2, 5)]
    public string firstLine = "[대사 미정]";

    [Tooltip("현재 UI는 최대 2개까지 표시 (첫 번째 = A, 두 번째 = B)")]
    public List<TalkChoice> choices = new List<TalkChoice>();

    [Tooltip("선택지를 고른 뒤 다시 말을 걸면 나오는 대사")]
    [TextArea(2, 5)]
    public string repeatLine = "[재방문 대사 미정]";
}

[System.Serializable]
public class TalkChoice
{
    public string text = "[선택지 미정]";

    [TextArea(2, 5)]
    public string result = "[결과 미정]";
}

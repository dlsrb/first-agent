using UnityEngine;

// 인물 1명의 데이터 (에셋 1개 = 인물 1명). 여러 장소에서 같은 NPC를 재사용할 수 있다.
[CreateAssetMenu(fileName = "Npc_New", menuName = "요원/NPC 데이터 (NpcData)")]
public class NpcData : ScriptableObject
{
    [Tooltip("NPC 고유 ID (영문 소문자)")]
    public string npcId = "";

    public string npcName = "[이름 미정]";

    [Tooltip("대화창 캐릭터 이미지. 비어 있으면 회색 자리")]
    public Sprite portrait;
}

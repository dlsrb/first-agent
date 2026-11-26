using System.Collections.Generic;

// 게임 진행 상태 묶음 (데이터와 분리된 "플레이 중 변하는 값")
// - 씬에 붙이지 않는 일반 클래스. GameStateManager가 하나를 들고 있다.
// - JsonUtility로 그대로 저장/불러오기 가능한 형태만 사용 (List, string, int, bool)
//   → E단계 세이브/로드에서 이 객체 하나만 저장하면 된다.
[System.Serializable]
public class GameState
{
    // 이미 일어난 일을 문자열 키로 기록. 키는 StateKeys에서만 만든다. 예: "talk:normal:clerk"
    public List<string> flags = new List<string>();

    // [B-1] 주인공 수치 (체력·기력·스탯·주사위·아이템·복귀 장소). initialized가 false면 새 게임으로 채운다
    public PlayerState player = new PlayerState();
}

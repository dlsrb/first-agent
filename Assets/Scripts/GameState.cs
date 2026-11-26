using System.Collections.Generic;

// 게임 진행 상태 묶음 (데이터와 분리된 "플레이 중 변하는 값")
// - 씬에 붙이지 않는 일반 클래스. GameStateManager가 하나를 들고 있다.
// - JsonUtility로 그대로 저장/불러오기 가능한 형태만 사용 (List, string, int, bool)
//   → E단계 세이브/로드에서 이 객체 하나만 저장하면 된다.
[System.Serializable]
public class GameState
{
    // 이미 일어난 일을 문자열 키로 기록. 예: "clerkChoice:normal"
    public List<string> flags = new List<string>();
}

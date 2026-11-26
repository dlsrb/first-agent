using UnityEngine;

// 게임 진행 상태를 한곳에서 관리 (씬의 GameState 오브젝트에 부착)
// 다른 스크립트는 GameStateManager.Instance 로 접근하므로 Inspector 연결이 필요 없다.
public class GameStateManager : MonoBehaviour
{
    private static GameStateManager instance;

    public static GameStateManager Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindAnyObjectByType<GameStateManager>();

                if (instance == null)
                {
                    Debug.LogWarning("씬에 GameStateManager가 없어 임시로 생성합니다. GameState 오브젝트를 확인하세요.");
                    instance = new GameObject("GameState").AddComponent<GameStateManager>();
                }
            }

            return instance;
        }
    }

    [Tooltip("Play 중 현재 진행 상태 확인용. Play를 다시 시작하면 비워진다 (저장은 E단계)")]
    [SerializeField]
    private GameState state = new GameState();

    public GameState State => state;

    void Awake()
    {
        if (instance != null && instance != this)
        {
            Debug.LogWarning("GameStateManager가 둘 이상 있습니다. 나중 것을 제거합니다.");
            Destroy(this);
            return;
        }

        instance = this;
    }

    // ---- 플래그 (키는 반드시 StateKeys로 만든다) ----

    public bool HasFlag(string key)
    {
        return state.flags.Contains(key);
    }

    public void SetFlag(string key)
    {
        if (!state.flags.Contains(key))
        {
            state.flags.Add(key);
        }
    }

    // ---- E단계 세이브/로드용 ----

    public string ToJson()
    {
        return JsonUtility.ToJson(state);
    }

    public void LoadFromJson(string json)
    {
        state = JsonUtility.FromJson<GameState>(json) ?? new GameState();
    }
}

using System.Collections;
using UnityEngine;

// [C-6] 게임 시작 흐름 (성장경제규칙 9번, 설계안 5번). 씬의 StartFlow 오브젝트에 부착.
// 새 게임 → 시작 만화 → 재능 선택("나는 어떤 재능이 있을까?") → 지역 맵(도심만 열림) → 도심(병원만 열림) → 병원에 들어가면 모든 장소가 열린다.
// 잠금은 장소마다 조건을 달지 않고 "시작 규칙" 하나로 처리 (EconomySettings.startArea·startHospital, 플래그 StateKeys.IntroDone).
public class StartFlowController : MonoBehaviour
{
    public EconomySettings economy;
    public BattleSettings settings;
    public GameDatabase database;
    public PickController pickController;

    public const string SkipPrefKey = "yowon_skip_intro";
    public const string UnlockNotice = "[병원에 등록했다. 이제 다른 곳에도 갈 수 있다]";

    public bool Running { get; private set; }

    // 시작 규칙에 따라 아직 잠긴 곳인지 (구역 마커는 area, 장소 마커는 place)
    public static bool IsLocked(GameState state, EconomySettings eco, PlaceData place, AreaData area)
    {
        if (eco == null || eco.startHospital == null || eco.startArea == null) return false;
        if (state != null && state.flags.Contains(StateKeys.IntroDone)) return false;
        if (area != null) return area != eco.startArea;
        return place != null && place != eco.startHospital;
    }

    // 시작 병원에 들어가면 호출 (LocationSelector.OpenInterior). 처음이면 true
    public static bool TryUnlock(GameState state, EconomySettings eco, PlaceData place)
    {
        if (eco == null || place == null || place != eco.startHospital) return false;
        if (state.flags.Contains(StateKeys.IntroDone)) return false;
        state.flags.Add(StateKeys.IntroDone);
        return true;
    }

    void Start()
    {
        var gm = GameStateManager.Instance;
        gm.EnsurePlayer(settings, database);

        if (gm.HasFlag(StateKeys.IntroDone)) return;

#if UNITY_EDITOR
        // 개발용: 메뉴 "요원 > (개발) 시작 흐름 건너뛰기"를 켜 두면 바로 모든 장소가 열린 상태로 시작
        if (UnityEditor.EditorPrefs.GetBool(SkipPrefKey, false))
        {
            gm.SetFlag(StateKeys.IntroDone);
            Debug.Log("[시작 흐름 건너뜀 — 개발 설정]");
            return;
        }
#endif

        StartCoroutine(Run());
    }

    IEnumerator Run()
    {
        Running = true;
        yield return null;   // 다른 오브젝트의 Start가 끝난 뒤

        if (economy != null && economy.openingComic != null && ComicViewer.Instance != null)
        {
            bool done = false;
            ComicViewer.Instance.Play(economy.openingComic, () => done = true);
            while (!done) yield return null;
        }

        if (pickController != null) pickController.OpenTalents();
        Running = false;
    }
}

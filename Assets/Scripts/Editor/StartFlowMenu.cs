using UnityEditor;

// [C-6] 개발용: 시작 흐름(시작 만화 → 재능 → 병원만 열린 지도)을 건너뛰고 모든 장소가 열린 상태로 Play
// 켜 두면 Play할 때마다 건너뛴다. 통합 테스트는 시작 흐름부터 확인하므로 끄고 실행할 것.
public static class StartFlowMenu
{
    const string MenuPath = "요원/(개발) 시작 흐름 건너뛰기";

    [MenuItem(MenuPath)]
    static void Toggle()
    {
        bool on = !EditorPrefs.GetBool(StartFlowController.SkipPrefKey, false);
        EditorPrefs.SetBool(StartFlowController.SkipPrefKey, on);
    }

    [MenuItem(MenuPath, true)]
    static bool ToggleValidate()
    {
        Menu.SetChecked(MenuPath, EditorPrefs.GetBool(StartFlowController.SkipPrefKey, false));
        return true;
    }
}

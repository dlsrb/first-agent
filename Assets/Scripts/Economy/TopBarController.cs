using UnityEngine;
using TMPro;

// 지도 공통 상단 바 (Canvas/TopBar, 맵 구상도 "공통 상단"): 오른쪽 위 골드 표시 + 프로필 버튼.
// 전투·만화 화면이 위에 덮는다. 프로필 버튼(홈·스탯 창)은 C-3.
public class TopBarController : MonoBehaviour
{
    public BattleSettings settings;
    public GameDatabase database;
    public TMP_Text goldText;

    int shownGold = int.MinValue;

    void Start()
    {
        // 게임 시작 시 주인공을 만든다 (상점·병원이 전투 전에 열려도 되게)
        GameStateManager.Instance.EnsurePlayer(settings, database);
    }

    void Update()
    {
        var p = GameStateManager.Instance.State.player;
        int gold = p != null ? p.gold : 0;

        if (gold != shownGold && goldText != null)
        {
            shownGold = gold;
            goldText.text = gold + "G";
        }
    }

    // ProfileButton OnClick — C-3에서 홈·스탯 창을 연다
    public void OnProfile()
    {
        Debug.Log("[홈·스탯 창은 C-3]");
    }
}

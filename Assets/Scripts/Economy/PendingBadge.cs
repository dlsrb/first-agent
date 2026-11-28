using UnityEngine;

// [C-4] 프로필 버튼의 빨간 점: 남은 스탯 포인트·재능·특성 선택이 있으면 보인다 (상단 바·전투 화면)
public class PendingBadge : MonoBehaviour
{
    public GameDatabase database;
    public GameObject dot;

    void Update()
    {
        var gm = GameStateManager.Instance;
        bool on = gm != null && database != null && GrowthLogic.HasPending(gm.State.player, database.economy);
        if (dot != null && dot.activeSelf != on) dot.SetActive(on);
    }
}

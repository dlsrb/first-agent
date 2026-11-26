using UnityEngine;
using UnityEngine.UI;
using TMPro;

// 병원 창 (Canvas/HospitalPanel에 부착, EconomyUIBuilder가 만든다). 전체 치료(체력·기력) = 직급별 고정비.
// 복귀 지점 등록은 장소에 들어갈 때 LocationSelector가 한다 (병원 상호작용이 있는 장소).
public class HospitalController : MonoBehaviour
{
    [Header("데이터")]
    public BattleSettings settings;
    public GameDatabase database;
    public EconomySettings economy;

    [Header("화면")]
    public TMP_Text titleText;
    public TMP_Text statusText;
    public Button healButton;
    public TMP_Text healButtonText;
    public TMP_Text messageText;

    public bool IsOpen => gameObject.activeSelf;
    public string Message => messageText != null ? messageText.text : "";

    PlayerState Player => GameStateManager.Instance.EnsurePlayer(settings, database);

    public void Open(PlaceData place)
    {
        if (titleText != null) titleText.text = place != null ? place.placeName : "[병원 미정]";
        if (messageText != null) messageText.text = "[병원 대사 미정]";
        gameObject.SetActive(true);
        Refresh();
    }

    // 전체 치료 (HealButton OnClick)
    public void OnHeal()
    {
        var r = ShopLogic.Heal(Player, economy);
        if (messageText != null) messageText.text = r.message;
        Refresh();
    }

    // 닫기 (CloseButton OnClick)
    public void Close()
    {
        gameObject.SetActive(false);
    }

    void Refresh()
    {
        var p = Player;
        var rank = economy.RankOf(p);
        int cost = economy.HospitalCost(p);

        if (statusText != null)
        {
            statusText.text = "체력 " + p.currentHp + " / " + p.maxHp + "    기력 " + p.currentEnergy + " / " + p.maxEnergy +
                              "\n직급: " + (rank != null ? rank.rankName : "-") + "    소지 골드 " + p.gold + "G";
        }

        if (healButtonText != null) healButtonText.text = "전체 치료 (" + cost + "G)";
    }
}

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// [C-4] 재능·특성 선택 창 (Canvas/PickPanel, EconomyUIBuilder가 만든다). 카드 최대 4장 중 1장.
// 재능: "나는 어떤 재능이 있을까?" (시작 1개, 승급마다 1개). 특성: 무기 숙련도 10레벨마다 2개 중 1개.
public class PickController : MonoBehaviour
{
    public enum Mode { None, Talent, Trait }

    public BattleSettings settings;
    public GameDatabase database;
    public EconomySettings economy;
    public MapView mapView;
    public LocationSelector selector;
    public HomeController home;

    public TMP_Text titleText;
    public List<PickCardView> cards = new List<PickCardView>();

    public bool IsOpen => gameObject.activeSelf;
    public Mode CurrentMode { get; private set; }
    public readonly List<string> OptionIds = new List<string>();

    WeaponType traitType;

    PlayerState Player => GameStateManager.Instance.EnsurePlayer(settings, database);

    public void OpenTalents()
    {
        var options = GrowthLogic.TalentOptions(Player, economy);
        if (options.Count == 0) return;

        CurrentMode = Mode.Talent;
        titleText.text = Player.talentIds.Count == 0 ? "나는 어떤 재능이 있을까?" : "승급! 새 재능을 하나 고른다";
        OptionIds.Clear();
        var names = new List<string>();
        var descs = new List<string>();

        foreach (var t in options)
        {
            OptionIds.Add(t.talentId);
            names.Add(t.talentName);
            descs.Add(t.description);
        }

        Show(names, descs);
    }

    public void OpenTraits(WeaponType type)
    {
        var tier = GrowthLogic.NextTraitTier(Player, economy, type);
        if (tier == null) return;

        CurrentMode = Mode.Trait;
        traitType = type;
        var rule = economy.MasteryFor(type);
        titleText.text = (rule != null ? rule.displayName : type.ToString()) + " 숙련도 " + tier.level + " — 특성을 하나 고른다";
        OptionIds.Clear();
        var names = new List<string>();
        var descs = new List<string>();

        foreach (var t in new[] { tier.optionA, tier.optionB })
        {
            if (t == null) continue;
            OptionIds.Add(t.traitId);
            names.Add(t.traitName);
            descs.Add(t.description);
        }

        Show(names, descs);
    }

    void Show(List<string> names, List<string> descs)
    {
        for (int i = 0; i < cards.Count; i++)
        {
            bool on = i < names.Count;
            cards[i].root.SetActive(on);
            if (!on) continue;
            cards[i].nameText.text = names[i];
            cards[i].descText.text = descs[i];
        }

        gameObject.SetActive(true);
    }

    public void OnCard0() { Pick(0); }
    public void OnCard1() { Pick(1); }
    public void OnCard2() { Pick(2); }
    public void OnCard3() { Pick(3); }

    // 나중에 고르기 (프로필 빨간 점으로 남음)
    public void Close()
    {
        CurrentMode = Mode.None;
        gameObject.SetActive(false);
    }

    void Pick(int i)
    {
        if (i < 0 || i >= OptionIds.Count) return;

        var p = Player;
        string id = OptionIds[i];
        bool ok = CurrentMode == Mode.Talent ? GrowthLogic.ChooseTalent(p, economy, id) : GrowthLogic.ChooseTrait(p, economy, traitType, id);
        if (!ok) return;

        PlayerStats.Recalculate(p, settings, database);
        string name = CurrentMode == Mode.Talent ? economy.FindTalent(id).talentName : economy.FindTrait(id).traitName;
        Close();

        // 재능이 매력을 올렸으면 숨겨진 것이 드러날 수 있다
        bool revealed = mapView != null && RevealScanner.Scan(GameStateManager.Instance.State, mapView.CurrentRegion ?? mapView.startRegion) > 0;
        NoticeToast.Show(revealed ? RevealScanner.Notice : "[" + name + "] 획득");
        if (revealed && selector != null) selector.RefreshMap();
        if (home != null && home.IsOpen) home.Refresh();
    }
}

[System.Serializable]
public class PickCardView
{
    public GameObject root;
    public TMP_Text nameText;
    public TMP_Text descText;
}

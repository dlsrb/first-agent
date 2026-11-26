using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// 전투 화면 (Canvas의 BattlePanel에 부착). 규칙 계산은 BattleEngine, 여기는 화면과 입력만.
// [B-2] 공격(주사위)·방어(연속 터치)·도망·결과. 특수 행동 창·스킬은 B-3, 만화·보스 연출은 B-4.
public class BattleController : MonoBehaviour
{
    [Header("데이터")]
    public BattleSettings settings;
    public GameDatabase database;

    [Header("괴이")]
    public TMP_Text monsterNameText;
    public TMP_Text monsterStatsText;
    public Image monsterImage;
    public Image monsterHpFill;
    public TMP_Text monsterHpText;
    public TMP_Text intentText;

    [Header("주인공")]
    public Image playerHpFill;
    public TMP_Text playerHpText;
    public Image playerEnergyFill;
    public TMP_Text playerEnergyText;
    public TMP_Text effectsText;
    public TMP_Text messageText;

    [Header("행동 버튼")]
    public GameObject actionButtons;
    public Button attackButton;
    public Button defendButton;
    public Button specialButton;
    public Button fleeButton;

    [Header("공격 기믹 영역 (주사위)")]
    public GameObject attackArea;
    public Transform diceRoot;
    public GameObject diceTemplate;
    public TMP_Text comboText;
    public TMP_Text previewText;
    public Button rollButton;
    public Button confirmAttackButton;
    public Button attackBackButton;

    [Header("방어 창 (연속 터치)")]
    public GameObject defensePopup;
    public TMP_Text defenseText;
    public Image defenseTimerFill;

    [Header("도망 창")]
    public GameObject fleePopup;
    public TMP_Text fleeText;
    public Button fleeConfirmButton;

    [Header("결과 창")]
    public GameObject resultPopup;
    public TMP_Text resultTitleText;
    public TMP_Text resultDetailText;

    // 전투가 끝나고 결과 창 확인을 누르면 호출 (LocationSelector가 구독)
    public event Action<BattleOutcome, PlaceData> BattleFinished;

    public bool IsActive => gameObject.activeSelf;
    public BattleEngine Engine { get; private set; }
    public PlaceData CurrentPlace { get; private set; }

    readonly List<TMP_Text> diceTexts = new List<TMP_Text>();
    int logCursor;
    bool defending;
    float touchBonus;
    Sprite defaultMonsterSprite;
    Color defaultMonsterColor;
    bool defaultsSaved;

    // ---------- 시작 ----------

    // 장소에 들어갈 때 호출. 싸울 괴이를 정하지 못하면 false
    public bool StartBattle(PlaceData place, AreaData area)
    {
        if (settings == null)
        {
            Debug.LogWarning("BattleController에 BattleSettings가 연결되지 않았습니다.");
            return false;
        }

        GameStateManager.Instance.EnsurePlayer(settings);

        var rng = new System.Random(Environment.TickCount);
        MonsterData monster = PickMonster(place, area, rng);

        if (monster == null)
        {
            Debug.LogWarning(place.name + ": 싸울 괴이가 없습니다 (조우 목록 또는 고정 괴이 확인).");
            return false;
        }

        SaveDefaults();

        CurrentPlace = place;
        var player = GameStateManager.Instance.State.player;
        bool knows = GameStateManager.Instance.HasFlag(StateKeys.MonsterInfo(monster.monsterId));

        Engine = new BattleEngine(settings, database, player, monster, knows, place.battle.allowFlee, rng);
        logCursor = 0;

        gameObject.SetActive(true);
        ShowOnly(null);

        if (monsterImage != null)
        {
            monsterImage.sprite = monster.image != null ? monster.image : defaultMonsterSprite;
            monsterImage.color = monster.image != null ? Color.white : defaultMonsterColor;
        }

        Engine.Start();
        Refresh();
        ShowNewMessages(knows ? "" : "괴이가 나타났다!");

        if (Engine.Outcome != BattleOutcome.None)
        {
            ShowResult();
        }

        return true;
    }

    MonsterData PickMonster(PlaceData place, AreaData area, System.Random rng)
    {
        if (place.battle.mode == BattleMode.Story)
        {
            return place.battle.fixedMonster;
        }

        if (area == null || area.encounters.Count == 0)
        {
            return null;
        }

        int total = 0;

        foreach (var e in area.encounters)
        {
            if (e.monster != null) total += Mathf.Max(1, e.weight);
        }

        if (total == 0)
        {
            return null;
        }

        int pick = rng.Next(total);

        foreach (var e in area.encounters)
        {
            if (e.monster == null) continue;
            pick -= Mathf.Max(1, e.weight);
            if (pick < 0) return e.monster;
        }

        return null;
    }

    // ---------- 행동 버튼 (OnClick) ----------

    public void OnAttackButton()
    {
        if (!CanAct()) return;

        BuildDice();
        SetDiceFaces(null);
        comboText.text = "";
        previewText.text = "주사위를 굴리세요";
        rollButton.gameObject.SetActive(true);
        confirmAttackButton.gameObject.SetActive(false);
        attackBackButton.gameObject.SetActive(true);
        ShowOnly(attackArea);
    }

    public void OnRollButton()
    {
        if (Engine == null || Engine.Outcome != BattleOutcome.None) return;
        StartCoroutine(RollRoutine());
    }

    IEnumerator RollRoutine()
    {
        rollButton.gameObject.SetActive(false);
        attackBackButton.gameObject.SetActive(false);

        // 굴리는 연출: 잠깐 숫자가 바뀐다
        float end = Time.unscaledTime + 0.5f;

        while (Time.unscaledTime < end)
        {
            foreach (var t in diceTexts)
            {
                t.text = UnityEngine.Random.Range(1, Engine.player.diceFaces + 1).ToString();
            }

            yield return new WaitForSecondsRealtime(0.05f);
        }

        var roll = Engine.RollDice();
        SetDiceFaces(roll);
        comboText.text = roll.combo ? "조합! ×" + roll.comboMultiplier : "";
        previewText.text = "예상 피해 " + Engine.PreviewDamage(roll) + (Engine.MonsterReflecting ? "  (반사 자세 주의!)" : "");
        confirmAttackButton.gameObject.SetActive(true);
    }

    public void OnConfirmAttack()
    {
        if (Engine == null || Engine.PendingRoll == null) return;

        Engine.ConfirmAttack();
        AfterAction();
    }

    public void OnAttackBack()
    {
        if (Engine != null && Engine.PendingRoll != null) return;   // 굴린 뒤에는 취소 불가
        ShowOnly(null);
    }

    public void OnDefendButton()
    {
        if (!CanAct()) return;
        StartCoroutine(DefenseRoutine());
    }

    IEnumerator DefenseRoutine()
    {
        defending = true;
        touchBonus = 0f;
        ShowOnly(defensePopup);

        float duration = Mathf.Max(0.1f, settings.touchWindowSeconds);
        float start = Time.unscaledTime;

        while (Time.unscaledTime - start < duration)
        {
            defenseText.text = "방어 확률 " + Pct(Engine.DefenseChance(touchBonus)) + "\n(연속 터치로 확률을 높이세요!)";
            defenseTimerFill.fillAmount = 1f - (Time.unscaledTime - start) / duration;
            yield return null;
        }

        defending = false;
        Engine.Defend(touchBonus);
        AfterAction();
    }

    // 방어 창이 떠 있는 동안 화면 어디든 클릭·터치 (DefensePopup의 전체 화면 버튼 OnClick)
    public void OnDefenseTouch()
    {
        if (!defending) return;
        touchBonus = Mathf.Min(Engine.TouchBonusMax, touchBonus + settings.touchIncrement);
    }

    public float CurrentTouchBonus => touchBonus;

    public void OnFleeButton()
    {
        if (!CanAct()) return;

        fleeText.text = Engine.CanFlee
            ? "도망칠 수 있는 확률 " + Pct(Engine.FleeChance) + " (다른 기믹 없음)\n실패 시 받는 피해 " + settings.fleeFailDamageMultiplier + "배"
            : "이 전투에서는 도망칠 수 없다.";
        fleeConfirmButton.interactable = Engine.CanFlee;
        ShowOnly(fleePopup);
    }

    public void OnFleeConfirm()
    {
        if (Engine == null || !Engine.CanFlee) return;
        Engine.Flee();
        AfterAction();
    }

    public void OnFleeCancel()
    {
        ShowOnly(null);
    }

    public void OnResultOk()
    {
        var outcome = Engine != null ? Engine.Outcome : BattleOutcome.None;
        var place = CurrentPlace;

        gameObject.SetActive(false);
        Engine = null;
        BattleFinished?.Invoke(outcome, place);
    }

    // ---------- 진행 ----------

    bool CanAct()
    {
        return Engine != null && Engine.Outcome == BattleOutcome.None && !defending;
    }

    void AfterAction()
    {
        Engine.ClearPendingPhase();   // 페이즈 만화는 B-4
        Refresh();
        ShowNewMessages(null);

        if (Engine.Outcome != BattleOutcome.None)
        {
            ShowResult();
        }
        else
        {
            ShowOnly(null);
        }
    }

    void ShowResult()
    {
        var p = Engine.player;
        string detail;

        switch (Engine.Outcome)
        {
            case BattleOutcome.Win:
                resultTitleText.text = "승리!";
                detail = "[보상은 C단계]";

                if (CurrentPlace != null && CurrentPlace.battle.mode == BattleMode.Story)
                {
                    GameStateManager.Instance.SetFlag(StateKeys.BattleCleared(CurrentPlace.locationId));
                }
                break;

            case BattleOutcome.Lose:
                resultTitleText.text = "패배...";
                int loss = Mathf.FloorToInt(p.gold * settings.defeatGoldLossRatio);
                p.gold -= loss;
                p.currentHp = Mathf.Max(1, Mathf.RoundToInt(p.maxHp * settings.reviveHpRatio));
                detail = "마지막으로 들른 병원에서 깨어난다. (골드 -" + loss + ")";
                break;

            default:
                resultTitleText.text = "도망쳤다";
                detail = "";
                break;
        }

        resultDetailText.text = detail + "\n체력 " + p.currentHp + "/" + p.maxHp + "  기력 " + p.currentEnergy + "/" + p.maxEnergy;
        Refresh();
        ShowOnly(resultPopup);
    }

    // 아래쪽 영역은 하나만 보인다 (null이면 행동 버튼)
    void ShowOnly(GameObject panel)
    {
        attackArea.SetActive(panel == attackArea);
        defensePopup.SetActive(panel == defensePopup);
        fleePopup.SetActive(panel == fleePopup);
        resultPopup.SetActive(panel == resultPopup);
        actionButtons.SetActive(panel == null);

        if (panel == null && Engine != null)
        {
            specialButton.interactable = false;   // B-3에서 특수 행동 창
        }
    }

    // ---------- 화면 갱신 ----------

    void Refresh()
    {
        if (Engine == null) return;

        var m = Engine.monster;
        var p = Engine.player;

        monsterNameText.text = m.monsterName;
        monsterStatsText.text = Engine.KnowsMonster
            ? "공격 " + Engine.MonsterAttack + " / 방어 " + Engine.MonsterDefense
            : "공격 ??? / 방어 ???";
        monsterHpFill.fillAmount = Engine.MonsterMaxHp > 0 ? (float)Engine.MonsterHp / Engine.MonsterMaxHp : 0f;
        monsterHpText.text = Engine.MonsterHp + " / " + Engine.MonsterMaxHp;
        intentText.text = Engine.Outcome == BattleOutcome.None ? Engine.IntentText : "";

        playerHpFill.fillAmount = p.maxHp > 0 ? (float)p.currentHp / p.maxHp : 0f;
        playerHpText.text = "체력 " + p.currentHp + " / " + p.maxHp;
        playerEnergyFill.fillAmount = p.maxEnergy > 0 ? (float)p.currentEnergy / p.maxEnergy : 0f;
        playerEnergyText.text = "기력 " + p.currentEnergy + " / " + p.maxEnergy;

        var sb = new System.Text.StringBuilder();

        foreach (var e in Engine.playerEffects)
        {
            sb.Append(e.data.effectName).Append(" (").Append(e.turnsLeft).Append("턴)\n");
        }

        effectsText.text = sb.ToString();
    }

    void ShowNewMessages(string fallback)
    {
        var lines = new List<string>();

        for (int i = logCursor; i < Engine.log.Count; i++)
        {
            string line = Engine.log[i];
            int close = line.IndexOf("] ");
            lines.Add(close >= 0 ? line.Substring(close + 2) : line);
        }

        logCursor = Engine.log.Count;

        if (lines.Count > 3)
        {
            lines.RemoveRange(0, lines.Count - 3);
        }

        messageText.text = lines.Count > 0 ? string.Join("\n", lines) : (fallback ?? "");
    }

    void BuildDice()
    {
        int count = Mathf.Max(1, Engine.player.diceCount);

        if (diceTexts.Count == count) return;

        foreach (Transform child in diceRoot)
        {
            if (child.gameObject != diceTemplate)
            {
                Destroy(child.gameObject);
            }
        }

        diceTexts.Clear();
        diceTemplate.SetActive(false);

        for (int i = 0; i < count; i++)
        {
            var die = Instantiate(diceTemplate, diceRoot);
            die.name = "Die_" + i;
            die.SetActive(true);
            diceTexts.Add(die.GetComponentInChildren<TMP_Text>(true));
        }
    }

    void SetDiceFaces(DiceRoll roll)
    {
        for (int i = 0; i < diceTexts.Count; i++)
        {
            diceTexts[i].text = roll != null && i < roll.faces.Length ? roll.faces[i].ToString() : "?";
        }
    }

    void SaveDefaults()
    {
        if (defaultsSaved || monsterImage == null) return;
        defaultMonsterSprite = monsterImage.sprite;
        defaultMonsterColor = monsterImage.color;
        defaultsSaved = true;
    }

    static string Pct(float v)
    {
        return Mathf.RoundToInt(v * 100f) + "%";
    }
}

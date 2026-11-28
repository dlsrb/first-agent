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
    [Tooltip("[C-1] 보상·드롭")]
    public EconomySettings economy;
    [Tooltip("[C-4] 전투 뒤 특성·승급 재능 선택 창")]
    public PickController pickController;

    [Header("괴이")]
    public TMP_Text monsterNameText;
    public TMP_Text monsterStatsText;
    public Image monsterImage;
    public Image monsterHpFill;
    public TMP_Text monsterHpText;
    public TMP_Text intentText;
    [Tooltip("[B-3] 괴이에게 걸린 효과 (도발·빈틈 등)")]
    public TMP_Text monsterEffectsText;

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
    [Tooltip("[B-3] 굴린 뒤 쓰는 스킬 버튼 (장착 스킬 순서대로, 최대 2개)")]
    public Button skillButton1;
    public Button skillButton2;

    [Header("특수 행동 창 (B-3)")]
    public GameObject specialPopup;
    public Transform specialItemRoot;
    public Button specialItemTemplate;
    public TMP_Text specialPageText;
    public Button specialNextButton;

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
    // [C-1] 마지막 승리 보상 (결과 창·테스트 확인용)
    public BattleReward LastReward { get; private set; }

    readonly List<TMP_Text> diceTexts = new List<TMP_Text>();
    readonly List<Button> diceButtons = new List<Button>();
    readonly List<Button> specialItems = new List<Button>();
    const int SpecialPageSize = 4;   // 2×2 (맵구상도)
    int specialPage;
    SkillData pendingLockSkill;      // 주사위 고정: 고를 주사위를 기다리는 중
    int logCursor;
    bool defending;
    float touchBonus;
    System.Random rng;
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

        if (economy == null)
        {
            Debug.LogWarning("BattleController에 EconomySettings가 연결되지 않았습니다 (보상 없음).");
        }

        GameStateManager.Instance.EnsurePlayer(settings, database);
        LastReward = null;

        var rng = new System.Random(Environment.TickCount);
        this.rng = rng;
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
        bool intellectReveal = false;

        // [C-4] 지능: 전투 시작 시 일정 확률로 괴이 정보 획득
        if (!knows && player.infoChance > 0f && rng.NextDouble() < player.infoChance)
        {
            GameStateManager.Instance.SetFlag(StateKeys.MonsterInfo(monster.monsterId));
            knows = true;
            intellectReveal = true;
        }

        Engine = new BattleEngine(settings, database, player, monster, knows, place.battle.allowFlee, rng);
        logCursor = 0;

        gameObject.SetActive(true);
        ShowOnly(null);

        if (monsterImage != null)
        {
            monsterImage.sprite = monster.image != null ? monster.image : defaultMonsterSprite;
            monsterImage.color = monster.image != null ? Color.white : defaultMonsterColor;
        }

        // [B-4] 스토리 전투: 만화를 먼저 보여 준 뒤 전투 시작 (선공도 만화 뒤)
        var intro = place.battle.mode == BattleMode.Story ? place.battle.introComic : null;
        string opening = intellectReveal ? "지능으로 괴이의 정체를 알아냈다!" : knows ? "" : "괴이가 나타났다!";

        if (intro != null && ComicViewer.Instance != null)
        {
            WaitingForComic = true;
            actionButtons.SetActive(false);
            ComicViewer.Instance.Play(intro, () => { WaitingForComic = false; BeginEngine(opening); });
        }
        else
        {
            BeginEngine(opening);
        }

        return true;
    }

    // 만화가 재생 중인지 (그동안 행동 불가)
    public bool WaitingForComic { get; private set; }

    void BeginEngine(string opening)
    {
        Engine.Start();
        Refresh();
        ShowNewMessages(opening);

        if (Engine.Outcome != BattleOutcome.None)
        {
            ShowResult();
        }
        else
        {
            ShowOnly(null);
        }
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
        SetDiceClickable(false);
        pendingLockSkill = null;
        comboText.text = "";
        previewText.text = Engine.LockedDieValue > 0 ? "주사위를 굴리세요 (첫 주사위 " + Engine.LockedDieValue + " 고정)" : "주사위를 굴리세요";
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

        Engine.RollDice();
        ShowRollResult();
        confirmAttackButton.gameObject.SetActive(true);
    }

    // 굴린 결과·조합·예상 피해·스킬 버튼 갱신
    void ShowRollResult()
    {
        var roll = Engine.PendingRoll;
        SetDiceFaces(roll);
        comboText.text = roll.combo ? "조합! ×" + roll.comboMultiplier : "";
        previewText.text = "예상 피해 " + Engine.PreviewDamage(roll) +
                           (Engine.MonsterReflecting ? "  (반사 자세 주의!)" : "") +
                           (Engine.LockedDieValue > 0 ? "\n다음 공격: " + Engine.LockedDieValue + " 고정" : "");
        RefreshSkillButtons();
        Refresh();
    }

    public void OnConfirmAttack()
    {
        if (Engine == null || Engine.PendingRoll == null) return;

        pendingLockSkill = null;
        Engine.ConfirmAttack();
        AfterAction();
    }

    // ---------- [B-3] 스킬 (굴린 뒤 사용) ----------

    public void OnSkillButton1() { UseSkillSlot(0); }
    public void OnSkillButton2() { UseSkillSlot(1); }

    void UseSkillSlot(int slot)
    {
        if (Engine == null || Engine.PendingRoll == null) return;

        var skills = Engine.EquippedSkills;
        if (slot >= skills.Count) return;

        var skill = skills[slot];

        if (!Engine.CanUseSkill(skill)) return;

        if (skill.kind == SkillKind.Reroll)
        {
            Engine.UseReroll(skill);
            ShowRollResult();
            ShowNewMessages(null);
        }
        else if (skill.kind == SkillKind.LockDie)
        {
            // 고를 주사위를 누르면 고정
            pendingLockSkill = skill;
            previewText.text = "고정할 주사위를 누르세요";
            SetDiceClickable(true);
        }
    }

    void OnDieClicked(int index)
    {
        if (pendingLockSkill == null || Engine == null) return;

        Engine.UseLockDie(pendingLockSkill, index);
        pendingLockSkill = null;
        SetDiceClickable(false);
        ShowRollResult();
        ShowNewMessages(null);
    }

    void RefreshSkillButtons()
    {
        var skills = Engine.EquippedSkills;
        var buttons = new[] { skillButton1, skillButton2 };

        for (int i = 0; i < buttons.Length; i++)
        {
            if (buttons[i] == null) continue;

            bool has = i < skills.Count && skills[i].timing == SkillTiming.AfterRoll && Engine.PendingRoll != null;
            buttons[i].gameObject.SetActive(has);

            if (has)
            {
                buttons[i].interactable = Engine.CanUseSkill(skills[i]) && pendingLockSkill == null;
                buttons[i].GetComponentInChildren<TMP_Text>(true).text = skills[i].skillName + " (기력 " + skills[i].energyCost + ")";
            }
        }
    }

    void SetDiceClickable(bool clickable)
    {
        foreach (var b in diceButtons)
        {
            b.interactable = clickable;
        }

        RefreshSkillButtons();
    }

    // ---------- [B-3] 특수 행동 창 ----------

    public void OnSpecialButton()
    {
        if (!CanAct()) return;

        specialPage = 0;
        BuildSpecialPage();
        ShowOnly(specialPopup);
    }

    public void OnSpecialClose()
    {
        ShowOnly(null);
    }

    public void OnSpecialNextPage()
    {
        int pages = Mathf.Max(1, Mathf.CeilToInt(SpecialEntries().Count / (float)SpecialPageSize));
        specialPage = (specialPage + 1) % pages;
        BuildSpecialPage();
    }

    class SpecialEntry
    {
        public string label;
        public bool enabled;
        public Action onClick;
    }

    // 창에 들어가는 순서: 스킬 칸 2개 → 가만히 있기 → 도발하기 → 귀막기 → 호흡참기 → 입막기 → 음식먹기
    List<SpecialEntry> SpecialEntries()
    {
        var list = new List<SpecialEntry>();

        foreach (var skill in Engine.EquippedSkills)
        {
            bool afterRoll = skill.timing == SkillTiming.AfterRoll;
            list.Add(new SpecialEntry
            {
                label = skill.skillName + " (기력 " + skill.energyCost + ")" + (afterRoll ? "\n<size=70%>공격 중 주사위를 굴린 뒤 사용</size>" : ""),
                enabled = false,   // 첫 출시 임시 스킬은 모두 굴린 뒤 사용 (Turn 스킬이 생기면 여기서 사용)
                onClick = null
            });
        }

        foreach (var action in new[] { SpecialAction.Wait, SpecialAction.Taunt, SpecialAction.CoverEars,
                                       SpecialAction.HoldBreath, SpecialAction.CoverMouth, SpecialAction.EatFood })
        {
            var a = action;
            string label = DisplayNames.Special(a);

            if (a == SpecialAction.EatFood) label += " (" + FoodCount() + "개)";
            if (a == SpecialAction.Taunt) label += Engine.TauntUsedThisTurn ? "\n<size=70%>이번 턴 사용함</size>" : "\n<size=70%>턴을 쓰지 않음</size>";

            list.Add(new SpecialEntry { label = label, enabled = Engine.CanSpecial(a), onClick = () => OnSpecialChosen(a) });
        }

        return list;
    }

    int FoodCount()
    {
        int n = 0;

        if (database == null) return 0;

        foreach (var stack in Engine.player.items)
        {
            var item = database.FindItem(stack.itemId);
            if (item != null && item.type == ItemType.Consumable) n += stack.count;
        }

        return n;
    }

    void BuildSpecialPage()
    {
        foreach (var b in specialItems)
        {
            if (b != null)
            {
                b.gameObject.SetActive(false);   // Destroy는 프레임 끝에 지워지므로 먼저 숨김
                Destroy(b.gameObject);
            }
        }

        specialItems.Clear();
        specialItemTemplate.gameObject.SetActive(false);

        var entries = SpecialEntries();
        int pages = Mathf.Max(1, Mathf.CeilToInt(entries.Count / (float)SpecialPageSize));
        specialPage = Mathf.Clamp(specialPage, 0, pages - 1);

        for (int i = specialPage * SpecialPageSize; i < Mathf.Min(entries.Count, (specialPage + 1) * SpecialPageSize); i++)
        {
            var entry = entries[i];
            var b = Instantiate(specialItemTemplate, specialItemRoot);
            b.name = "Special_" + i;
            b.GetComponentInChildren<TMP_Text>(true).text = entry.label;
            b.interactable = entry.enabled;

            if (entry.onClick != null)
            {
                var click = entry.onClick;
                b.onClick.AddListener(() => click());
            }

            b.gameObject.SetActive(true);
            specialItems.Add(b);
        }

        specialPageText.text = (specialPage + 1) + "/" + pages;
        specialNextButton.gameObject.SetActive(pages > 1);
    }

    void OnSpecialChosen(SpecialAction action)
    {
        if (!CanAct() || !Engine.CanSpecial(action)) return;

        Engine.Special(action);

        if (action == SpecialAction.Taunt)
        {
            // 도발은 턴을 쓰지 않음: 창을 닫고 같은 턴에 다른 행동을 고른다
            Refresh();
            ShowNewMessages(null);
            ShowOnly(null);
            return;
        }

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

        // [C-4] 이번 전투에서 새로 생긴 특성·승급 재능 선택만 결과 창 뒤에 바로 묻는다
        // ("나중에"로 미룬 것은 다시 묻지 않고 프로필 빨간 점으로 남는다)
        if (pickController != null && economy != null)
        {
            var p = GameStateManager.Instance.State.player;
            var traitType = LastMasteryLevels > 0 ? GrowthLogic.PendingTraitType(p, economy) : null;
            if (traitType.HasValue) pickController.OpenTraits(traitType.Value);
            else if (LastPromotions > 0 && GrowthLogic.TalentOptions(p, economy).Count > 0) pickController.OpenTalents();
        }
    }

    // ---------- 진행 ----------

    bool CanAct()
    {
        return Engine != null && Engine.Outcome == BattleOutcome.None && !defending && !WaitingForComic;
    }

    void AfterAction()
    {
        Refresh();
        ShowNewMessages(null);

        // [B-4] 보스 페이즈 전환: 전환 문구 + 전환 만화 → 끝나면 진행
        var phaseComic = Engine.PendingComic;
        var phaseMessage = Engine.PendingPhaseMessage;
        Engine.ClearPendingPhase();

        if (!string.IsNullOrEmpty(phaseMessage))
        {
            messageText.text = phaseMessage;
        }

        if (phaseComic != null && ComicViewer.Instance != null && Engine.Outcome == BattleOutcome.None)
        {
            WaitingForComic = true;
            actionButtons.SetActive(false);
            attackArea.SetActive(false);
            ComicViewer.Instance.Play(phaseComic, () => { WaitingForComic = false; ContinueAfterAction(); });
            return;
        }

        ContinueAfterAction();
    }

    void ContinueAfterAction()
    {
        if (Engine == null) return;

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

                // 보상 종류: 스토리 장소는 첫 클리어/재도전, 나머지는 일반 (C-1)
                var kind = RewardKind.Normal;

                if (CurrentPlace != null && CurrentPlace.battle.mode == BattleMode.Story)
                {
                    bool first = !GameStateManager.Instance.HasFlag(StateKeys.BattleCleared(CurrentPlace.locationId));
                    GameStateManager.Instance.SetFlag(StateKeys.BattleCleared(CurrentPlace.locationId));
                    kind = first ? RewardKind.StoryFirst : RewardKind.StoryRetry;
                }

                LastReward = RewardCalculator.Roll(economy, Engine.monster, kind, rng ?? new System.Random(), 1f + p.dropBonus);
                LastLevelsGained = RewardCalculator.Apply(p, LastReward, economy);
                detail = RewardCalculator.Describe(LastReward) + "\n소지 골드 " + p.gold;

                if (Engine.monster.IsBoss)
                {
                    GameStateManager.Instance.SetFlag(StateKeys.BossDefeated(Engine.monster.monsterId));
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

        string growth = ApplyGrowth(p);
        if (growth != "") detail += "\n" + growth;

        resultDetailText.text = detail + "\n체력 " + p.currentHp + "/" + p.maxHp + "  기력 " + p.currentEnergy + "/" + p.maxEnergy;
        Refresh();
        ShowOnly(resultPopup);
    }

    // [C-4] 전투 뒤 성장: 무기 숙련도(공격 횟수), 승급, 수치 다시 계산. 결과 창 한 줄
    public int LastLevelsGained { get; private set; }
    public int LastMasteryLevels { get; private set; }
    public int LastPromotions { get; private set; }

    string ApplyGrowth(PlayerState p)
    {
        var parts = new List<string>();
        LastMasteryLevels = 0;
        LastPromotions = 0;

        if (Engine.Outcome != BattleOutcome.Win) LastLevelsGained = 0;
        if (LastLevelsGained > 0) parts.Add("레벨 업! Lv" + p.level + " (스탯 포인트 +" + LastLevelsGained * (economy != null ? economy.pointsPerLevel : 1) + ")");

        var weapon = PlayerStats.EquippedItem(p, p.equippedWeaponUid, database, ItemType.Weapon);

        if (weapon != null && economy != null)
        {
            LastMasteryLevels = GrowthLogic.AddMasteryExp(p, economy, weapon.weaponType, Engine.attackCount);
            if (LastMasteryLevels > 0) parts.Add("숙련도 Lv" + p.MasteryOf(weapon.weaponType).level);
        }

        foreach (var r in GrowthLogic.TryPromote(GameStateManager.Instance.State, economy))
        {
            parts.Add("승급! " + r.rankName);
            LastPromotions++;
        }

        PlayerStats.Recalculate(p, settings, database);
        return string.Join("  ·  ", parts);
    }

    // 아래쪽 영역은 하나만 보인다 (null이면 행동 버튼)
    void ShowOnly(GameObject panel)
    {
        attackArea.SetActive(panel == attackArea);
        defensePopup.SetActive(panel == defensePopup);
        fleePopup.SetActive(panel == fleePopup);
        resultPopup.SetActive(panel == resultPopup);

        if (specialPopup != null)
        {
            specialPopup.SetActive(panel == specialPopup);
        }

        actionButtons.SetActive(panel == null);

        if (panel == null && Engine != null)
        {
            specialButton.interactable = Engine.Outcome == BattleOutcome.None;
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

        if (monsterEffectsText != null)
        {
            var ms = new System.Text.StringBuilder();

            foreach (var e in Engine.monsterEffects)
            {
                ms.Append(e.data.effectName).Append(" (").Append(e.turnsLeft).Append("턴)\n");
            }

            if (Engine.MonsterReflecting)
            {
                ms.Append("반사 자세\n");
            }

            monsterEffectsText.text = ms.ToString();
        }
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
        diceButtons.Clear();
        diceTemplate.SetActive(false);

        for (int i = 0; i < count; i++)
        {
            var die = Instantiate(diceTemplate, diceRoot);
            die.name = "Die_" + i;
            die.SetActive(true);
            diceTexts.Add(die.GetComponentInChildren<TMP_Text>(true));

            var button = die.GetComponent<Button>();

            if (button != null)
            {
                int index = i;
                button.onClick.AddListener(() => OnDieClicked(index));
                button.interactable = false;
                diceButtons.Add(button);
            }
        }
    }

    static readonly Color LockedDieColor = new Color(1f, 0.85f, 0.3f);

    void SetDiceFaces(DiceRoll roll)
    {
        for (int i = 0; i < diceTexts.Count; i++)
        {
            diceTexts[i].text = roll != null && i < roll.faces.Length ? roll.faces[i].ToString() : "?";

            // 고정된 주사위(이번 공격에 고정 값으로 나온 것)는 노란 글자
            diceTexts[i].color = roll != null && roll.lockedIndex == i ? LockedDieColor : new Color(0.15f, 0.15f, 0.15f);
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

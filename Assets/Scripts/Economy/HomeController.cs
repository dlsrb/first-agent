using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// 홈·스탯 창 (Canvas/HomePanel, EconomyUIBuilder가 만든다). 성장경제규칙 7번.
// 상단 바·전투 화면의 프로필 버튼으로 연다. 전투 중에는 보기만 (장착·사용 불가).
// 탭: 스탯(레벨 업 포인트 찍기) / 장비 / 인벤토리 / 성장(숙련도·특성·직급·재능, 재능·특성 고르기).
public class HomeController : MonoBehaviour
{
    public enum Tab { Stats, Equipment, Inventory, Growth }

    [Header("데이터")]
    public BattleSettings settings;
    public GameDatabase database;
    public EconomySettings economy;
    [Tooltip("전투 중인지 확인 (보기 전용)")]
    public BattleController battleController;

    [Header("탭")]
    public Button statsTabButton;
    public Button equipmentTabButton;
    public Button inventoryTabButton;
    public Button growthTabButton;

    [Header("글 영역 (스탯·성장 탭)")]
    public GameObject textArea;
    public TMP_Text leftText;
    public TMP_Text rightText;

    [Header("장비 칸 (장비 탭)")]
    public GameObject slotArea;
    public TMP_Text weaponSlotText;
    public Button weaponUnequipButton;
    public TMP_Text armorSlotText;
    public Button armorUnequipButton;

    [Header("목록 (장비·인벤토리 탭)")]
    public GameObject listArea;
    public List<ShopRowView> rows = new List<ShopRowView>();
    public TMP_Text pageText;
    public Button prevPageButton;
    public Button nextPageButton;

    [Header("[C-4] 스탯 찍기 (스탯 탭)")]
    public GameObject statArea;
    public List<StatRowView> statRows = new List<StatRowView>();
    public TMP_Text pointsText;

    [Header("[C-4] 고르기 버튼 (성장 탭)")]
    public GameObject growthButtons;
    public Button talentPickButton;
    public TMP_Text talentPickText;
    public Button traitPickButton;
    public TMP_Text traitPickText;
    public PickController pickController;
    public MapView mapView;
    public LocationSelector selector;

    [Header("아래")]
    public TMP_Text summaryText;
    public TMP_Text messageText;

    public bool IsOpen => gameObject.activeSelf;
    public bool ReadOnly { get; private set; }
    public Tab CurrentTab { get; private set; }
    public int Page { get; private set; }
    public readonly List<ShopController.RowEntry> Visible = new List<ShopController.RowEntry>();
    public string Message => messageText != null ? messageText.text : "";

    readonly List<ShopController.RowEntry> entries = new List<ShopController.RowEntry>();

    PlayerState Player => GameStateManager.Instance.EnsurePlayer(settings, database);

    // ---------- 열기·닫기 (프로필 버튼 OnClick) ----------

    public void Open()
    {
        ReadOnly = battleController != null && battleController.IsActive;
        gameObject.SetActive(true);
        SetMessage(ReadOnly ? "전투 중에는 볼 수만 있다." : "");
        ShowTab(Tab.Stats);
    }

    public void Close()
    {
        gameObject.SetActive(false);
    }

    public void OnStatsTab() { ShowTab(Tab.Stats); }
    public void OnEquipmentTab() { ShowTab(Tab.Equipment); }
    public void OnInventoryTab() { ShowTab(Tab.Inventory); }
    public void OnGrowthTab() { ShowTab(Tab.Growth); }

    public void OnPrevPage() { if (Page > 0) { Page--; Refresh(); } }
    public void OnNextPage() { if ((Page + 1) * rows.Count < entries.Count) { Page++; Refresh(); } }

    public void OnRow0() { ClickRow(0); }
    public void OnRow1() { ClickRow(1); }
    public void OnRow2() { ClickRow(2); }
    public void OnRow3() { ClickRow(3); }

    // [C-4] 스탯 + 버튼 (StatType 순서: 힘·민첩·정신력·지능·매력)
    public void OnStat0() { AddStat(StatType.Strength); }
    public void OnStat1() { AddStat(StatType.Agility); }
    public void OnStat2() { AddStat(StatType.Spirit); }
    public void OnStat3() { AddStat(StatType.Intellect); }
    public void OnStat4() { AddStat(StatType.Charm); }

    public void OnTalentPick() { if (!ReadOnly && pickController != null) pickController.OpenTalents(); }

    public void OnTraitPick()
    {
        var t = GrowthLogic.PendingTraitType(Player, economy);
        if (!ReadOnly && pickController != null && t.HasValue) pickController.OpenTraits(t.Value);
    }

    void AddStat(StatType st)
    {
        if (ReadOnly || !GrowthLogic.AssignPoint(Player, st)) return;

        PlayerStats.Recalculate(Player, settings, database);
        SetMessage(DisplayNames.Stat(st) + " +1");

        // [C-4] 매력 등으로 숨겨진 것이 드러났으면 알림 + 지도 다시 그리기
        if (mapView != null && RevealScanner.Scan(GameStateManager.Instance.State, mapView.CurrentRegion ?? mapView.startRegion) > 0)
        {
            NoticeToast.Show(RevealScanner.Notice);
            SetMessage(RevealScanner.Notice);
            if (selector != null) selector.RefreshMap();
        }

        Refresh();
    }

    public void OnUnequipWeapon() { Unequip(ItemType.Weapon); }
    public void OnUnequipArmor() { Unequip(ItemType.Armor); }

    public void ShowTab(Tab tab)
    {
        CurrentTab = tab;
        Page = 0;
        Refresh();
    }

    void ClickRow(int i)
    {
        if (i < 0 || i >= Visible.Count) return;
        var e = Visible[i];
        if (e.enabled && !ReadOnly) e.onClick?.Invoke();
    }

    // ---------- 행동 ----------

    void Equip(int uid)
    {
        var owned = Player.FindEquipment(uid);
        var item = owned != null ? database.FindItem(owned.itemId) : null;
        bool ok = item != null && PlayerStats.Equip(Player, uid, settings, database);
        SetMessage(ok ? Josa.Eul(ShopLogic.NameWithEnhance(item, owned.enhance)) + " 장착했다." : "[장착할 수 없다]");
        Refresh();
    }

    void Unequip(ItemType slot)
    {
        if (ReadOnly) return;
        var p = Player;
        var item = PlayerStats.EquippedItem(p, slot == ItemType.Weapon ? p.equippedWeaponUid : p.equippedArmorUid, database, slot);
        if (item == null) return;

        PlayerStats.Unequip(p, slot, settings, database);
        SetMessage(Josa.Eul(item.itemName) + " 해제했다.");
        Refresh();
    }

    void Use(ItemData item)
    {
        var r = ShopLogic.UseItem(Player, item);
        SetMessage(r.message);

        // [11/28] 스탯 아이템 (예: 매력 +1) → 다시 계산·히든 알림
        if (r.ok && item.statGains != null && item.statGains.Count > 0)
        {
            PlayerStats.Recalculate(Player, settings, database);
            if (RevealScanner.ScanAndNotify(mapView, selector)) SetMessage(r.message + "  " + RevealScanner.Notice);
        }

        Refresh();
    }

    // ---------- 화면 ----------

    public void Refresh()
    {
        var p = Player;
        PlayerStats.Recalculate(p, settings, database);

        bool list = CurrentTab == Tab.Equipment || CurrentTab == Tab.Inventory;
        if (textArea != null) textArea.SetActive(!list);
        if (statArea != null) statArea.SetActive(CurrentTab == Tab.Stats);
        if (leftText != null) leftText.gameObject.SetActive(CurrentTab != Tab.Stats);
        if (growthButtons != null) growthButtons.SetActive(CurrentTab == Tab.Growth);
        if (listArea != null) listArea.SetActive(list);
        if (slotArea != null) slotArea.SetActive(CurrentTab == Tab.Equipment);

        SetTabLook(statsTabButton, CurrentTab == Tab.Stats);
        SetTabLook(equipmentTabButton, CurrentTab == Tab.Equipment);
        SetTabLook(inventoryTabButton, CurrentTab == Tab.Inventory);
        SetTabLook(growthTabButton, CurrentTab == Tab.Growth);

        var rank = economy != null ? economy.RankOf(p) : null;
        if (summaryText != null)
        {
            summaryText.text = "레벨 " + p.level + " (" + p.exp + "/" + (economy != null ? economy.ExpToNext(p.level) : 0) + ") · " + (rank != null ? rank.rankName : "-") + "    체력 " + p.currentHp + "/" + p.maxHp +
                               "    기력 " + p.currentEnergy + "/" + p.maxEnergy + "    " + p.gold + "G";
        }

        switch (CurrentTab)
        {
            case Tab.Stats: ShowStats(p); break;
            case Tab.Growth: ShowGrowth(p, rank); break;
            case Tab.Equipment: ShowSlots(p); BuildEquipment(p); ShowList(); break;
            case Tab.Inventory: BuildInventory(p); ShowList(); break;
        }
    }

    void ShowStats(PlayerState p)
    {
        // 왼쪽: 스탯 5개 + 찍기 버튼 (값 = 찍은 포인트 + 재능). 오른쪽: 계산된 전투 수치
        if (pointsText != null) pointsText.text = "남은 스탯 포인트  " + p.statPoints;

        for (int i = 0; i < statRows.Count; i++)
        {
            var st = (StatType)i;
            var row = statRows[i];
            row.nameText.text = DisplayNames.Stat(st);
            row.valueText.text = ConditionChecker.StatValue(p, st).ToString();
            row.hintText.text = StatHint(st);
            row.plusButton.interactable = !ReadOnly && p.statPoints > 0;

            // [11/28] 매력은 히든 스탯: 값만 보여 주고, 찍는 버튼·설명 없음
            bool assignable = GrowthLogic.CanAssign(st);
            row.plusButton.gameObject.SetActive(assignable);
            if (!assignable) row.hintText.text = "";
        }

        var skills = new List<string>();
        foreach (var id in p.skillIds)
        {
            var sk = database.FindSkill(id);
            skills.Add(sk != null ? sk.skillName : id);
        }

        rightText.text = "<b>전투 수치</b>\n" +
                         "체력  " + p.currentHp + " / " + p.maxHp + "\n" +
                         "기력  " + p.currentEnergy + " / " + p.maxEnergy + "\n" +
                         "공격  " + p.attack + "\n" +
                         "방어  " + p.defense + "\n" +
                         "치명타  " + Mathf.RoundToInt(p.critChance * 100) + "%\n" +
                         "도망 확률  " + Mathf.RoundToInt(Mathf.Min(settings.fleeChanceMax, settings.fleeChanceBase + p.agility * settings.fleeChancePerAgility) * 100) + "%\n" +
                         "주사위  " + p.diceCount + "개 × " + p.diceFaces + "면\n" +
                         "스킬  " + (skills.Count > 0 ? string.Join(", ", skills) : "없음") + "\n" +
                         "디버프 무시  " + Mathf.RoundToInt(p.debuffResist * 100) + "%   상점 할인  " + Mathf.RoundToInt(p.shopDiscount * 100) + "%";
    }

    static string StatHint(StatType st)
    {
        switch (st)
        {
            case StatType.Strength: return "공격력";
            case StatType.Agility: return "도망·치명타";
            case StatType.Spirit: return "최대 체력·디버프 무시";
            case StatType.Intellect: return "최대 기력·괴이 정보";
            case StatType.Charm: return "할인·숨겨진 것 발견";
            default: return "";
        }
    }

    void ShowGrowth(PlayerState p, RankData rank)
    {
        // 왼쪽: 무기 숙련도·특성
        var sb = new System.Text.StringBuilder("<b>무기 숙련도</b>\n");

        if (economy != null)
        {
            foreach (var rule in economy.masteries)
            {
                if (rule == null) continue;
                var m = p.MasteryOf(rule.weaponType);
                sb.AppendLine(rule.displayName + "  Lv" + m.level + "  (" + m.exp + "/" + rule.expPerLevel + ")");
                var names = new List<string>();
                foreach (var id in m.traitIds) { var t = economy.FindTrait(id); if (t != null) names.Add(t.traitName); }
                sb.AppendLine("특성: " + (names.Count > 0 ? string.Join(", ", names) : "없음"));
            }
        }

        leftText.text = sb.ToString();

        // 오른쪽: 직급·재능
        var talents = new List<string>();
        if (economy != null) foreach (var id in p.talentIds) { var t = economy.FindTalent(id); if (t != null) talents.Add(t.talentName); }
        int next = economy != null ? economy.ranks.IndexOf(rank) + 1 : 0;
        string nextRank = economy != null && next > 0 && next < economy.ranks.Count ? "\n다음: " + economy.ranks[next].rankName + " (레벨 " + economy.ranks[next].minLevel + ")" : "";

        rightText.text = "<b>직급</b>\n" + (rank != null ? rank.rankName : "-") + "   병원비 " + (economy != null ? economy.HospitalCost(p) : 0) + "G" + nextRank +
                         "\n\n<b>재능</b>\n" + (talents.Count > 0 ? string.Join(", ", talents) : "없음");

        int talentPicks = GrowthLogic.TalentOptions(p, economy).Count > 0 ? p.pendingTalentPicks : 0;
        bool trait = GrowthLogic.PendingTraitType(p, economy) != null;
        if (talentPickText != null) talentPickText.text = "재능 고르기" + (talentPicks > 0 ? " (" + talentPicks + ")" : "");
        if (traitPickText != null) traitPickText.text = "특성 고르기" + (trait ? " (1)" : "");
        if (talentPickButton != null) talentPickButton.interactable = !ReadOnly && talentPicks > 0;
        if (traitPickButton != null) traitPickButton.interactable = !ReadOnly && trait;
    }

    void ShowSlots(PlayerState p)
    {
        weaponSlotText.text = "무기: " + SlotLabel(p, p.equippedWeaponUid, ItemType.Weapon);
        armorSlotText.text = "방어구: " + SlotLabel(p, p.equippedArmorUid, ItemType.Armor);
        weaponUnequipButton.interactable = !ReadOnly && p.equippedWeaponUid != 0;
        armorUnequipButton.interactable = !ReadOnly && p.equippedArmorUid != 0;
    }

    string SlotLabel(PlayerState p, int uid, ItemType type)
    {
        var item = PlayerStats.EquippedItem(p, uid, database, type);
        if (item == null) return "없음";
        var owned = p.FindEquipment(uid);
        return ShopLogic.NameWithEnhance(item, owned.enhance) + "  (" + ShopLogic.EffectText(item, owned.enhance) + ")";
    }

    void BuildEquipment(PlayerState p)
    {
        entries.Clear();

        foreach (var eq in p.equipment)
        {
            var item = database.FindItem(eq.itemId);
            if (item == null) continue;
            bool on = p.IsEquipped(eq.uid);
            int uid = eq.uid;

            entries.Add(new ShopController.RowEntry
            {
                item = item, uid = uid, icon = item.icon,
                name = ShopLogic.NameWithEnhance(item, eq.enhance),
                info = ShopLogic.EffectText(item, eq.enhance),
                button = on ? "장착 중" : "장착",
                enabled = !on && !ReadOnly,
                onClick = () => Equip(uid)
            });
        }
    }

    void BuildInventory(PlayerState p)
    {
        entries.Clear();

        foreach (var st in p.items)
        {
            var item = database.FindItem(st.itemId);
            if (item == null) continue;
            bool usable = item.type == ItemType.Consumable;
            var it = item;

            entries.Add(new ShopController.RowEntry
            {
                item = item, icon = item.icon,
                name = item.itemName,
                info = ShopLogic.EffectText(item) + "\n보유 " + st.count + "개",
                button = usable ? "사용" : "재료",
                enabled = usable && !ReadOnly,
                onClick = () => Use(it)
            });
        }
    }

    void ShowList()
    {
        int pages = Mathf.Max(1, Mathf.CeilToInt(entries.Count / (float)Mathf.Max(1, rows.Count)));
        Page = Mathf.Clamp(Page, 0, pages - 1);
        Visible.Clear();

        for (int i = 0; i < rows.Count; i++)
        {
            int idx = Page * rows.Count + i;
            var e = idx < entries.Count ? entries[idx] : null;
            if (e != null) Visible.Add(e);
            rows[i].Show(e);
        }

        if (pageText != null) pageText.text = entries.Count == 0 ? (CurrentTab == Tab.Inventory ? "[가진 아이템이 없다]" : "[가진 장비가 없다]") : (Page + 1) + " / " + pages;
        if (prevPageButton != null) prevPageButton.interactable = Page > 0;
        if (nextPageButton != null) nextPageButton.interactable = Page < pages - 1;
    }

    void SetMessage(string text)
    {
        if (messageText != null) messageText.text = text;
    }

    static void SetTabLook(Button b, bool on)
    {
        if (b == null) return;
        var img = b.targetGraphic as Image;
        if (img != null) img.color = on ? new Color(1f, 0.85f, 0.4f) : Color.white;
    }
}

// [C-4] 스탯 한 줄 (이름 · 값 · 효과 · + 버튼)
[Serializable]
public class StatRowView
{
    public TMP_Text nameText;
    public TMP_Text valueText;
    public TMP_Text hintText;
    public Button plusButton;
}

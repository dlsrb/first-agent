using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// 홈·스탯 창 (Canvas/HomePanel, EconomyUIBuilder가 만든다). 성장경제규칙 7번.
// 상단 바·전투 화면의 프로필 버튼으로 연다. 전투 중에는 보기만 (장착·사용 불가).
// 탭: 스탯 / 장비 / 인벤토리 / 성장(숙련도·직급·재능). 스탯 5개·숙련도·재능은 C-4 전까지 표시 자리만.
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
        Refresh();
    }

    // ---------- 화면 ----------

    public void Refresh()
    {
        var p = Player;
        PlayerStats.Recalculate(p, settings, database);

        bool list = CurrentTab == Tab.Equipment || CurrentTab == Tab.Inventory;
        if (textArea != null) textArea.SetActive(!list);
        if (listArea != null) listArea.SetActive(list);
        if (slotArea != null) slotArea.SetActive(CurrentTab == Tab.Equipment);

        SetTabLook(statsTabButton, CurrentTab == Tab.Stats);
        SetTabLook(equipmentTabButton, CurrentTab == Tab.Equipment);
        SetTabLook(inventoryTabButton, CurrentTab == Tab.Inventory);
        SetTabLook(growthTabButton, CurrentTab == Tab.Growth);

        var rank = economy != null ? economy.RankOf(p) : null;
        if (summaryText != null)
        {
            summaryText.text = "레벨 " + p.level + " · " + (rank != null ? rank.rankName : "-") + "    체력 " + p.currentHp + "/" + p.maxHp +
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
        // 왼쪽: 스탯 5개 (C-4 전까지 표시만). 오른쪽: 계산된 전투 수치
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("<b>스탯</b>   (레벨 업 포인트는 C-4)");
        foreach (StatType st in Enum.GetValues(typeof(StatType)))
        {
            sb.AppendLine(DisplayNames.Stat(st) + "   " + ConditionChecker.StatValue(p, st) + "   " + StatHint(st));
        }
        leftText.text = sb.ToString();

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
                         "스킬  " + (skills.Count > 0 ? string.Join(", ", skills) : "없음");
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
        leftText.text = "<b>무기 숙련도</b>\n주사위   [C-4]\n\n<b>특성</b>\n[C-4]";
        rightText.text = "<b>직급</b>\n" + (rank != null ? rank.rankName : "-") + "\n병원비 " + (economy != null ? economy.HospitalCost(p) : 0) + "G" +
                         "\n\n<b>경험치</b>\n" + p.exp + "  (레벨 업은 C-4)" +
                         "\n\n<b>재능</b>\n[C-4]";
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

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// 상점 창 (Canvas/ShopPanel에 부착, EconomyUIBuilder가 만든다). 규칙은 ShopLogic, 여기는 화면과 입력만.
// 배치: docs/맵구상도.drawio "일반 상점 구매 창" — 왼쪽 점원, 오른쪽 상점 이름·탭·상품 행 4개·나가기.
public class ShopController : MonoBehaviour
{
    public enum Tab { Buy, Sell, Enhance }

    [Header("데이터")]
    public BattleSettings settings;
    public GameDatabase database;
    public EconomySettings economy;

    [Header("상점 정보")]
    public Image keeperImage;
    public TMP_Text keeperNameText;
    public Image shopIcon;
    public TMP_Text shopNameText;

    [Header("탭")]
    public Button buyTabButton;
    public Button sellTabButton;
    public Button enhanceTabButton;

    [Header("상품 행 (4개)")]
    public List<ShopRowView> rows = new List<ShopRowView>();
    public TMP_Text pageText;
    public Button prevPageButton;
    public Button nextPageButton;

    [Header("아래")]
    public TMP_Text goldText;
    public TMP_Text messageText;

    [Header("확인 창")]
    public GameObject confirmPopup;
    public TMP_Text confirmText;
    public Button confirmOkButton;
    public TMP_Text confirmOkText;
    public Button confirmAltButton;
    public TMP_Text confirmAltText;

    public static ShopController Instance { get; private set; }

    public bool IsOpen => gameObject.activeSelf;
    public ShopData CurrentShop { get; private set; }
    public Tab CurrentTab { get; private set; }
    public int Page { get; private set; }
    // 지금 보이는 행 (테스트 확인용)
    public readonly List<RowEntry> Visible = new List<RowEntry>();

    public class RowEntry
    {
        public string name, info, button;
        public bool enabled = true;
        public Sprite icon;
        public Action onClick;
        public ItemData item;
        public int uid;
    }

    readonly List<RowEntry> entries = new List<RowEntry>();
    Action confirmOk, confirmAlt;
    Sprite defaultKeeperSprite;
    Color defaultKeeperColor;
    bool defaultsSaved;

    void Awake()
    {
        Instance = this;
    }

    GameState State => GameStateManager.Instance.State;
    PlayerState Player => GameStateManager.Instance.EnsurePlayer(settings, database);

    // ---------- 열기·닫기 ----------

    public void Open(ShopData shop)
    {
        if (shop == null)
        {
            Debug.LogWarning("상점 데이터가 연결되지 않았습니다.");
            return;
        }

        Instance = this;
        SaveDefaults();
        CurrentShop = shop;

        if (shopNameText != null) shopNameText.text = shop.shopName;
        if (keeperNameText != null) keeperNameText.text = shop.keeperName;

        if (keeperImage != null)
        {
            keeperImage.sprite = shop.keeperImage != null ? shop.keeperImage : defaultKeeperSprite;
            keeperImage.color = shop.keeperImage != null ? Color.white : defaultKeeperColor;
        }

        if (shopIcon != null)
        {
            shopIcon.enabled = shop.icon != null;
            shopIcon.sprite = shop.icon;
        }

        if (sellTabButton != null) sellTabButton.gameObject.SetActive(shop.canSell);
        if (enhanceTabButton != null) enhanceTabButton.gameObject.SetActive(shop.canEnhance);

        gameObject.SetActive(true);
        SetMessage("");
        ShowTab(Tab.Buy);
    }

    // 나가기 (ExitButton OnClick)
    public void Close()
    {
        HideConfirm();
        gameObject.SetActive(false);
        CurrentShop = null;
    }

    // ---------- 탭·페이지 (OnClick) ----------

    public void OnBuyTab() { ShowTab(Tab.Buy); }
    public void OnSellTab() { ShowTab(Tab.Sell); }
    public void OnEnhanceTab() { ShowTab(Tab.Enhance); }

    public void OnPrevPage()
    {
        if (Page > 0) { Page--; Refresh(); }
    }

    public void OnNextPage()
    {
        if ((Page + 1) * rows.Count < entries.Count) { Page++; Refresh(); }
    }

    public void OnRow0() { ClickRow(0); }
    public void OnRow1() { ClickRow(1); }
    public void OnRow2() { ClickRow(2); }
    public void OnRow3() { ClickRow(3); }

    public void OnConfirmOk() { var a = confirmOk; HideConfirm(); a?.Invoke(); }
    public void OnConfirmAlt() { var a = confirmAlt; HideConfirm(); a?.Invoke(); }
    public void OnConfirmCancel() { HideConfirm(); }

    public void ShowTab(Tab tab)
    {
        CurrentTab = tab;
        Page = 0;
        HideConfirm();
        Refresh();
    }

    void ClickRow(int index)
    {
        if (index < 0 || index >= Visible.Count) return;
        var e = Visible[index];
        if (e.enabled) e.onClick?.Invoke();
    }

    // ---------- 목록 ----------

    public void Refresh()
    {
        BuildEntries();

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

        if (pageText != null) pageText.text = (Page + 1) + " / " + pages;
        if (prevPageButton != null) prevPageButton.interactable = Page > 0;
        if (nextPageButton != null) nextPageButton.interactable = Page < pages - 1;
        if (goldText != null) goldText.text = "소지 골드 " + Player.gold + "G";

        SetTabLook(buyTabButton, CurrentTab == Tab.Buy);
        SetTabLook(sellTabButton, CurrentTab == Tab.Sell);
        SetTabLook(enhanceTabButton, CurrentTab == Tab.Enhance);
    }

    void BuildEntries()
    {
        entries.Clear();
        if (CurrentShop == null) return;

        var p = Player;

        switch (CurrentTab)
        {
            case Tab.Buy:
                foreach (var entry in CurrentShop.entries)
                {
                    if (entry == null || entry.item == null) continue;
                    bool ok = ConditionChecker.Check(State, entry.condition);
                    if (ConditionChecker.Hidden(State, entry.condition)) continue;

                    var item = entry.item;
                    int price = ShopLogic.BuyPrice(entry, p);
                    int owned = item.IsEquipment ? p.equipment.FindAll(x => x.itemId == item.itemId).Count : p.CountOf(item.itemId);
                    var en = entry;

                    entries.Add(new RowEntry
                    {
                        item = item,
                        icon = item.icon,
                        name = item.itemName,
                        info = ShopLogic.EffectText(item) + "\n보유 " + owned + "개",
                        // [C-4] 매력 할인: 원가 취소선 + 할인가
                        button = !ok ? ConditionChecker.LockedText(entry.condition)
                               : price < entry.BasePrice ? "<s>" + entry.BasePrice + "G</s> " + price + "G 구매" : price + "G 구매",
                        enabled = ok,
                        onClick = () => OnBuyEntry(en, price)
                    });
                }
                break;

            case Tab.Sell:
                foreach (var eq in p.equipment)
                {
                    var item = database.FindItem(eq.itemId);
                    if (item == null) continue;
                    bool equipped = p.IsEquipped(eq.uid);
                    int uid = eq.uid;
                    int price = economy.SellPrice(item);

                    entries.Add(new RowEntry
                    {
                        item = item, uid = uid, icon = item.icon,
                        name = ShopLogic.NameWithEnhance(item, eq.enhance),
                        info = ShopLogic.EffectText(item, eq.enhance) + (equipped ? "\n장착 중" : ""),
                        button = equipped ? "장착 중" : price + "G 판매",
                        enabled = !equipped,
                        onClick = () => OnSellEquipment(uid, price)
                    });
                }

                foreach (var st in p.items)
                {
                    var item = database.FindItem(st.itemId);
                    if (item == null) continue;
                    int price = economy.SellPrice(item);
                    string id = st.itemId;

                    entries.Add(new RowEntry
                    {
                        item = item, icon = item.icon,
                        name = item.itemName,
                        info = ShopLogic.EffectText(item) + "\n보유 " + st.count + "개",
                        button = price + "G 판매",
                        enabled = price > 0,
                        onClick = () => Act(ShopLogic.SellItem(Player, id, economy, database))
                    });
                }
                break;

            case Tab.Enhance:
                string matId = economy.materialItem != null ? economy.materialItem.itemId : "";

                foreach (var eq in p.equipment)
                {
                    var item = database.FindItem(eq.itemId);
                    if (item == null) continue;
                    var step = economy.NextEnhance(eq.enhance);
                    int uid = eq.uid;
                    string stat = item.type == ItemType.Weapon
                        ? "공격 " + (item.attackBonus + item.attackPerEnhance * eq.enhance) + (step != null ? " → " + (item.attackBonus + item.attackPerEnhance * (eq.enhance + 1)) : "")
                        : "방어 " + (item.defenseBonus + item.defensePerEnhance * eq.enhance) + (step != null ? " → " + (item.defenseBonus + item.defensePerEnhance * (eq.enhance + 1)) : "");

                    entries.Add(new RowEntry
                    {
                        item = item, uid = uid, icon = item.icon,
                        name = ShopLogic.NameWithEnhance(item, eq.enhance) + (p.IsEquipped(uid) ? " (장착)" : ""),
                        info = step != null
                            ? stat + "\n" + ShopLogic.EnhanceGold(step, p) + "G · 강화석 " + step.materials + "개 (보유 " + p.CountOf(matId) + ")"
                            : stat + "\n최대 강화",
                        button = step != null ? "강화" : "최대",
                        enabled = step != null,
                        onClick = () => OnEnhance(uid)
                    });
                }
                break;
        }
    }

    // ---------- 행동 ----------

    void OnBuyEntry(ShopEntry entry, int price)
    {
        if (!entry.item.IsEquipment)
        {
            Act(ShopLogic.Buy(State, entry));
            return;
        }

        ShowConfirm(Josa.Eul(entry.item.itemName) + " " + price + "G에 살까?", "구매", () => Act(ShopLogic.Buy(State, entry)),
                    "구매 후 장착", () =>
                    {
                        var r = ShopLogic.Buy(State, entry);
                        if (r.ok && PlayerStats.Equip(Player, r.equipmentUid, settings, database)) r.message += " 장착했다.";
                        Act(r);
                    });
    }

    void OnSellEquipment(int uid, int price)
    {
        var eq = Player.FindEquipment(uid);
        var item = eq != null ? database.FindItem(eq.itemId) : null;
        if (item == null) return;

        // 장착은 홈 창(C-3)에서. 상점에서는 판매만
        ShowConfirm(ShopLogic.NameWithEnhance(item, eq.enhance) + "\n판매가 " + price + "G", "판매",
                    () => Act(ShopLogic.SellEquipment(Player, uid, economy, database)), null, null);
    }

    void OnEnhance(int uid)
    {
        var eq = Player.FindEquipment(uid);
        var item = eq != null ? database.FindItem(eq.itemId) : null;
        var step = eq != null ? economy.NextEnhance(eq.enhance) : null;
        if (item == null || step == null) return;

        ShowConfirm(ShopLogic.NameWithEnhance(item, eq.enhance) + " → +" + (eq.enhance + 1) + "\n" + ShopLogic.EnhanceGold(step, Player) + "G · 강화석 " + step.materials + "개",
                    "강화", () => Act(ShopLogic.Enhance(Player, uid, economy, settings, database)), null, null);
    }

    void Act(ShopResult r)
    {
        SetMessage(r.message);
        Refresh();
    }

    // ---------- 확인 창 ----------

    void ShowConfirm(string text, string okLabel, Action ok, string altLabel, Action alt)
    {
        confirmOk = ok;
        confirmAlt = alt;
        if (confirmText != null) confirmText.text = text;
        if (confirmOkText != null) confirmOkText.text = okLabel;
        if (confirmAltButton != null) confirmAltButton.gameObject.SetActive(alt != null);
        if (confirmAltText != null && altLabel != null) confirmAltText.text = altLabel;
        if (confirmPopup != null) confirmPopup.SetActive(true);
    }

    void HideConfirm()
    {
        confirmOk = confirmAlt = null;
        if (confirmPopup != null) confirmPopup.SetActive(false);
    }

    public bool ConfirmOpen => confirmPopup != null && confirmPopup.activeSelf;

    // ---------- 도우미 ----------

    void SetMessage(string text)
    {
        if (messageText != null) messageText.text = text;
    }

    public string Message => messageText != null ? messageText.text : "";

    static void SetTabLook(Button b, bool on)
    {
        if (b == null) return;
        var img = b.targetGraphic as Image;
        if (img != null) img.color = on ? new Color(1f, 0.85f, 0.4f) : Color.white;
    }

    void SaveDefaults()
    {
        if (defaultsSaved || keeperImage == null) return;
        defaultKeeperSprite = keeperImage.sprite;
        defaultKeeperColor = keeperImage.color;
        defaultsSaved = true;
    }
}

// 상품 행 1개의 화면 칸
[Serializable]
public class ShopRowView
{
    public GameObject root;
    public Image icon;
    public TMP_Text nameText;
    public TMP_Text infoText;
    public Button actionButton;
    public TMP_Text actionText;

    public void Show(ShopController.RowEntry e)
    {
        if (root == null) return;
        root.SetActive(e != null);
        if (e == null) return;

        if (nameText != null) nameText.text = e.name;
        if (infoText != null) infoText.text = e.info;
        if (actionText != null) actionText.text = e.button;
        if (actionButton != null) actionButton.interactable = e.enabled;

        if (icon != null)
        {
            icon.sprite = e.icon;
            icon.color = e.icon != null ? Color.white : new Color(0.6f, 0.6f, 0.6f);
        }
    }
}

using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

// 개발용: 상단 바(TopBar)·상점 창(ShopPanel)·병원 창(HospitalPanel)을 코드로 만든다. 다시 실행하면 지우고 새로 만든다.
// 상점 배치는 docs/맵구상도.drawio "일반 상점 구매 창", 상단 바는 "전체 맵/공통 상단" (1920x1080 기준).
// 순서: 상단 바는 장소 내부 위·지도 정보창 아래, 상점·병원은 전투·만화 아래 (전투·만화가 덮는다).
public static class EconomyUIBuilder
{
    static TMP_FontAsset font;
    static Sprite square;
    static Sprite uiSprite;

    static readonly Color Dark = new Color(0.15f, 0.15f, 0.15f);
    static readonly Color White = Color.white;
    static readonly Color Gray = new Color(0.6f, 0.6f, 0.6f);
    static readonly Vector2 Mid = new Vector2(0.5f, 0.5f);

    const string SettingsPath = "Assets/Data/Battle/BattleSettings.asset";
    const string DatabasePath = "Assets/Data/Battle/GameDatabase.asset";
    const string EconomyPath = "Assets/Data/Economy/EconomySettings.asset";

    [MenuItem("요원/(개발) 상점·병원 화면 다시 만들기")]
    public static void Build()
    {
        if (EditorApplication.isPlaying)
        {
            Debug.LogWarning("Play 모드에서는 만들 수 없습니다.");
            return;
        }

        var canvas = GameObject.Find("Canvas");
        var mapView = Object.FindAnyObjectByType<MapView>();
        InteractionController ic = null;

        foreach (var c in Resources.FindObjectsOfTypeAll<InteractionController>())
        {
            if (!EditorUtility.IsPersistent(c)) ic = c;
        }

        if (canvas == null || mapView == null || ic == null)
        {
            Debug.LogWarning("Canvas·MapView·InteractionController를 찾지 못했습니다.");
            return;
        }

        font = canvas.GetComponentInChildren<TMP_Text>(true).font;
        square = mapView.emptyMapSprite;
        uiSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");

        var settings = AssetDatabase.LoadAssetAtPath<BattleSettings>(SettingsPath);
        var db = AssetDatabase.LoadAssetAtPath<GameDatabase>(DatabasePath);
        var eco = AssetDatabase.LoadAssetAtPath<EconomySettings>(EconomyPath);

        foreach (var n in new[] { "TopBar", "ShopPanel", "HospitalPanel", "TrainingPanel", "HomePanel", "PickPanel", "NoticeToast" })
        {
            var old = canvas.transform.Find(n);
            if (old != null) Undo.DestroyObjectImmediate(old.gameObject);
        }

        var selector = Object.FindAnyObjectByType<LocationSelector>();
        var shop = BuildShop(canvas.transform, settings, db, eco);
        var hospital = BuildHospital(canvas.transform, settings, db, eco);
        var training = BuildTraining(canvas.transform, settings, db);
        var home = BuildHome(canvas.transform, settings, db, eco, selector != null ? selector.battleController : null);
        var top = BuildTopBar(canvas.transform, settings, db);
        top.home = home;

        // [C-4] 재능·특성 선택 창, 알림, 홈 창 연결
        var pick = BuildPick(canvas.transform, settings, db, eco);
        pick.mapView = mapView; pick.selector = selector; pick.home = home;
        home.pickController = pick; home.mapView = mapView; home.selector = selector;
        BuildToast(canvas.transform);

        if (selector != null && selector.battleController != null)
        {
            Undo.RecordObject(selector.battleController, "pick");
            selector.battleController.pickController = pick;
            EditorUtility.SetDirty(selector.battleController);
        }

        // 그리는 순서 (뒤가 위): 정보창은 상단 바보다 위 (오른쪽 위에 열려도 X 버튼이 가려지지 않게).
        // 정보창과 장소 내부는 동시에 열리지 않는다. 홈 창은 전투 위(전투 중 보기), 선택 창은 홈 위, 만화 위에 알림
        string[] order = { "BackToRegionButton", "PlaceInteriorPanel", "TopBar", "LocationInfoPanel", "ShopPanel", "HospitalPanel", "TrainingPanel", "BattlePanel", "HomePanel", "PickPanel", "ComicPanel", "NoticeToast" };

        for (int i = 0; i < order.Length; i++)
        {
            var t = canvas.transform.Find(order[i]);
            if (t != null) t.SetAsLastSibling();
        }

        Undo.RecordObject(ic, "economy ui");
        ic.shopController = shop;
        ic.hospitalController = hospital;
        ic.trainingController = training;
        training.mapView = mapView;
        training.selector = Object.FindAnyObjectByType<LocationSelector>();

        // [C-5] 전투 화면 가장자리 연출 (없으면 붙인다)
        var battlePanel = canvas.transform.Find("BattlePanel");

        if (battlePanel != null && battlePanel.GetComponent<BattleScreenEffects>() == null)
        {
            var fx = Undo.AddComponent<BattleScreenEffects>(battlePanel.gameObject);
            fx.battle = battlePanel.GetComponent<BattleController>();
            EditorUtility.SetDirty(fx);
        }
        EditorUtility.SetDirty(ic);

        // [C-3] 전투 화면 프로필 버튼 → 홈 창 (보기 전용)
        var battleProfile = canvas.transform.Find("BattlePanel/ProfileButton");

        if (battleProfile != null)
        {
            var b = battleProfile.GetComponent<Button>();
            Undo.RecordObject(b, "profile");
            for (int i = b.onClick.GetPersistentEventCount() - 1; i >= 0; i--) UnityEventTools.RemovePersistentListener(b.onClick, i);
            UnityEventTools.AddPersistentListener(b.onClick, home.Open);
            b.interactable = true;   // B-2에서는 자리만이라 꺼 두었음
            EditorUtility.SetDirty(b);

            // [C-4] 빨간 점
            var oldDot = battleProfile.Find("Badge");
            if (oldDot != null) Undo.DestroyObjectImmediate(oldDot.gameObject);
            var oldBadge = battleProfile.GetComponent<PendingBadge>();
            if (oldBadge != null) Undo.DestroyObjectImmediate(oldBadge);
            AddBadge(battleProfile, db);
        }

        EditorSceneManager.MarkSceneDirty(canvas.scene);
        EditorSceneManager.SaveScene(canvas.scene);
        Debug.Log("상단 바·상점 창·병원 창·홈 창을 만들고 씬을 저장했습니다.");
    }

    // ---------- [C-3] 홈·스탯 창 ----------

    static HomeController BuildHome(Transform canvas, BattleSettings settings, GameDatabase db, EconomySettings eco, BattleController battle)
    {
        var root = Rect("HomePanel", canvas, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        Undo.RegisterCreatedObjectUndo(root.gameObject, "HomePanel");
        AddImage(root, new Color(0f, 0f, 0f, 0.6f), null);
        var hc = root.gameObject.AddComponent<HomeController>();
        hc.settings = settings; hc.database = db; hc.economy = eco; hc.battleController = battle;

        var win = Rect("Window", root, Mid, Mid, new Vector2(0, -20), new Vector2(1640, 860));
        AddImage(win, new Color(0.93f, 0.93f, 0.93f), uiSprite).type = Image.Type.Sliced;

        Text("TitleText", win, "홈", 44, Dark, TextAlignmentOptions.Left, Mid, new Vector2(-560, 360), new Vector2(400, 70));
        hc.summaryText = Text("SummaryText", win, "", 28, Dark, TextAlignmentOptions.Right, Mid, new Vector2(230, 360), new Vector2(1060, 60));

        hc.statsTabButton = Button("StatsTab", win, "스탯", 30, Mid, new Vector2(-600, 270), new Vector2(220, 64), hc.OnStatsTab);
        hc.equipmentTabButton = Button("EquipmentTab", win, "장비", 30, Mid, new Vector2(-370, 270), new Vector2(220, 64), hc.OnEquipmentTab);
        hc.inventoryTabButton = Button("InventoryTab", win, "인벤토리", 30, Mid, new Vector2(-140, 270), new Vector2(220, 64), hc.OnInventoryTab);
        hc.growthTabButton = Button("GrowthTab", win, "성장", 30, Mid, new Vector2(90, 270), new Vector2(220, 64), hc.OnGrowthTab);

        // 글 영역 (스탯·성장)
        var text = Rect("TextArea", win, Mid, Mid, new Vector2(0, -20), new Vector2(1520, 480));
        hc.textArea = text.gameObject;
        hc.leftText = Text("LeftText", text, "", 30, Dark, TextAlignmentOptions.TopLeft, Mid, new Vector2(-390, 0), new Vector2(720, 470));

        // [C-4] 스탯 탭 왼쪽: 남은 포인트 + 스탯 5줄 (이름 · 값 · 효과 · +)
        var stat = Rect("StatArea", win, Mid, Mid, new Vector2(-390, -20), new Vector2(720, 480));
        hc.statArea = stat.gameObject;
        hc.pointsText = Text("PointsText", stat, "", 30, new Color(0.7f, 0.2f, 0.1f), TextAlignmentOptions.Left, Mid, new Vector2(0, 205), new Vector2(720, 50));
        UnityAction[] statClicks = { hc.OnStat0, hc.OnStat1, hc.OnStat2, hc.OnStat3, hc.OnStat4 };

        for (int i = 0; i < 5; i++)
        {
            var row = Rect("StatRow" + i, stat, Mid, Mid, new Vector2(0, 125 - i * 80), new Vector2(720, 70));
            var v = new StatRowView();
            v.nameText = Text("NameText", row, "", 30, Dark, TextAlignmentOptions.Left, Mid, new Vector2(-280, 0), new Vector2(160, 60));
            v.valueText = Text("ValueText", row, "", 32, Dark, TextAlignmentOptions.Center, Mid, new Vector2(-150, 0), new Vector2(90, 60));
            v.hintText = Text("HintText", row, "", 22, Gray, TextAlignmentOptions.Left, Mid, new Vector2(80, 0), new Vector2(340, 60));
            v.plusButton = Button("PlusButton", row, "+", 34, Mid, new Vector2(310, 0), new Vector2(70, 62), statClicks[i]);
            ((Image)v.plusButton.targetGraphic).color = new Color(1f, 0.92f, 0.7f);
            hc.statRows.Add(v);
        }

        // [C-4] 성장 탭: 재능·특성 고르기
        var gb = Rect("GrowthButtons", win, Mid, Mid, new Vector2(0, -270), new Vector2(1520, 80));
        hc.growthButtons = gb.gameObject;
        hc.talentPickButton = Button("TalentPickButton", gb, "재능 고르기", 28, Mid, new Vector2(-560, 0), new Vector2(300, 70), hc.OnTalentPick);
        hc.talentPickText = hc.talentPickButton.GetComponentInChildren<TMP_Text>();
        hc.traitPickButton = Button("TraitPickButton", gb, "특성 고르기", 28, Mid, new Vector2(-230, 0), new Vector2(300, 70), hc.OnTraitPick);
        hc.traitPickText = hc.traitPickButton.GetComponentInChildren<TMP_Text>();
        hc.rightText = Text("RightText", text, "", 30, Dark, TextAlignmentOptions.TopLeft, Mid, new Vector2(390, 0), new Vector2(720, 470));

        // 목록 영역 (장비·인벤토리). 장비 탭이면 위에 장착 칸 2개
        var list = Rect("ListArea", win, Mid, Mid, new Vector2(0, -20), new Vector2(1520, 520));
        hc.listArea = list.gameObject;

        var slots = Rect("SlotArea", list, Mid, Mid, new Vector2(0, 205), new Vector2(1520, 100));
        hc.slotArea = slots.gameObject;
        hc.weaponSlotText = Text("WeaponSlotText", slots, "", 26, Dark, TextAlignmentOptions.Left, Mid, new Vector2(-470, 0), new Vector2(480, 90));
        hc.weaponUnequipButton = Button("WeaponUnequipButton", slots, "해제", 26, Mid, new Vector2(-150, 0), new Vector2(130, 70), hc.OnUnequipWeapon);
        hc.armorSlotText = Text("ArmorSlotText", slots, "", 26, Dark, TextAlignmentOptions.Left, Mid, new Vector2(300, 0), new Vector2(480, 90));
        hc.armorUnequipButton = Button("ArmorUnequipButton", slots, "해제", 26, Mid, new Vector2(620, 0), new Vector2(130, 70), hc.OnUnequipArmor);

        UnityAction[] rowClicks = { hc.OnRow0, hc.OnRow1, hc.OnRow2, hc.OnRow3 };

        for (int i = 0; i < 4; i++)
        {
            var row = Rect("Row" + i, list, Mid, Mid, new Vector2(0, 95 - i * 100), new Vector2(1400, 92));
            AddImage(row, White, uiSprite).type = Image.Type.Sliced;
            var v = new ShopRowView { root = row.gameObject };
            v.icon = AddImage(Rect("Icon", row, Mid, Mid, new Vector2(-640, 0), new Vector2(76, 76)), Gray, null);
            v.nameText = Text("NameText", row, "", 28, Dark, TextAlignmentOptions.Left, Mid, new Vector2(-410, 0), new Vector2(360, 86));
            v.infoText = Text("InfoText", row, "", 24, new Color(0.3f, 0.3f, 0.3f), TextAlignmentOptions.Left, Mid, new Vector2(110, 0), new Vector2(660, 86));
            v.actionButton = Button("ActionButton", row, "", 26, Mid, new Vector2(580, 0), new Vector2(200, 76), rowClicks[i]);
            v.actionText = v.actionButton.GetComponentInChildren<TMP_Text>();
            ((Image)v.actionButton.targetGraphic).color = new Color(1f, 0.92f, 0.7f);
            hc.rows.Add(v);
        }

        hc.prevPageButton = Button("PrevPageButton", list, "▲", 30, Mid, new Vector2(390, -240), new Vector2(90, 60), hc.OnPrevPage);
        hc.pageText = Text("PageText", list, "", 26, Dark, TextAlignmentOptions.Center, Mid, new Vector2(170, -240), new Vector2(340, 50));
        hc.nextPageButton = Button("NextPageButton", list, "▼", 30, Mid, new Vector2(500, -240), new Vector2(90, 60), hc.OnNextPage);

        hc.messageText = Text("MessageText", win, "", 28, new Color(0.1f, 0.3f, 0.6f), TextAlignmentOptions.Left, Mid, new Vector2(-150, -375), new Vector2(1200, 60));
        Button("CloseButton", win, "닫기", 32, Mid, new Vector2(680, -375), new Vector2(200, 72), hc.Close);

        root.gameObject.SetActive(false);
        EditorUtility.SetDirty(hc);
        return hc;
    }

    // ---------- 상점 창 ----------

    static ShopController BuildShop(Transform canvas, BattleSettings settings, GameDatabase db, EconomySettings eco)
    {
        var root = Rect("ShopPanel", canvas, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        Undo.RegisterCreatedObjectUndo(root.gameObject, "ShopPanel");
        AddImage(root, new Color(0f, 0f, 0f, 0.6f), null);   // 뒤 화면 클릭 막기
        var sc = root.gameObject.AddComponent<ShopController>();
        sc.settings = settings; sc.database = db; sc.economy = eco;

        var win = Rect("Window", root, Mid, Mid, new Vector2(0, -20), new Vector2(1640, 860));
        AddImage(win, new Color(0.93f, 0.93f, 0.93f), uiSprite).type = Image.Type.Sliced;

        // 왼쪽: 점원
        var keeper = Rect("KeeperImage", win, Mid, Mid, new Vector2(-610, 40), new Vector2(340, 640));
        sc.keeperImage = AddImage(keeper, Gray, square);
        Text("KeeperPlaceholder", keeper, "[점원 이미지 미정]", 26, Dark, TextAlignmentOptions.Center, Mid, Vector2.zero, new Vector2(320, 80));
        sc.keeperNameText = Text("KeeperNameText", win, "", 28, Dark, TextAlignmentOptions.Center, Mid, new Vector2(-610, -320), new Vector2(340, 50));

        // 오른쪽 위: 상점 아이콘·이름
        var icon = Rect("ShopIcon", win, Mid, Mid, new Vector2(-340, 360), new Vector2(70, 70));
        sc.shopIcon = AddImage(icon, White, null);
        sc.shopNameText = Text("ShopNameText", win, "", 44, Dark, TextAlignmentOptions.Left, Mid, new Vector2(110, 360), new Vector2(820, 70));

        // 탭
        sc.buyTabButton = Button("BuyTab", win, "구매", 30, Mid, new Vector2(-260, 280), new Vector2(200, 64), sc.OnBuyTab);
        sc.sellTabButton = Button("SellTab", win, "판매", 30, Mid, new Vector2(-40, 280), new Vector2(200, 64), sc.OnSellTab);
        sc.enhanceTabButton = Button("EnhanceTab", win, "강화", 30, Mid, new Vector2(180, 280), new Vector2(200, 64), sc.OnEnhanceTab);

        // 상품 행 4개: 이미지 · 이름 · 효과와 소지 개수 · 버튼
        UnityAction[] rowClicks = { sc.OnRow0, sc.OnRow1, sc.OnRow2, sc.OnRow3 };

        for (int i = 0; i < 4; i++)
        {
            var row = Rect("Row" + i, win, Mid, Mid, new Vector2(190, 160 - i * 115), new Vector2(1180, 105));
            AddImage(row, White, uiSprite).type = Image.Type.Sliced;
            var v = new ShopRowView { root = row.gameObject };
            var ri = Rect("Icon", row, Mid, Mid, new Vector2(-530, 0), new Vector2(86, 86));
            v.icon = AddImage(ri, Gray, null);
            v.nameText = Text("NameText", row, "", 28, Dark, TextAlignmentOptions.Left, Mid, new Vector2(-330, 0), new Vector2(290, 96));
            v.infoText = Text("InfoText", row, "", 24, new Color(0.3f, 0.3f, 0.3f), TextAlignmentOptions.Left, Mid, new Vector2(80, 0), new Vector2(500, 96));
            v.actionButton = Button("ActionButton", row, "", 26, Mid, new Vector2(460, 0), new Vector2(220, 84), rowClicks[i]);
            v.actionText = v.actionButton.GetComponentInChildren<TMP_Text>();
            ((Image)v.actionButton.targetGraphic).color = new Color(1f, 0.92f, 0.7f);
            sc.rows.Add(v);
        }

        // 아래: 소지 골드 · 페이지 · 메시지 · 나가기
        sc.goldText = Text("GoldText", win, "", 30, Dark, TextAlignmentOptions.Left, Mid, new Vector2(-190, -300), new Vector2(420, 50));
        sc.prevPageButton = Button("PrevPageButton", win, "▲", 30, Mid, new Vector2(360, -300), new Vector2(90, 60), sc.OnPrevPage);
        sc.pageText = Text("PageText", win, "1 / 1", 28, Dark, TextAlignmentOptions.Center, Mid, new Vector2(470, -300), new Vector2(120, 50));
        sc.nextPageButton = Button("NextPageButton", win, "▼", 30, Mid, new Vector2(580, -300), new Vector2(90, 60), sc.OnNextPage);
        sc.messageText = Text("MessageText", win, "", 28, new Color(0.1f, 0.3f, 0.6f), TextAlignmentOptions.Left, Mid, new Vector2(10, -375), new Vector2(1000, 60));
        Button("ExitButton", win, "나가기", 32, Mid, new Vector2(680, -375), new Vector2(200, 72), sc.Close);

        // 확인 창 (장비 구매·판매·강화)
        var confirm = Rect("ConfirmPopup", win, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        AddImage(confirm, new Color(0f, 0f, 0f, 0.45f), null);
        var box = Rect("ConfirmBox", confirm, Mid, Mid, Vector2.zero, new Vector2(900, 380));
        AddImage(box, White, uiSprite).type = Image.Type.Sliced;
        sc.confirmText = Text("ConfirmText", box, "", 32, Dark, TextAlignmentOptions.Center, Mid, new Vector2(0, 70), new Vector2(820, 180));
        sc.confirmOkButton = Button("ConfirmOkButton", box, "확인", 30, Mid, new Vector2(-260, -110), new Vector2(240, 84), sc.OnConfirmOk);
        sc.confirmOkText = sc.confirmOkButton.GetComponentInChildren<TMP_Text>();
        sc.confirmAltButton = Button("ConfirmAltButton", box, "", 30, Mid, new Vector2(0, -110), new Vector2(240, 84), sc.OnConfirmAlt);
        sc.confirmAltText = sc.confirmAltButton.GetComponentInChildren<TMP_Text>();
        Button("ConfirmCancelButton", box, "취소", 30, Mid, new Vector2(260, -110), new Vector2(240, 84), sc.OnConfirmCancel);
        confirm.gameObject.SetActive(false);
        sc.confirmPopup = confirm.gameObject;

        root.gameObject.SetActive(false);
        EditorUtility.SetDirty(sc);
        return sc;
    }

    // ---------- 병원 창 ----------

    static HospitalController BuildHospital(Transform canvas, BattleSettings settings, GameDatabase db, EconomySettings eco)
    {
        var root = Rect("HospitalPanel", canvas, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        Undo.RegisterCreatedObjectUndo(root.gameObject, "HospitalPanel");
        AddImage(root, new Color(0f, 0f, 0f, 0.6f), null);
        var hc = root.gameObject.AddComponent<HospitalController>();
        hc.settings = settings; hc.database = db; hc.economy = eco;

        var win = Rect("Window", root, Mid, Mid, Vector2.zero, new Vector2(1100, 640));
        AddImage(win, new Color(0.93f, 0.93f, 0.93f), uiSprite).type = Image.Type.Sliced;

        hc.titleText = Text("TitleText", win, "", 44, Dark, TextAlignmentOptions.Center, Mid, new Vector2(0, 250), new Vector2(1000, 70));
        hc.statusText = Text("StatusText", win, "", 32, Dark, TextAlignmentOptions.Center, Mid, new Vector2(0, 120), new Vector2(1000, 120));
        hc.healButton = Button("HealButton", win, "전체 치료", 36, Mid, new Vector2(0, -20), new Vector2(420, 100), hc.OnHeal);
        hc.healButtonText = hc.healButton.GetComponentInChildren<TMP_Text>();
        ((Image)hc.healButton.targetGraphic).color = new Color(0.75f, 0.95f, 0.8f);
        hc.messageText = Text("MessageText", win, "", 28, new Color(0.1f, 0.3f, 0.6f), TextAlignmentOptions.Center, Mid, new Vector2(0, -140), new Vector2(1000, 80));
        Button("CloseButton", win, "닫기", 32, Mid, new Vector2(0, -250), new Vector2(240, 80), hc.Close);

        root.gameObject.SetActive(false);
        EditorUtility.SetDirty(hc);
        return hc;
    }

    // ---------- 상단 바 ----------

    static TopBarController BuildTopBar(Transform canvas, BattleSettings settings, GameDatabase db)
    {
        // 화면 전체를 덮지 않게 오른쪽 위 영역만
        var root = Rect("TopBar", canvas, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-280, -70), new Vector2(520, 110));
        Undo.RegisterCreatedObjectUndo(root.gameObject, "TopBar");
        var tb = root.gameObject.AddComponent<TopBarController>();
        tb.settings = settings; tb.database = db;

        var gold = Rect("GoldBox", root, Mid, Mid, new Vector2(-90, 0), new Vector2(300, 80));
        AddImage(gold, new Color(1f, 1f, 1f, 0.9f), uiSprite).type = Image.Type.Sliced;
        tb.goldText = Text("GoldText", gold, "0G", 34, Dark, TextAlignmentOptions.Right, Mid, new Vector2(-10, 0), new Vector2(260, 70));
        Text("GoldLabel", gold, "골드", 26, Gray, TextAlignmentOptions.Left, Mid, new Vector2(-90, 0), new Vector2(100, 60));

        var profile = Button("ProfileButton", root, "프로필", 24, Mid, new Vector2(170, 0), new Vector2(100, 100), tb.OnProfile);
        AddBadge(profile.transform, db);

        EditorUtility.SetDirty(tb);
        return tb;
    }

    // ---------- [C-4] 프로필 빨간 점 ----------

    static void AddBadge(Transform button, GameDatabase db)
    {
        var dot = Rect("Badge", button, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-8, -8), new Vector2(28, 28));
        var img = AddImage(dot, new Color(0.9f, 0.1f, 0.1f), AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd"));
        img.raycastTarget = false;
        var badge = button.gameObject.AddComponent<PendingBadge>();
        badge.database = db;
        badge.dot = dot.gameObject;
        dot.gameObject.SetActive(false);
        EditorUtility.SetDirty(badge);
    }

    // ---------- [C-4] 재능·특성 선택 창 ----------

    static PickController BuildPick(Transform canvas, BattleSettings settings, GameDatabase db, EconomySettings eco)
    {
        var root = Rect("PickPanel", canvas, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        Undo.RegisterCreatedObjectUndo(root.gameObject, "PickPanel");
        AddImage(root, new Color(0f, 0f, 0f, 0.7f), null);
        var pc = root.gameObject.AddComponent<PickController>();
        pc.settings = settings; pc.database = db; pc.economy = eco;

        pc.titleText = Text("TitleText", root, "", 46, White, TextAlignmentOptions.Center, Mid, new Vector2(0, 330), new Vector2(1600, 80));
        UnityAction[] clicks = { pc.OnCard0, pc.OnCard1, pc.OnCard2, pc.OnCard3 };

        for (int i = 0; i < 4; i++)
        {
            var b = Button("Card" + i, root, "", 30, Mid, new Vector2(-615 + i * 410, 20), new Vector2(380, 480), clicks[i]);
            Object.DestroyImmediate(b.transform.Find("Text (TMP)").gameObject);
            var v = new PickCardView { root = b.gameObject };
            v.nameText = Text("NameText", b.transform, "", 36, Dark, TextAlignmentOptions.Center, Mid, new Vector2(0, 160), new Vector2(340, 90));
            v.descText = Text("DescText", b.transform, "", 26, new Color(0.3f, 0.3f, 0.3f), TextAlignmentOptions.Top, Mid, new Vector2(0, -60), new Vector2(330, 300));
            pc.cards.Add(v);
        }

        Button("LaterButton", root, "나중에 고르기", 30, Mid, new Vector2(0, -330), new Vector2(300, 80), pc.Close);

        root.gameObject.SetActive(false);
        EditorUtility.SetDirty(pc);
        return pc;
    }

    // ---------- [C-5] 훈련 창 ----------

    static TrainingController BuildTraining(Transform canvas, BattleSettings settings, GameDatabase db)
    {
        var root = Rect("TrainingPanel", canvas, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        Undo.RegisterCreatedObjectUndo(root.gameObject, "TrainingPanel");
        AddImage(root, new Color(0f, 0f, 0f, 0.6f), null);
        var tc = root.gameObject.AddComponent<TrainingController>();
        tc.settings = settings; tc.database = db;

        var win = Rect("Window", root, Mid, Mid, Vector2.zero, new Vector2(1400, 700));
        AddImage(win, new Color(0.93f, 0.93f, 0.93f), uiSprite).type = Image.Type.Sliced;

        tc.titleText = Text("TitleText", win, "", 44, Dark, TextAlignmentOptions.Center, Mid, new Vector2(0, 280), new Vector2(1200, 70));
        tc.infoText = Text("InfoText", win, "", 30, Dark, TextAlignmentOptions.Center, Mid, new Vector2(0, 175), new Vector2(1300, 100));

        // 게이지: 배경 · 목표 구간(초록) · 채움 · 오른쪽 끝 = 임계점(빨간 선)
        var gauge = Rect("Gauge", win, Mid, Mid, new Vector2(0, 40), new Vector2(1200, 80));
        tc.gaugeArea = gauge;
        AddImage(gauge, new Color(0.2f, 0.2f, 0.22f), square);
        var zone = Rect("Zone", gauge, new Vector2(0.65f, 0f), new Vector2(0.85f, 1f), Vector2.zero, Vector2.zero);
        AddImage(zone, new Color(0.3f, 0.85f, 0.4f, 0.45f), square).raycastTarget = false;
        tc.zoneRect = zone;
        var fillRt = Rect("Fill", gauge, Vector2.zero, Vector2.one, Vector2.zero, new Vector2(0, -36));
        var fill = AddImage(fillRt, new Color(0.95f, 0.65f, 0.15f), square);
        fill.type = Image.Type.Filled;
        fill.fillMethod = Image.FillMethod.Horizontal;
        fill.fillAmount = 0f;
        fill.raycastTarget = false;
        tc.fill = fill;
        var limit = Rect("LimitLine", gauge, new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(-3, 0), new Vector2(6, 20));
        AddImage(limit, new Color(0.9f, 0.1f, 0.1f), square).raycastTarget = false;
        Text("LimitLabel", gauge, "임계점", 22, new Color(0.9f, 0.2f, 0.2f), TextAlignmentOptions.Right, new Vector2(1f, 1f), new Vector2(-60, 30), new Vector2(160, 36));

        tc.timerText = Text("TimerText", win, "", 34, Dark, TextAlignmentOptions.Center, Mid, new Vector2(0, -50), new Vector2(400, 60));
        tc.resultText = Text("ResultText", win, "", 32, new Color(0.1f, 0.3f, 0.6f), TextAlignmentOptions.Center, Mid, new Vector2(0, -130), new Vector2(1300, 70));
        tc.startButton = Button("StartButton", win, "훈련 시작", 32, Mid, new Vector2(-170, -250), new Vector2(320, 90), tc.OnStart);
        tc.startButtonText = tc.startButton.GetComponentInChildren<TMP_Text>();
        ((Image)tc.startButton.targetGraphic).color = new Color(1f, 0.92f, 0.7f);
        tc.closeButton = Button("CloseButton", win, "닫기", 32, Mid, new Vector2(190, -250), new Vector2(240, 90), tc.Close);

        // 훈련 중: 화면 어디든 클릭 (창 위를 덮는 투명 버튼)
        var tap = Rect("TapArea", root, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        var tapImg = AddImage(tap, new Color(1f, 1f, 1f, 0.001f), null);
        var tapBtn = tap.gameObject.AddComponent<Button>();
        tapBtn.transition = Selectable.Transition.None;
        tapBtn.targetGraphic = tapImg;
        UnityEventTools.AddPersistentListener(tapBtn.onClick, tc.OnTap);
        Text("TapHint", tap, "연타!", 40, new Color(1f, 0.85f, 0.3f), TextAlignmentOptions.Center, Mid, new Vector2(0, -330), new Vector2(600, 70));
        tap.gameObject.SetActive(false);
        tc.tapArea = tap.gameObject;

        root.gameObject.SetActive(false);
        EditorUtility.SetDirty(tc);
        return tc;
    }

    // ---------- [C-4] 알림 ----------

    static void BuildToast(Transform canvas)
    {
        var root = Rect("NoticeToast", canvas, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -190), new Vector2(1000, 90));
        Undo.RegisterCreatedObjectUndo(root.gameObject, "NoticeToast");
        var nt = root.gameObject.AddComponent<NoticeToast>();
        var box = Rect("Box", root, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        var img = AddImage(box, new Color(0.1f, 0.1f, 0.15f, 0.88f), uiSprite);
        img.type = Image.Type.Sliced;
        img.raycastTarget = false;
        nt.text = Text("Text", box, "", 32, new Color(1f, 0.9f, 0.5f), TextAlignmentOptions.Center, Mid, Vector2.zero, new Vector2(960, 80));
        nt.box = box.gameObject;
        box.gameObject.SetActive(false);
        EditorUtility.SetDirty(nt);
    }

    // ---------- 도우미 (BattleUIBuilder와 같은 방식) ----------

    static RectTransform Rect(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 pos, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = LayerMask.NameToLayer("UI");
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        return rt;
    }

    static Image AddImage(RectTransform rt, Color color, Sprite sprite)
    {
        var img = rt.gameObject.AddComponent<Image>();
        img.color = color;
        img.sprite = sprite;
        return img;
    }

    static TMP_Text Text(string name, Transform parent, string text, float size, Color color, TextAlignmentOptions align,
                         Vector2 anchor, Vector2 pos, Vector2 box)
    {
        var rt = Rect(name, parent, anchor, anchor, pos, box);
        var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
        t.font = font;
        t.text = text;
        t.fontSize = size;
        t.color = color;
        t.alignment = align;
        t.raycastTarget = false;
        return t;
    }

    static Button Button(string name, Transform parent, string label, float fontSize, Vector2 anchor, Vector2 pos, Vector2 size, UnityAction onClick)
    {
        var rt = Rect(name, parent, anchor, anchor, pos, size);
        var img = AddImage(rt, White, uiSprite);
        img.type = Image.Type.Sliced;
        var b = rt.gameObject.AddComponent<Button>();
        b.targetGraphic = img;
        Text("Text (TMP)", rt, label, fontSize, Dark, TextAlignmentOptions.Center, Vector2.one * 0.5f, Vector2.zero, size);

        if (onClick != null)
        {
            UnityEventTools.AddPersistentListener(b.onClick, onClick);
        }

        return b;
    }
}

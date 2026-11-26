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

        foreach (var n in new[] { "TopBar", "ShopPanel", "HospitalPanel" })
        {
            var old = canvas.transform.Find(n);
            if (old != null) Undo.DestroyObjectImmediate(old.gameObject);
        }

        var shop = BuildShop(canvas.transform, settings, db, eco);
        var hospital = BuildHospital(canvas.transform, settings, db, eco);
        var top = BuildTopBar(canvas.transform, settings, db);

        // 그리는 순서 (뒤가 위): 정보창은 상단 바보다 위 (오른쪽 위에 열려도 X 버튼이 가려지지 않게).
        // 정보창과 장소 내부는 동시에 열리지 않는다. 전투·만화가 맨 위
        string[] order = { "BackToRegionButton", "PlaceInteriorPanel", "TopBar", "LocationInfoPanel", "ShopPanel", "HospitalPanel", "BattlePanel", "ComicPanel" };

        for (int i = 0; i < order.Length; i++)
        {
            var t = canvas.transform.Find(order[i]);
            if (t != null) t.SetAsLastSibling();
        }

        Undo.RecordObject(ic, "economy ui");
        ic.shopController = shop;
        ic.hospitalController = hospital;
        EditorUtility.SetDirty(ic);

        EditorSceneManager.MarkSceneDirty(canvas.scene);
        EditorSceneManager.SaveScene(canvas.scene);
        Debug.Log("상단 바·상점 창·병원 창을 만들고 씬을 저장했습니다.");
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

        Button("ProfileButton", root, "프로필", 24, Mid, new Vector2(170, 0), new Vector2(100, 100), tb.OnProfile);

        EditorUtility.SetDirty(tb);
        return tb;
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

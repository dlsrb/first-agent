using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

// 개발용: 전투 화면(BattlePanel)을 코드로 만든다. 다시 실행하면 기존 BattlePanel을 지우고 새로 만든다.
// 배치는 docs/맵구상도.drawio "전투 화면" 5페이지 + 전투규칙.md 11번 (1920x1080 기준).
// 만든 뒤 위치·크기는 Inspector에서 조정해도 되지만, 다시 실행하면 초기화되므로 조정 값은 이 파일에 반영할 것.
public static class BattleUIBuilder
{
    static TMP_FontAsset font;
    static Sprite square;
    static Sprite uiSprite;

    static readonly Color Dark = new Color(0.15f, 0.15f, 0.15f);
    static readonly Color White = Color.white;

    [MenuItem("요원/(개발) 전투 화면 다시 만들기")]
    public static void Build()
    {
        if (EditorApplication.isPlaying)
        {
            Debug.LogWarning("Play 모드에서는 만들 수 없습니다.");
            return;
        }

        var canvas = GameObject.Find("Canvas");
        var selector = Object.FindAnyObjectByType<LocationSelector>();
        var mapView = Object.FindAnyObjectByType<MapView>();

        if (canvas == null || selector == null || mapView == null)
        {
            Debug.LogWarning("Canvas·LocationSelector·MapView를 찾지 못했습니다.");
            return;
        }

        font = canvas.GetComponentInChildren<TMP_Text>(true).font;
        square = mapView.emptyMapSprite;
        uiSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");

        var old = canvas.transform.Find("BattlePanel");
        if (old != null) Undo.DestroyObjectImmediate(old.gameObject);

        // ---- 패널 ----
        var panel = Rect("BattlePanel", canvas.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        Undo.RegisterCreatedObjectUndo(panel.gameObject, "BattlePanel");
        AddImage(panel, new Color(0.22f, 0.22f, 0.25f, 1f), null);
        var bc = panel.gameObject.AddComponent<BattleController>();
        bc.settings = AssetDatabase.LoadAssetAtPath<BattleSettings>("Assets/Data/Battle/BattleSettings.asset");
        bc.database = AssetDatabase.LoadAssetAtPath<GameDatabase>("Assets/Data/Battle/GameDatabase.asset");

        var top = new Vector2(0.5f, 1f);
        var mid = new Vector2(0.5f, 0.5f);
        var bottom = new Vector2(0.5f, 0f);

        // ---- 위쪽: 괴이 체력바, 프로필·설정 ----
        var hpBar = Bar("MonsterHpBar", panel, top, new Vector2(0, -70), new Vector2(1200, 46), new Color(0.85f, 0.2f, 0.2f), out bc.monsterHpFill);
        bc.monsterHpText = Text("MonsterHpText", hpBar, "0 / 0", 30, White, TextAlignmentOptions.Center, Vector2.one * 0.5f, Vector2.zero, new Vector2(1200, 46));

        var profile = Button("ProfileButton", panel, "프로필", 26, new Vector2(1, 1), new Vector2(-190, -70), new Vector2(100, 100), null);
        profile.interactable = false;
        var setting = Button("SettingsButton", panel, "설정", 26, new Vector2(1, 1), new Vector2(-75, -70), new Vector2(100, 100), null);
        setting.interactable = false;

        // ---- 가운데: 괴이, 예고 말풍선, 메시지 ----
        // 세로 배치 (화면 중앙 기준): 괴이 20~380 / 이름 −10 / 수치 −55 / 메시지 −200~−80 / 주인공 체력바 −221 아래
        var monster = Rect("MonsterImage", panel, mid, mid, new Vector2(0, 200), new Vector2(260, 360));
        bc.monsterImage = AddImage(monster, new Color(0.55f, 0.55f, 0.55f), square);
        bc.monsterNameText = Text("MonsterNameText", panel, "[괴이 이름]", 40, White, TextAlignmentOptions.Center, mid, new Vector2(0, -10), new Vector2(800, 56));
        bc.monsterStatsText = Text("MonsterStatsText", panel, "공격 ??? / 방어 ???", 28, new Color(0.8f, 0.8f, 0.8f), TextAlignmentOptions.Center, mid, new Vector2(0, -55), new Vector2(800, 40));

        var bubble = Rect("IntentBubble", panel, mid, mid, new Vector2(500, 260), new Vector2(580, 200));
        AddImage(bubble, White, uiSprite).type = Image.Type.Sliced;
        bc.intentText = Text("IntentText", bubble, "[예고]", 32, Dark, TextAlignmentOptions.Center, mid, Vector2.zero, new Vector2(540, 180));

        bc.messageText = Text("MessageText", panel, "", 30, new Color(1f, 0.92f, 0.6f), TextAlignmentOptions.Center, mid, new Vector2(0, -140), new Vector2(1500, 120));

        // ---- 아래쪽 가운데: 주인공 체력·기력, 왼쪽에 상태 효과 ----
        var pHp = Bar("PlayerHpBar", panel, bottom, new Vector2(0, 300), new Vector2(640, 38), new Color(0.3f, 0.75f, 0.35f), out bc.playerHpFill);
        bc.playerHpText = Text("PlayerHpText", pHp, "체력", 26, White, TextAlignmentOptions.Center, mid, Vector2.zero, new Vector2(640, 38));
        var pEn = Bar("PlayerEnergyBar", panel, bottom, new Vector2(0, 254), new Vector2(640, 30), new Color(0.35f, 0.55f, 0.95f), out bc.playerEnergyFill);
        bc.playerEnergyText = Text("PlayerEnergyText", pEn, "기력", 22, White, TextAlignmentOptions.Center, mid, Vector2.zero, new Vector2(640, 30));
        bc.effectsText = Text("EffectsText", panel, "", 28, new Color(1f, 0.7f, 0.6f), TextAlignmentOptions.TopRight, bottom, new Vector2(-640, 262), new Vector2(560, 110));

        // ---- 아래쪽: 행동 버튼 4개 ----
        var actions = Rect("ActionButtons", panel, bottom, bottom, new Vector2(0, 120), new Vector2(1800, 130));
        bc.actionButtons = actions.gameObject;
        bc.attackButton = Button("AttackButton", actions, "공격", 44, mid, new Vector2(-690, 0), new Vector2(400, 120), bc.OnAttackButton);
        bc.defendButton = Button("DefendButton", actions, "방어", 44, mid, new Vector2(-230, 0), new Vector2(400, 120), bc.OnDefendButton);
        bc.specialButton = Button("SpecialButton", actions, "특수 행동", 44, mid, new Vector2(230, 0), new Vector2(400, 120), null);
        bc.fleeButton = Button("FleeButton", actions, "도망", 44, mid, new Vector2(690, 0), new Vector2(400, 120), bc.OnFleeButton);

        // ---- 공격 기믹 영역 (주사위) ----
        // 화면 아래에서 25~225 (기력 바 239 아래)
        var atk = Rect("AttackArea", panel, bottom, bottom, new Vector2(0, 125), new Vector2(1500, 200));
        AddImage(atk, new Color(0.3f, 0.3f, 0.36f), null);
        bc.attackArea = atk.gameObject;
        var diceRoot = Rect("DiceRoot", atk, mid, mid, new Vector2(-380, 0), new Vector2(560, 180));
        var hlg = diceRoot.gameObject.AddComponent<HorizontalLayoutGroup>();
        hlg.childAlignment = TextAnchor.MiddleCenter; hlg.spacing = 30;
        hlg.childControlWidth = hlg.childControlHeight = false; hlg.childForceExpandWidth = hlg.childForceExpandHeight = false;
        bc.diceRoot = diceRoot;
        var die = Rect("DiceTemplate", diceRoot, mid, mid, Vector2.zero, new Vector2(150, 150));
        AddImage(die, White, uiSprite).type = Image.Type.Sliced;
        Text("Face", die, "?", 84, Dark, TextAlignmentOptions.Center, mid, Vector2.zero, new Vector2(150, 150));
        die.gameObject.SetActive(false);
        bc.diceTemplate = die.gameObject;
        bc.comboText = Text("ComboText", atk, "", 44, new Color(1f, 0.85f, 0.3f), TextAlignmentOptions.Left, mid, new Vector2(140, 40), new Vector2(520, 64));
        bc.previewText = Text("PreviewText", atk, "", 32, White, TextAlignmentOptions.Left, mid, new Vector2(140, -25), new Vector2(560, 56));
        bc.rollButton = Button("RollButton", atk, "굴리기", 40, mid, new Vector2(570, 30), new Vector2(300, 95), bc.OnRollButton);
        bc.confirmAttackButton = Button("ConfirmAttackButton", atk, "공격!", 40, mid, new Vector2(570, 30), new Vector2(300, 95), bc.OnConfirmAttack);
        bc.attackBackButton = Button("AttackBackButton", atk, "뒤로", 30, mid, new Vector2(570, -60), new Vector2(300, 60), bc.OnAttackBack);
        atk.gameObject.SetActive(false);

        // ---- 방어 창: 전체 화면 버튼 (어디를 눌러도 연속 터치) ----
        var def = Rect("DefensePopup", panel, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        AddImage(def, new Color(0f, 0f, 0f, 0.25f), null);
        var defButton = def.gameObject.AddComponent<Button>();
        defButton.transition = Selectable.Transition.None;
        UnityEventTools.AddPersistentListener(defButton.onClick, bc.OnDefenseTouch);
        // 행동 버튼 자리(아래쪽)에 표시 — 주인공 체력·기력 바(화면 아래에서 221~301)를 가리지 않게
        var defBox = Rect("DefenseBox", def, mid, mid, new Vector2(0, -415), new Vector2(900, 190));
        AddImage(defBox, White, uiSprite).type = Image.Type.Sliced;
        bc.defenseText = Text("DefenseText", defBox, "방어 확률", 38, Dark, TextAlignmentOptions.Center, mid, new Vector2(0, 20), new Vector2(860, 130));
        Bar("DefenseTimer", defBox, mid, new Vector2(0, -65), new Vector2(760, 22), new Color(1f, 0.6f, 0.2f), out bc.defenseTimerFill);
        def.gameObject.SetActive(false);
        bc.defensePopup = def.gameObject;

        // ---- 도망 창 ----
        var flee = Rect("FleePopup", panel, mid, mid, new Vector2(0, -415), new Vector2(1100, 200));
        AddImage(flee, White, uiSprite).type = Image.Type.Sliced;
        bc.fleeText = Text("FleeText", flee, "도망칠 수 있는 확률", 32, Dark, TextAlignmentOptions.Center, mid, new Vector2(-170, 0), new Vector2(720, 170));
        bc.fleeConfirmButton = Button("FleeConfirmButton", flee, "도망치기", 34, mid, new Vector2(380, 45), new Vector2(280, 80), bc.OnFleeConfirm);
        Button("FleeCancelButton", flee, "취소", 34, mid, new Vector2(380, -50), new Vector2(280, 80), bc.OnFleeCancel);
        flee.gameObject.SetActive(false);
        bc.fleePopup = flee.gameObject;

        // ---- 결과 창 ----
        var result = Rect("ResultPopup", panel, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        AddImage(result, new Color(0f, 0f, 0f, 0.5f), null);
        var box = Rect("ResultBox", result, mid, mid, Vector2.zero, new Vector2(900, 480));
        AddImage(box, White, uiSprite).type = Image.Type.Sliced;
        bc.resultTitleText = Text("ResultTitleText", box, "승리!", 70, Dark, TextAlignmentOptions.Center, mid, new Vector2(0, 140), new Vector2(820, 100));
        bc.resultDetailText = Text("ResultDetailText", box, "", 32, Dark, TextAlignmentOptions.Center, mid, new Vector2(0, -5), new Vector2(820, 170));
        Button("ResultOkButton", box, "확인", 40, mid, new Vector2(0, -170), new Vector2(320, 100), bc.OnResultOk);
        result.gameObject.SetActive(false);
        bc.resultPopup = result.gameObject;

        panel.SetAsLastSibling();
        panel.gameObject.SetActive(false);

        Undo.RecordObject(selector, "battleController");
        selector.battleController = bc;
        EditorUtility.SetDirty(selector);
        EditorUtility.SetDirty(bc);

        EditorSceneManager.MarkSceneDirty(selector.gameObject.scene);
        EditorSceneManager.SaveScene(selector.gameObject.scene);
        Debug.Log("전투 화면(BattlePanel)을 만들고 씬을 저장했습니다.");
    }

    // ---------- 도우미 ----------

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
        var t = Text("Text (TMP)", rt, label, fontSize, Dark, TextAlignmentOptions.Center, Vector2.one * 0.5f, Vector2.zero, size);

        if (onClick != null)
        {
            UnityEventTools.AddPersistentListener(b.onClick, onClick);
        }

        return b;
    }

    static RectTransform Bar(string name, Transform parent, Vector2 anchor, Vector2 pos, Vector2 size, Color fillColor, out Image fill)
    {
        var bg = Rect(name, parent, anchor, anchor, pos, size);
        AddImage(bg, new Color(0.12f, 0.12f, 0.12f), square);
        var f = Rect("Fill", bg, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        fill = AddImage(f, fillColor, square);
        fill.type = Image.Type.Filled;
        fill.fillMethod = Image.FillMethod.Horizontal;
        fill.fillOrigin = 0;
        fill.fillAmount = 1f;
        return bg;
    }
}

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// 개발용 통합 테스트 (Play 모드에서 실행): 메뉴 "요원 > (개발) 통합 테스트 실행"
// 시작 지역 데이터에서 단계를 자동으로 만든다 → 데이터가 늘면 테스트도 같이 늘어난다.
//  - 모든 구역·장소 진입, 조사 결과, 대화(첫 대사 → 닫기 → 다시 → 선택 → 재방문), 대화 중 버튼 차단
//  - 전투 장소: 전투 화면이 열리고 괴이가 조우 목록(또는 고정 괴이)에서 나오는지 확인 후 도망 처리로 복귀
//  - 뒤로가기·X, 마지막에 첫 장소 재방문 대사로 상태 유지 확인
// 한 단계 = 행동(이번 틱) → 확인(다음 틱, UI가 한 번 그려진 뒤). 15프레임마다 한 틱.
// 결과: Console + SessionState "it_log" / "it_done"
public static class IntegrationTestRunner
{
    const string IA = "Canvas/PlaceInteriorPanel/InteractionArea/Interaction_";
    const string ENTER = "Canvas/LocationInfoPanel/EnterButton";
    const string EXIT = "Canvas/PlaceInteriorPanel/ExitButton";
    const string BACK = "Canvas/BackToRegionButton";
    const string CLOSEX = "Canvas/LocationInfoPanel/CloseButton";
    const string CLOSE_D = "Canvas/PlaceInteriorPanel/DialoguePanel/CloseDialogueButton";
    const string CHA = "Canvas/PlaceInteriorPanel/DialoguePanel/ChoicePanel/ChoiceAButton";
    const string CHB = "Canvas/PlaceInteriorPanel/DialoguePanel/ChoicePanel/ChoiceBButton";
    const BindingFlags NonPublic = BindingFlags.NonPublic | BindingFlags.Instance;

    class Step
    {
        public string kind, target, expect;
        public Step(string k, string t, string e) { kind = k; target = t; expect = e; }
    }

    static List<Step> steps;
    static Dictionary<string, PlaceData> places;
    static RegionData region;
    static MapView mapView;
    static LocationSelector selector;
    static BattleController battle;
    static Transform canvas;
    static StringBuilder log;
    static int index, pass, fail, nextFrame;
    static bool acted;
    static string actNote;

    [MenuItem("요원/(개발) 통합 테스트 실행")]
    public static string Run()
    {
        if (!EditorApplication.isPlaying)
        {
            Debug.LogWarning("통합 테스트는 Play 모드에서 실행하세요.");
            return "not playing";
        }

        mapView = UnityEngine.Object.FindAnyObjectByType<MapView>();
        selector = UnityEngine.Object.FindAnyObjectByType<LocationSelector>();
        battle = selector.battleController;
        canvas = GameObject.Find("Canvas").transform;
        region = mapView.startRegion;

        Build();

        log = new StringBuilder();
        index = pass = fail = 0;
        acted = false;
        nextFrame = Time.frameCount + 10;
        SessionState.SetBool("it_done", false);
        SessionState.SetString("it_log", "");
        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
        return "통합 테스트 시작: " + steps.Count + "단계";
    }

    // ---------- 단계 만들기 ----------

    static void Add(string k, string t, string e) { steps.Add(new Step(k, t, e)); }

    static void Build()
    {
        steps = new List<Step>();
        places = new Dictionary<string, PlaceData>();

        Add("noop", "", "map=region,markers=" + region.areas.Count + ",!back,!info");

        foreach (var ae in region.areas)
        {
            var area = ae.area;
            bool infoOpen = false;   // 이 구역에서 지금 정보창이 열려 있는지 (마지막 장소에 따라 다름)

            Add("sel", "area:" + area.areaId, "info,outline,type=" + DisplayNames.AreaTypeLine(area.areaType) + ",enterImg");
            Add("click", ENTER, "map=" + area.areaId + ",markers=" + area.places.Count + ",back,!info,camReset");

            for (int pi = 0; pi < area.places.Count; pi++)
            {
                var p = area.places[pi].place;
                places[p.locationId] = p;
                Add("sel", "place:" + p.locationId, "info,outline,colorKept,type=" + DisplayNames.PlaceTypeLine(p.placeType));

                if (p.battle.mode != BattleMode.None)
                {
                    // 전투 장소: 전투 화면 → 도망 처리로 복귀 (전투 뒤에는 정보창이 닫힌 채 구역 맵)
                    Add("click", ENTER, "battle,!interior,monsterOk:" + p.locationId + ":" + area.areaId + ",battleBlocksMap");
                    Add("battleExit", "", "!battle,map=" + area.areaId + ",!info,!interior");
                    infoOpen = false;
                    continue;
                }

                Add("click", ENTER, "interior,!battle,result=,buttons=" + p.interactions.Count + ",bgOpaque,backCovered");

                foreach (var it in p.interactions)
                {
                    var btn = IA + it.interactionId;
                    var key = p.locationId + ":" + it.interactionId;

                    if (it.type == InteractionType.Investigate)
                    {
                        Add("click", btn, "resultIs:" + key + (it.revealsMonster != null ? ",infoFlag:" + it.revealsMonster.monsterId : ""));
                        continue;
                    }

                    int cc = Mathf.Min(it.talk.choices.Count, 2);
                    Add("click", btn, "dialogue,line:first:" + key + ",choiceBtns=" + cc + ",allBlocked");
                    Add("click", CLOSE_D, "!dialogue,!chosen:" + key);
                    Add("click", btn, "dialogue,line:first:" + key);

                    if (cc > 0)
                    {
                        int k = (pi % 2 == 1 && cc > 1) ? 1 : 0;
                        Add("click", k == 0 ? CHA : CHB, "!dialogue,chosen:" + key + ",pick:" + key + ":" + k + ",resultChoice:" + key + ":" + k);
                        Add("click", btn, "dialogue,!choices,line:repeat:" + key);
                        Add("click", CLOSE_D, "!dialogue");
                    }
                }

                Add("click", EXIT, "info,!interior,map=" + area.areaId);
                infoOpen = true;
            }

            // 정보창이 열려 있을 때만 X로 닫는다 (전투 장소로 끝났으면 이미 닫혀 있음)
            if (infoOpen)
            {
                Add("click", CLOSEX, "!info,map=" + area.areaId);
            }
            else
            {
                Add("noop", "", "!info,map=" + area.areaId);
            }

            Add("click", BACK, "map=region,markers=" + region.areas.Count + ",!back,!info");
        }

        // 모든 구역을 돈 뒤 첫 장소 재방문: 대화 상태 유지
        var firstArea = region.areas[0].area;
        var firstPlace = firstArea.places[0].place;
        var talk = firstPlace.interactions.Find(x => x.type == InteractionType.Talk);
        Add("sel", "area:" + firstArea.areaId, "info");
        Add("click", ENTER, "map=" + firstArea.areaId);
        Add("sel", "place:" + firstPlace.locationId, "info");
        Add("click", ENTER, "interior");

        if (talk != null)
        {
            Add("click", IA + talk.interactionId, "dialogue,!choices,line:repeat:" + firstPlace.locationId + ":" + talk.interactionId);
            Add("click", CLOSE_D, "!dialogue");
        }

        Add("click", EXIT, "info");
        Add("click", CLOSEX, "!info");
        Add("click", BACK, "map=region,!back");
    }

    // ---------- 실행 ----------

    static void Tick()
    {
        if (!EditorApplication.isPlaying)
        {
            EditorApplication.update -= Tick;
            return;
        }

        if (Time.frameCount < nextFrame) return;
        nextFrame = Time.frameCount + 15;

        if (index >= steps.Count)
        {
            EditorApplication.update -= Tick;
            log.Append("DONE total=" + steps.Count + " pass=" + pass + " fail=" + fail + "\n");
            SessionState.SetString("it_log", log.ToString());
            SessionState.SetBool("it_done", true);
            Debug.Log("[통합 테스트] " + steps.Count + "단계 중 통과 " + pass + ", 실패 " + fail + (fail > 0 ? "\n" + log : ""));
            return;
        }

        var s = steps[index];

        if (!acted)
        {
            actNote = Act(s);
            acted = true;
            return;
        }

        acted = false;
        string fails = actNote + Check(s);

        if (fails == "") pass++;
        else
        {
            fail++;
            log.Append("#" + index + " " + s.kind + " " + s.target.Replace("Canvas/", "") + " => FAIL:" + fails + "\n");
        }

        index++;
        SessionState.SetString("it_log", log + "(running " + index + "/" + steps.Count + " pass=" + pass + " fail=" + fail + ")");
    }

    static string Act(Step s)
    {
        try
        {
            if (s.kind == "sel")
            {
                var id = s.target.Substring(s.target.IndexOf(':') + 1);
                var m = GameObject.Find("MapView/Marker_" + id);
                if (m == null) return " markerMissing";
                typeof(LocationSelector).GetMethod("SelectMarker", NonPublic).Invoke(selector, new object[] { m.GetComponent<MapMarker>() });
            }
            else if (s.kind == "click")
            {
                var go = GameObject.Find(s.target);
                if (go == null) return " targetInactive";
                var sp = Center(go);
                var h = HandlerAt(sp);
                if (h != go) return " clickBlockedBy:" + (h == null ? "none" : h.name);
                ExecuteEvents.Execute(go, new PointerEventData(EventSystem.current) { position = sp }, ExecuteEvents.pointerClickHandler);
            }
            else if (s.kind == "battleExit")
            {
                // 테스트 지름길: 도망 성공 처리 후 결과 확인
                typeof(BattleEngine).GetProperty("Outcome").GetSetMethod(true).Invoke(battle.Engine, new object[] { BattleOutcome.Fled });
                battle.OnResultOk();
            }
        }
        catch (Exception ex)
        {
            return " EXC:" + ex.Message;
        }

        return "";
    }

    static string Check(Step s)
    {
        string fails = "";

        try
        {
            var selM = typeof(LocationSelector).GetField("selectedMarker", NonPublic).GetValue(selector) as MapMarker;
            int markers = 0;
            foreach (Transform t in mapView.transform) if (t.gameObject.activeSelf && t.GetComponent<MapMarker>() != null) markers++;
            string mapNow = mapView.CurrentArea == null ? "region" : mapView.CurrentArea.areaId;
            string dlg = canvas.Find("PlaceInteriorPanel/DialoguePanel/DialogueText").GetComponent<TMP_Text>().text;
            string res = canvas.Find("PlaceInteriorPanel/InteractionResultText").GetComponent<TMP_Text>().text;
            int btns = 0;
            foreach (Transform t in canvas.Find("PlaceInteriorPanel/InteractionArea")) if (t.gameObject.activeSelf && t.name.StartsWith("Interaction_")) btns++;
            var g = GameStateManager.Instance;

            foreach (var e0 in s.expect.Split(','))
            {
                if (e0 == "") continue;
                bool neg = e0.StartsWith("!");
                var e = neg ? e0.Substring(1) : e0;
                int eq = e.IndexOf('=');
                string key = eq >= 0 ? e.Substring(0, eq) : e;
                string val = eq >= 0 ? e.Substring(eq + 1) : "";
                bool v;

                if (e == "info") v = Active("LocationInfoPanel");
                else if (e == "interior") v = Active("PlaceInteriorPanel");
                else if (e == "battle") v = battle.IsActive;
                else if (e == "battleBlocksMap") { var b = canvas.Find("BackToRegionButton").gameObject; v = HandlerAt(Center(b)) != b; }
                else if (e.StartsWith("monsterOk:"))
                {
                    var kv = e.Substring(10).Split(':');
                    var pl = places[kv[0]];
                    var ar = region.areas.Find(x => x.area.areaId == kv[1]).area;
                    var mon = battle.Engine.monster;
                    v = pl.battle.mode == BattleMode.Story ? mon == pl.battle.fixedMonster : ar.encounters.Exists(x => x.monster == mon);
                }
                else if (e == "dialogue") v = Active("PlaceInteriorPanel/DialoguePanel");
                else if (e == "choices") v = Active("PlaceInteriorPanel/DialoguePanel/ChoicePanel");
                else if (e == "back") v = Active("BackToRegionButton");
                else if (e == "outline") v = selM != null && selM.selectionOutline.activeSelf;
                else if (e == "colorKept") v = selM != null && selM.GetComponent<SpriteRenderer>().color == mapView.ColorFor(selM.Place.placeType);
                else if (e == "camReset") v = Mathf.Abs(Camera.main.orthographicSize - 5f) < 0.01f;
                else if (e == "result=") v = res == "";
                else if (e == "bgOpaque") v = canvas.Find("PlaceInteriorPanel").GetComponent<Image>().color.a >= 0.999f;
                else if (e == "backCovered") { var b = canvas.Find("BackToRegionButton").gameObject; v = !b.activeInHierarchy || HandlerAt(Center(b)) != b; }
                else if (e == "enterImg")
                {
                    var en = canvas.Find("LocationInfoPanel/EnterButton");
                    var img = en.GetComponent<Image>();
                    var tx = en.GetComponentInChildren<TMP_Text>();
                    v = img != null && img.enabled && tx != null && tx.text == "진입하기" && tx.color.r < 0.5f;
                }
                else if (e == "allBlocked")
                {
                    v = true;
                    foreach (Transform t in canvas.Find("PlaceInteriorPanel/InteractionArea"))
                        if (t.gameObject.activeSelf && t.name.StartsWith("Interaction_") && HandlerAt(Center(t.gameObject)) == t.gameObject) v = false;
                }
                else if (key == "map") v = mapNow == val;
                else if (key == "markers") v = markers == int.Parse(val);
                else if (key == "buttons") v = btns == int.Parse(val);
                else if (key == "type") v = canvas.Find("LocationInfoPanel/LocationTypeText").GetComponent<TMP_Text>().text == val;
                else if (key == "choiceBtns")
                {
                    int n = 0;
                    if (Active("PlaceInteriorPanel/DialoguePanel/ChoicePanel"))
                    {
                        if (Active("PlaceInteriorPanel/DialoguePanel/ChoicePanel/ChoiceAButton")) n++;
                        if (Active("PlaceInteriorPanel/DialoguePanel/ChoicePanel/ChoiceBButton")) n++;
                    }
                    v = n == int.Parse(val);
                }
                else if (e.StartsWith("resultIs:")) v = res == FindIt(e.Substring(9)).resultText;
                else if (e.StartsWith("infoFlag:")) v = g.HasFlag(StateKeys.MonsterInfo(e.Substring(9)));
                else if (e.StartsWith("line:first:")) v = dlg == FindIt(e.Substring(11)).talk.firstLine;
                else if (e.StartsWith("line:repeat:")) v = dlg == FindIt(e.Substring(12)).talk.repeatLine;
                else if (e.StartsWith("chosen:")) { var kv = e.Substring(7).Split(':'); v = g.HasFlag(StateKeys.TalkChosen(kv[0], kv[1])); }
                else if (e.StartsWith("pick:")) { var kv = e.Substring(5).Split(':'); v = g.HasFlag(StateKeys.TalkChoice(kv[0], kv[1], int.Parse(kv[2]))); }
                else if (e.StartsWith("resultChoice:")) { var kv = e.Substring(13).Split(':'); v = res == FindIt(kv[0] + ":" + kv[1]).talk.choices[int.Parse(kv[2])].result; }
                else { fails += " ?" + e; continue; }

                if (v == neg) fails += " " + e0;
            }
        }
        catch (Exception ex)
        {
            fails += " EXC:" + ex.GetType().Name + ":" + ex.Message;
        }

        return fails;
    }

    // ---------- 도우미 ----------

    static bool Active(string path)
    {
        var t = canvas.Find(path);
        return t != null && t.gameObject.activeInHierarchy;
    }

    static PlaceInteraction FindIt(string key)
    {
        var kv = key.Split(':');
        return places[kv[0]].interactions.Find(x => x.interactionId == kv[1]);
    }

    static Vector2 Center(GameObject go)
    {
        var rt = (RectTransform)go.transform;
        return RectTransformUtility.WorldToScreenPoint(null, rt.TransformPoint(rt.rect.center));
    }

    // 실제 클릭처럼: 맨 위에 맞은 오브젝트에서 클릭 처리기를 찾음
    static GameObject HandlerAt(Vector2 screenPos)
    {
        var es = EventSystem.current;
        var hits = new List<RaycastResult>();
        es.RaycastAll(new PointerEventData(es) { position = screenPos }, hits);
        return hits.Count == 0 ? null : ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject);
    }
}

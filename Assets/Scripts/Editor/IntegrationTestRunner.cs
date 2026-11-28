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
    // [C-2] 상점·병원
    const string SHOP_EXIT = "Canvas/ShopPanel/Window/ExitButton";
    const string SHOP_ROW = "Canvas/ShopPanel/Window/Row";
    const string CONFIRM_OK = "Canvas/ShopPanel/Window/ConfirmPopup/ConfirmBox/ConfirmOkButton";
    const string CONFIRM_ALT = "Canvas/ShopPanel/Window/ConfirmPopup/ConfirmBox/ConfirmAltButton";
    const string HOSP_HEAL = "Canvas/HospitalPanel/Window/HealButton";
    const string HOSP_CLOSE = "Canvas/HospitalPanel/Window/CloseButton";
    const string RESPAWN_NOTICE = "[이 병원이 복귀 지점이 되었다]";

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
    static bool botRunning;
    static string actNote;

    // true면 전투 장소를 끝까지 싸운다 (B단계 통합 테스트)
    static bool fullBattles;

    // [C-2] 경제 흐름 확인용 기준값 (mark 단계에서 저장)
    static int markGold, markFood, markHp;
    static ShopController shopUi;
    static HomeController homeUi;
    // [C-5] 훈련·가장자리 연출
    const string TRAIN = "Canvas/TrainingPanel/Window/";
    static TrainingController trainUi;
    static BattleScreenEffects edgeFx;
    static bool trainRunning;
    static PickController pickUi;
    const string HOME = "Canvas/HomePanel/Window/";
    static EconomySettings eco;

    [MenuItem("요원/(개발) 통합 테스트 실행")]
    public static string Run()
    {
        fullBattles = false;
        return Start();
    }

    // [B-4] B단계 통합 테스트: A단계 흐름 + 모든 전투(일반·스토리·보스)를 버튼으로 끝까지 + 만화·패배·도망
    [MenuItem("요원/(개발) B단계 통합 테스트 실행")]
    public static string RunFull()
    {
        fullBattles = true;
        return Start();
    }

    static string Start()
    {
        if (!EditorApplication.isPlaying)
        {
            Debug.LogWarning("통합 테스트는 Play 모드에서 실행하세요.");
            return "not playing";
        }

        mapView = UnityEngine.Object.FindAnyObjectByType<MapView>();
        selector = UnityEngine.Object.FindAnyObjectByType<LocationSelector>();
        battle = selector.battleController;
        shopUi = UnityEngine.Object.FindAnyObjectByType<ShopController>(FindObjectsInactive.Include);
        eco = battle.economy;
        homeUi = UnityEngine.Object.FindAnyObjectByType<HomeController>(FindObjectsInactive.Include);
        trainUi = UnityEngine.Object.FindAnyObjectByType<TrainingController>(FindObjectsInactive.Include);
        edgeFx = battle.GetComponent<BattleScreenEffects>();
        pickUi = UnityEngine.Object.FindAnyObjectByType<PickController>(FindObjectsInactive.Include);
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
            // [C-4] 매력 조건 등으로 숨은 장소는 지도에 없음
            var visiblePlaces = area.places.FindAll(e => !ConditionChecker.Hidden(GameStateManager.Instance.State, e.condition));
            foreach (var hp in area.places) if (!visiblePlaces.Contains(hp)) places[hp.place.locationId] = hp.place;
            Add("click", ENTER, "map=" + area.areaId + ",markers=" + visiblePlaces.Count + ",back,!info,camReset");

            for (int pi = 0; pi < visiblePlaces.Count; pi++)
            {
                var p = visiblePlaces[pi].place;
                places[p.locationId] = p;
                Add("sel", "place:" + p.locationId, "info,outline,colorKept,type=" + DisplayNames.PlaceTypeLine(p.placeType));

                if (p.battle.mode != BattleMode.None)
                {
                    if (fullBattles)
                    {
                        // 끝까지 싸움 (스토리: 만화 → 전투, 보스: 페이즈 만화 2번, 첫 클리어 플래그)
                        bool story = p.battle.mode == BattleMode.Story;
                        bool boss = story && p.battle.fixedMonster != null && p.battle.fixedMonster.IsBoss;
                        Add("prepBattle", boss ? "boss" : "", "");
                        Add("click", ENTER, story ? "comic" : "battle,!interior,monsterOk:" + p.locationId + ":" + area.areaId);
                        Add("battleFull", p.locationId, "won,!battle,map=" + area.areaId + ",!info,!interior,rewarded:" + (story ? "StoryFirst" : "Normal") +
                            (story ? ",introComicFirst,cleared:" + p.locationId : "") +
                            (boss ? ",phaseComics=" + p.battle.fixedMonster.phases.Count + ",bossFlag:" + p.battle.fixedMonster.monsterId : ""));
                    }
                    else
                    {
                        // 전투 장소: 전투 화면 → 도망 처리로 복귀 (전투 뒤에는 정보창이 닫힌 채 구역 맵)
                        Add("click", ENTER, "battle,!interior,monsterOk:" + p.locationId + ":" + area.areaId + ",battleBlocksMap");
                        Add("battleExit", "", "!battle,map=" + area.areaId + ",!info,!interior");
                    }

                    infoOpen = false;
                    continue;
                }

                // [C-2] 조건으로 숨은 상호작용은 버튼이 없어야 함. 병원은 들어가면 복귀 지점 안내
                var gs = GameStateManager.Instance.State;
                var shown = p.interactions.FindAll(x => !ConditionChecker.Hidden(gs, x.condition));
                string hiddenChecks = "";
                foreach (var x in p.interactions) if (!shown.Contains(x)) hiddenChecks += ",!btn:" + x.interactionId;
                Add("click", ENTER, "interior,!battle," + (p.IsHospital ? "notice,respawnIs:" + p.locationId : "result=") +
                    ",buttons=" + shown.Count + hiddenChecks + ",bgOpaque,backCovered");

                foreach (var it in shown)
                {
                    var btn = IA + it.interactionId;
                    var key = p.locationId + ":" + it.interactionId;

                    if (it.type == InteractionType.Investigate)
                    {
                        Add("click", btn, "resultIs:" + key + (it.revealsMonster != null ? ",infoFlag:" + it.revealsMonster.monsterId : ""));
                        continue;
                    }

                    if (it.type == InteractionType.Shop)
                    {
                        int rows = Mathf.Min(4, it.shop.entries.FindAll(x => x.item != null && !ConditionChecker.Hidden(gs, x.condition)).Count);
                        Add("click", btn, "shopOpen,tab=Buy,shopRows=" + rows + (it.shop.canSell ? ",sellTab" : ",!sellTab") + ",allBlocked");
                        Add("click", SHOP_EXIT, "!shopOpen,interior");
                        continue;
                    }

                    if (it.type == InteractionType.Hospital)
                    {
                        Add("click", btn, "hospOpen,allBlocked");
                        Add("click", HOSP_CLOSE, "!hospOpen,interior");
                        continue;
                    }

                    if (it.type == InteractionType.Train)
                    {
                        Add("click", btn, "trainOpen,allBlocked,trainStat=" + it.training.stat);
                        Add("click", TRAIN + "CloseButton", "!trainOpen,interior");
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

        if (!fullBattles) return;

        // ---- 만화 뷰어 단독: 테스트 만화 1페이지 4칸 (칸마다 다른 등장 효과), 끝까지 / 건너뛰기 ----
        Add("comicPlay", "Assets/Data/Battle/Comics/Comic_Test.asset", "comicOpen,comicPanel=1,placeholder:1:1");
        Add("comicAdvance", "", "comicPanel=2,placeholder:1:2");
        Add("comicAdvance", "", "comicPanel=3,placeholder:1:3");
        Add("comicAdvance", "", "comicPanel=4,placeholder:1:4,lines=4");
        Add("comicAdvance", "", "!comicOpen,comicDone");
        Add("comicPlay", "Assets/Data/Battle/Comics/Comic_Test.asset", "comicOpen");
        Add("click", "Canvas/ComicPanel/SkipButton", "!comicOpen,comicDone");

        // ---- 패배: 체력 1로 일반 전투 → 복귀 장소 ----
        var fightArea = region.areas.Find(a => a.area.places.Exists(e => e.place.battle.mode == BattleMode.Random));

        if (fightArea != null)
        {
            var fightPlace = fightArea.area.places.Find(e => e.place.battle.mode == BattleMode.Random).place;
            Add("sel", "area:" + fightArea.area.areaId, "info");
            Add("click", ENTER, "map=" + fightArea.area.areaId);
            Add("sel", "place:" + fightPlace.locationId, "info");
            Add("prepBattle", "lose", "");
            Add("click", ENTER, "battle");
            Add("battleFull", fightPlace.locationId, "lost,!battle,respawned");

            // ---- 도망: 성공할 때까지 도망 ----
            // 패배 후 복귀는 지도 선택 없이 장소 내부로 들어오므로, 나가면 정보창 없이 구역 맵
            Add("click", EXIT, "!info,!interior");
            Add("click", BACK, "map=region");
            Add("sel", "area:" + fightArea.area.areaId, "info");
            Add("click", ENTER, "map=" + fightArea.area.areaId);
            Add("sel", "place:" + fightPlace.locationId, "info");
            Add("prepBattle", "flee", "");
            Add("click", ENTER, "battle");
            Add("battleFull", fightPlace.locationId, "fled,!battle,map=" + fightArea.area.areaId);

            BuildEconomyFlow(fightArea.area, fightPlace);
        }
    }

    // ---------- [B-4] 전투 봇 (버튼으로 끝까지) ----------

    static string botMode = "";          // "" = 이기기, "lose" = 지기, "flee" = 도망
    static string botWant;               // 특수 행동 창에서 누를 항목
    static int botTicks, botComics, botComicsBeforeTurn1, botTaunts, botCounters, botFoods;
    static string botResult;             // 결과 창 제목
    static string botDetail;
    static bool botComicWasOpen;
    static bool comicCallback;

    static bool BotTick()
    {
        botTicks++;
        if (botTicks > 600) { botResult = "TIMEOUT"; return true; }

        var viewer = ComicViewer.Instance;

        if (viewer != null && viewer.IsPlaying)
        {
            if (!botComicWasOpen)
            {
                botComics++;
                if (battle.Engine == null || battle.Engine.Turn == 0) botComicsBeforeTurn1++;
            }

            botComicWasOpen = true;
            ClickPath("Canvas/ComicPanel");   // 다음 칸
            return false;
        }

        botComicWasOpen = false;

        if (Active("BattlePanel/ResultPopup"))
        {
            botResult = canvas.Find("BattlePanel/ResultPopup/ResultBox/ResultTitleText").GetComponent<TMP_Text>().text;
            botDetail = canvas.Find("BattlePanel/ResultPopup/ResultBox/ResultDetailText").GetComponent<TMP_Text>().text;
            ClickPath("Canvas/BattlePanel/ResultPopup/ResultBox/ResultOkButton");
            return true;
        }

        var e = battle.Engine;
        if (e == null || battle.WaitingForComic) return false;

        if (Active("BattlePanel/AttackArea"))
        {
            if (Active("BattlePanel/AttackArea/ConfirmAttackButton")) ClickPath("Canvas/BattlePanel/AttackArea/ConfirmAttackButton");
            else if (Active("BattlePanel/AttackArea/RollButton")) ClickPath("Canvas/BattlePanel/AttackArea/RollButton");
            return false;   // 굴리는 중이면 기다림
        }

        if (Active("BattlePanel/SpecialPopup"))
        {
            foreach (Transform t in canvas.Find("BattlePanel/SpecialPopup/SpecialItems"))
            {
                if (t.gameObject.activeSelf && t.GetComponentInChildren<TMP_Text>().text.StartsWith(botWant))
                {
                    ClickPath("Canvas/BattlePanel/SpecialPopup/SpecialItems/" + t.name);
                    return false;
                }
            }

            ClickPath("Canvas/BattlePanel/SpecialPopup/SpecialNextButton");
            return false;
        }

        if (Active("BattlePanel/FleePopup"))
        {
            ClickPath("Canvas/BattlePanel/FleePopup/FleeConfirmButton");
            return false;
        }

        if (!Active("BattlePanel/ActionButtons")) return false;

        if (botMode == "flee")
        {
            ClickPath("Canvas/BattlePanel/ActionButtons/FleeButton");
            return false;
        }

        if (botMode == "lose")
        {
            ClickPath("Canvas/BattlePanel/ActionButtons/AttackButton");
            return false;
        }

        // 이기는 전략: 보스 1페이즈 도발(턴 안 씀) → 예고 대응 → 공격
        var intent = e.Intent;
        bool tauntActive = e.monsterEffects.Exists(x => x.data == e.settings.tauntEffect);

        if (e.monster.IsBoss && e.PhaseIndex < 0 && !tauntActive && e.CanSpecial(SpecialAction.Taunt))
        {
            botWant = DisplayNames.Special(SpecialAction.Taunt);
            botTaunts++;
        }
        else if (e.player.currentHp <= e.player.maxHp * 0.35f && e.CanSpecial(SpecialAction.EatFood))
        {
            // [C-1] 체력 35% 이하면 음식 (시뮬레이터와 같은 기준)
            botWant = DisplayNames.Special(SpecialAction.EatFood);
            botFoods++;
        }
        else if (intent != null && intent.kind == MonsterActionKind.Reflect)
        {
            botWant = DisplayNames.Special(SpecialAction.Wait);
            botCounters++;
        }
        else if (intent != null && intent.kind == MonsterActionKind.Debuff && intent.effect != null && intent.effect.blockedBy != SpecialAction.None)
        {
            botWant = DisplayNames.Special(intent.effect.blockedBy);
            botCounters++;
        }
        else
        {
            ClickPath("Canvas/BattlePanel/ActionButtons/AttackButton");
            return false;
        }

        ClickPath("Canvas/BattlePanel/ActionButtons/SpecialButton");
        return false;
    }

    static void ClickPath(string path)
    {
        var go = GameObject.Find(path);
        if (go == null) return;
        var sp = Center(go);
        var es = EventSystem.current;
        var ped = new PointerEventData(es) { position = sp };
        var hits = new List<RaycastResult>();
        es.RaycastAll(ped, hits);
        if (hits.Count > 0) ExecuteEvents.ExecuteHierarchy(hits[0].gameObject, ped, ExecuteEvents.pointerClickHandler);
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

        // [B-4] 전투를 끝까지: 여러 틱에 걸쳐 봇이 버튼을 누르고, 결과 창 확인까지 마치면 확인 단계로
        // [C-5] 훈련 미니게임: 여러 틱에 걸쳐 화면을 클릭해 결과가 나올 때까지
        if (s.kind == "trainPlay")
        {
            if (!acted)
            {
                acted = true;
                trainRunning = true;
                actNote = "";
                return;
            }

            if (trainRunning)
            {
                if (!TrainTick(s.target)) return;
                trainRunning = false;
                return;   // 다음 틱에 확인
            }
        }

        if (s.kind == "battleFull")
        {
            if (!acted)
            {
                botTicks = botComics = botComicsBeforeTurn1 = botTaunts = botCounters = botFoods = 0;
                botResult = null;
                botDetail = null;
                botComicWasOpen = false;
                acted = true;
                botRunning = true;
                actNote = "";
                return;
            }

            if (botRunning)
            {
                if (!BotTick()) return;
                botRunning = false;
                return;   // 다음 틱에 확인
            }

            // [C-4] 전투 뒤 특성 선택 창이 자동으로 떴는데 이 단계가 기대하지 않으면 "나중에" (빨간 점으로 남음)
            if (pickUi != null && pickUi.IsOpen && !s.expect.Contains("pickOpen"))
            {
                log.Append("  (전투 뒤 " + pickUi.CurrentMode + " 선택 창 → 나중에)\n");
                pickUi.Close();
            }
        }

        if (!acted)
        {
            actNote = Act(s);
            acted = true;
            if (s.kind == "wait") nextFrame = Time.frameCount + int.Parse(s.target);   // [C-5] 연출이 서서히 바뀌는 시간
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

        if (s.kind == "battleFull")
        {
            // 전투 기록 (통과해도 남김)
            log.Append("  [전투 " + s.target + "] 결과=" + botResult + " 틱=" + botTicks + " 만화=" + botComics + "(전투 전 " + botComicsBeforeTurn1 +
                       ") 도발=" + botTaunts + " 대응=" + botCounters + " 음식=" + botFoods +
                       " 보상=" + (battle.LastReward != null ? battle.LastReward.gold + "G" : "-") + "\n");
        }

        index++;
        SessionState.SetString("it_log", log + "(running " + index + "/" + steps.Count + " pass=" + pass + " fail=" + fail + ")");
    }

    // ---------- [C-2] 경제 흐름: 전투 → 골드 → 병원 치료 → 장비 구매·장착 → 강화 → 판매 → 히든 상점 → 편의점 ----------
    // 지금 위치: fightArea 구역 맵 (선택 없음)
    static void BuildEconomyFlow(AreaData fightArea, PlaceData fightPlace)
    {
        AreaData hospArea = null, shopArea = null, storeArea = null;
        PlaceData hosp = null, shopPlace = null, store = null;

        foreach (var ae in region.areas)
        {
            foreach (var pe in ae.area.places)
            {
                var pl = pe.place;
                if (pl.IsHospital && hosp == null) { hosp = pl; hospArea = ae.area; }
                foreach (var it in pl.interactions)
                {
                    if (it.type != InteractionType.Shop || it.shop == null) continue;
                    if (it.shop.canEnhance && shopPlace == null) { shopPlace = pl; shopArea = ae.area; }
                    if (!it.shop.canSell && it.shop.entries.Exists(x => x.item != null && x.item.type == ItemType.Consumable) && store == null) { store = pl; storeArea = ae.area; }
                }
            }
        }

        if (hosp == null || shopPlace == null || store == null)
        {
            Add("noop", "", "economyPlacesMissing");
            return;
        }

        var equipIt = shopPlace.interactions.Find(x => x.type == InteractionType.Shop && x.shop.canEnhance);
        var hiddenIt = shopPlace.interactions.Find(x => x.type == InteractionType.Shop && !x.condition.IsEmpty);
        var advWeapon = equipIt.shop.entries.Find(x => x.item.type == ItemType.Weapon && x.item.grade == ItemGrade.Advanced).item;
        var commonWeapon = equipIt.shop.entries.Find(x => x.item.type == ItemType.Weapon && x.item.grade == ItemGrade.Common).item;
        var storeIt = store.interactions.Find(x => x.type == InteractionType.Shop);
        var food = storeIt.shop.entries.Find(x => x.item.type == ItemType.Consumable && x.item.healHp > 0).item;
        int baseAtk = battle.settings.playerBase.attack;
        int heal = eco.HospitalCost(new PlayerState());
        string cur = fightArea.areaId;

        // 1) 새 주인공(시작 장비·골드 0)으로 일반 전투 승리 → 골드
        Add("newPlayer", "", "gold=0,weaponIs:" + commonWeapon.itemId);
        Add("mark", "", "");
        Add("sel", "place:" + fightPlace.locationId, "info");
        Add("prepBattle", "", "");
        Add("click", ENTER, "battle");
        Add("battleFull", fightPlace.locationId, "won,rewarded:Normal,goldMore,!battle,map=" + cur);

        // 2) 다친 상태(테스트 지름길)로 병원 → 복귀 지점 등록 → 전체 치료 (직급별 고정비). 다시 누르면 치료할 곳 없음(돈 안 냄)
        Add("hurt", "", "");
        Add("mark", "", "");
        GoToArea(ref cur, hospArea);
        Add("sel", "place:" + hosp.locationId, "info");
        Add("click", ENTER, "interior,notice,respawnIs:" + hosp.locationId);
        Add("click", IA + hosp.interactions.Find(x => x.type == InteractionType.Hospital).interactionId, "hospOpen");
        Add("click", HOSP_HEAL, "fullHp,goldDelta=-" + heal + ",hospMsg");
        Add("click", HOSP_HEAL, "goldDelta=-" + heal);
        Add("click", HOSP_CLOSE, "!hospOpen");
        Add("click", EXIT, "info,!interior");

        // 3) 특수 상점: 히든 상점 버튼 없음 → 고급 무기 "구매 후 장착"
        Add("grant", "", "");   // 테스트 지름길: 골드 1,000 + 강화석 3개로 맞춤 (일반 전투 약 25회 분)
        GoToArea(ref cur, shopArea);
        Add("sel", "place:" + shopPlace.locationId, "info");
        Add("click", ENTER, "interior" + (hiddenIt != null ? ",!btn:" + hiddenIt.interactionId : ""));
        Add("click", IA + equipIt.interactionId, "shopOpen,tab=Buy,noRare,sellTab");
        Add("mark", "", "");
        Add("clickRow", "item:" + advWeapon.itemId, "confirm");
        Add("click", CONFIRM_ALT, "!confirm,weaponIs:" + advWeapon.itemId + ",attack=" + (baseAtk + advWeapon.attackBonus) +
            ",goldDelta=-" + advWeapon.price + ",skills=" + advWeapon.skills.Count + ",shopMsg");

        // 4) 강화 탭: 장착 무기 +1 (골드 + 강화석)
        Add("click", "Canvas/ShopPanel/Window/EnhanceTab", "tab=Enhance");
        Add("mark", "", "");
        Add("clickRow", "equippedWeapon", "confirm");
        Add("click", CONFIRM_OK, "!confirm,weaponEnhance=1,attack=" + (baseAtk + advWeapon.attackBonus + advWeapon.attackPerEnhance) +
            ",goldDelta=-" + eco.enhanceSteps[0].gold + ",stones=" + (3 - eco.enhanceSteps[0].materials) + ",shopMsg");

        // 5) 판매 탭: 장착 중 무기는 판매 불가, 쓰던 일반 무기 판매
        Add("click", "Canvas/ShopPanel/Window/SellTab", "tab=Sell,equippedLocked");
        Add("mark", "", "");
        Add("clickRow", "item:" + commonWeapon.itemId, "confirm");
        Add("click", CONFIRM_OK, "!confirm,goldDelta=" + eco.SellPrice(commonWeapon) + ",!owns:" + commonWeapon.itemId + ",shopMsg");
        Add("click", SHOP_EXIT, "!shopOpen");

        // 6) 히든 상점: 매력 판정은 C-4 → 지금은 구조만 (직접 열어 희귀 장비 목록 확인)
        if (hiddenIt != null)
        {
            Add("openShop", shopPlace.locationId + ":" + hiddenIt.interactionId, "shopOpen,allRare,shopRows=" + hiddenIt.shop.entries.Count + ",!sellTab");
            Add("click", SHOP_EXIT, "!shopOpen");
        }

        Add("click", EXIT, "info,!interior");

        // 7) 편의점: 음식 구매 (확인 없이 바로)
        GoToArea(ref cur, storeArea);
        Add("sel", "place:" + store.locationId, "info");
        Add("click", ENTER, "interior");
        Add("click", IA + storeIt.interactionId, "shopOpen,!sellTab");
        Add("mark", "", "");
        Add("clickRow", "item:" + food.itemId, "foodDelta=1,goldDelta=-" + food.price + ",shopMsg");
        Add("click", SHOP_EXIT, "!shopOpen");
        Add("click", EXIT, "info,!interior");
        Add("click", CLOSEX, "!info");

        // 8) [C-3] 홈 창 (상단 바 프로필): 무기 해제 → 다시 장착, 인벤토리에서 음식 사용
        int atkAdv = baseAtk + advWeapon.attackBonus + advWeapon.attackPerEnhance;
        Add("hurt", "", "");
        Add("click", "Canvas/TopBar/ProfileButton", "homeOpen,homeTab=Stats,!homeReadOnly,attackShown=" + atkAdv);
        Add("click", HOME + "EquipmentTab", "homeTab=Equipment");
        Add("click", HOME + "ListArea/SlotArea/WeaponUnequipButton", "noWeapon,attack=" + baseAtk + ",homeMsg");
        Add("homeRow", "item:" + advWeapon.itemId, "weaponIs:" + advWeapon.itemId + ",attack=" + atkAdv + ",homeMsg");
        Add("click", HOME + "InventoryTab", "homeTab=Inventory");
        Add("mark", "", "");
        Add("homeRow", "item:" + food.itemId, "foodDelta=-1,hpUp,homeMsg");
        Add("click", HOME + "CloseButton", "!homeOpen");

        // 9) [C-3] 전투 중 프로필: 보기만 (장착·사용 불가) → 닫으면 전투 계속
        Add("sel", "place:" + fightPlace.locationId, "info");
        Add("prepBattle", "", "");
        Add("click", ENTER, "battle");
        Add("click", "Canvas/BattlePanel/ProfileButton", "homeOpen,homeReadOnly,homeMsg");
        Add("click", HOME + "EquipmentTab", "homeTab=Equipment,homeRowsLocked");
        Add("click", HOME + "InventoryTab", "homeTab=Inventory,homeRowsLocked");
        Add("click", HOME + "CloseButton", "!homeOpen,battle");
        Add("battleExit", "", "!battle");

        BuildGrowthFlow(fightPlace, shopPlace, equipIt, advWeapon);
    }

    // ---------- [C-4] 성장: 시작 재능 → 레벨 업 → 매력 3 → 히든 장소 드러남·할인 → 숙련도 10 특성 ----------
    // 지금 위치: 도심 구역 맵 (선택 없음), 새 주인공(경제 흐름) — 시작 재능 대기
    static void BuildGrowthFlow(PlaceData fightPlace, PlaceData shopPlace, PlaceInteraction equipIt, ItemData advWeapon)
    {
        PlaceData hidden = null;
        foreach (var ae in region.areas)
            foreach (var pe in ae.area.places)
                if (ConditionChecker.HasCharm(pe.condition) && hidden == null) hidden = pe.place;

        var gs = GameStateManager.Instance.State;

        // 1) 시작 재능: 빨간 점 → 홈 성장 탭 → 재능 고르기 → 카드 4장 중 첫 번째
        Add("noop", "", "profileDot");
        Add("click", "Canvas/TopBar/ProfileButton", "homeOpen");
        Add("click", HOME + "GrowthTab", "homeTab=Growth");
        Add("click", HOME + "GrowthButtons/TalentPickButton", "pickOpen,pickMode=Talent,pickCards=" + Mathf.Min(4, eco.startTalents.Count));
        Add("click", "Canvas/PickPanel/Card0", "!pickOpen,talent:" + eco.startTalents[0].talentId + ",homeOpen");

        // 2) 레벨 업 (테스트 지름길: 경험치 지급) → 스탯 포인트 3 → 매력 3번
        Add("grantLevels", "3", "points=3,profileDot");
        Add("click", HOME + "StatsTab", "homeTab=Stats");
        Add("click", HOME + "StatArea/StatRow4/PlusButton", "charm=1,points=2,discount=1" + (hidden != null ? ",!revealed:" + hidden.locationId : ""));
        Add("click", HOME + "StatArea/StatRow4/PlusButton", "charm=2,points=1");
        Add("click", HOME + "StatArea/StatRow4/PlusButton", "charm=3,points=0,!profileDot" + (hidden != null ? ",revealed:" + hidden.locationId + ",toast" : ""));
        Add("click", HOME + "CloseButton", "!homeOpen");

        // 3) 매력 3으로 드러난 히든 장소
        if (hidden != null)
        {
            Add("noop", "", "marker:" + hidden.locationId);
            Add("sel", "place:" + hidden.locationId, "info");
            Add("click", ENTER, "interior");
            Add("click", EXIT, "info,!interior");
            Add("click", CLOSEX, "!info");
        }

        // 4) 매력 할인: 원가 취소선 + 할인가
        int price = Mathf.RoundToInt(advWeapon.price * (1f - 3 * eco.discountPerCharm));
        Add("sel", "place:" + shopPlace.locationId, "info");
        Add("click", ENTER, "interior");
        Add("click", IA + equipIt.interactionId, "shopOpen,priceShown:" + advWeapon.itemId + ":<s>" + advWeapon.price + "G</s> " + price + "G");
        Add("click", SHOP_EXIT, "!shopOpen");
        Add("click", EXIT, "info,!interior");

        // 5) 무기 숙련도 10 → 전투 뒤 특성 선택 창 (테스트 지름길: 숙련도 9레벨 + 11)
        Add("grantMastery", "", "");
        Add("sel", "place:" + fightPlace.locationId, "info");
        Add("prepBattle", "", "");
        Add("click", ENTER, "battle");
        Add("battleFull", fightPlace.locationId, "!battle,pickOpen,pickMode=Trait,pickCards=2,profileDot");
        Add("click", "Canvas/PickPanel/Card0", "!pickOpen,diceCount=" + (battle.settings.playerBase.diceCount + 1) + ",!profileDot");

        BuildTrainingFlow(fightPlace);
    }

    // ---------- [C-5] 훈련 성공·실패, 전투 가장자리 연출 ----------
    // 지금 위치: 도심 구역 맵 (선택 없음)
    static void BuildTrainingFlow(PlaceData fightPlace)
    {
        PlaceData gym = null;
        PlaceInteraction train = null;

        foreach (var pe in region.areas[0].area.places)
        {
            var t = pe.place.interactions.Find(x => x.type == InteractionType.Train);
            if (t != null && gym == null) { gym = pe.place; train = t; }
        }

        if (gym == null)
        {
            Add("noop", "", "trainingPlaceMissing");
            return;
        }

        var d = train.training;
        string stat = d.stat.ToString();

        // 1) 성공: 비용을 내고, 목표 구간에 맞춰 멈춤 → 스탯 +1, 다음 비용 증가·구간 좁아짐
        Add("grant", "", "");
        Add("sel", "place:" + gym.locationId, "info");
        Add("click", ENTER, "interior");
        Add("click", IA + train.interactionId, "trainOpen,trainCost=" + d.baseCost);
        Add("mark", "", "");
        Add("click", TRAIN + "StartButton", "trainRunning,goldDelta=-" + d.baseCost + ",tapArea");
        Add("trainPlay", "success", "trainResult=Success,statGain:" + stat + "=" + d.gain + ",trainCost=" + (d.baseCost + d.costStep) + ",zoneWidth=" + (d.targetMax - d.targetMin - d.narrowPerTrain) + ",!tapArea");

        // 2) 실패: 임계점을 넘김 → 스탯 그대로, 비용은 그대로 나감(환불 없음), 다음 비용도 그대로
        Add("mark", "", "");
        Add("click", TRAIN + "StartButton", "trainRunning,goldDelta=-" + (d.baseCost + d.costStep));
        Add("trainPlay", "over", "trainResult=Over,statGain:" + stat + "=" + d.gain + ",goldDelta=-" + (d.baseCost + d.costStep) + ",trainCost=" + (d.baseCost + d.costStep));
        Add("click", TRAIN + "CloseButton", "!trainOpen");
        Add("click", EXIT, "info,!interior");

        // 3) 전투 가장자리 연출: 체력 30% 이하 빨강(낮을수록 진함) → 회복하면 사라짐, 정신 계열 디버프 동안 검정 → 풀리면 사라짐
        var s = battle.settings;
        float a25 = Mathf.Lerp(s.lowHpAlphaMin, s.lowHpAlphaMax, 1f - 0.25f / s.lowHpThreshold);
        float a10 = Mathf.Lerp(s.lowHpAlphaMin, s.lowHpAlphaMax, 1f - 0.10f / s.lowHpThreshold);
        Add("sel", "place:" + fightPlace.locationId, "info");
        Add("prepBattle", "", "");
        Add("click", ENTER, "battle,!edgeLow,!edgeMental");
        Add("setHp", "25", "");
        Add("wait", "90", "edgeLow=" + a25.ToString("0.000") + ",!edgeMental");
        Add("setHp", "10", "");
        Add("wait", "90", "edgeLow=" + a10.ToString("0.000"));
        Add("setHp", "100", "");
        Add("wait", "90", "!edgeLow");
        Add("addMental", "", "");
        Add("wait", "90", "edgeMental=" + s.mentalAlpha.ToString("0.000") + ",!edgeLow");
        Add("clearMental", "", "");
        Add("wait", "90", "!edgeMental");
        Add("battleExit", "", "!battle");
    }

    // 훈련 봇 한 틱: success = 목표 구간 가운데 근처 유지, over = 한 번에 몰아 클릭. 결과가 나오면 true
    static bool TrainTick(string mode)
    {
        var g = trainUi.Gauge;
        if (g == null) return true;
        if (g.Result != TrainingResult.Running) return true;

        int taps;
        if (mode == "over") taps = 20;
        else
        {
            float mid = (g.ZoneMin + g.ZoneMax) / 2f;
            taps = Mathf.Clamp(Mathf.CeilToInt((mid - g.Value) / trainUi.Data.tapIncrease), 0, 4);
        }

        for (int i = 0; i < taps && g.Result == TrainingResult.Running; i++) ClickPath("Canvas/TrainingPanel/TapArea");
        return g.Result != TrainingResult.Running;
    }

    static void GoToArea(ref string cur, AreaData target)
    {
        if (cur == target.areaId) return;
        Add("click", BACK, "map=region");
        Add("sel", "area:" + target.areaId, "info");
        Add("click", ENTER, "map=" + target.areaId);
        cur = target.areaId;
    }

    static PlayerState P => GameStateManager.Instance.State.player;

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
            else if (s.kind == "prepBattle")
            {
                // 테스트 지름길: 병원 화면(C-2) 전이라 전투 전 체력·기력을 채움. 보스는 적정 장비(실제 고급 무기·방어구 데이터) 장착
                var p = GameStateManager.Instance.EnsurePlayer(battle.settings, battle.database);
                botMode = s.target == "boss" ? "" : s.target;

                if (s.target == "boss")
                {
                    // 적정 장비(고급 무기·방어구) + 약 1시간 성장(레벨 7: 힘 3·정신력 3, 주사위 숙련도 9) + 음식 3개 (시뮬레이션 승률 100%, 14.6~17.4턴)
                    var geared = BattleSimulator.WithGear(p, battle.settings, battle.database, ItemGrade.Advanced, ItemGrade.Advanced);
                    geared.level = Mathf.Max(geared.level, 7);
                    geared.stats.strength = Mathf.Max(geared.stats.strength, 3);
                    geared.stats.spirit = Mathf.Max(geared.stats.spirit, 3);
                    var dm = geared.MasteryOf(WeaponType.Dice);
                    if (dm.level < 9) { dm.level = 9; dm.exp = 0; }
                    PlayerStats.Recalculate(geared, battle.settings, battle.database);
                    if (geared.CountOf("food") < 3) geared.AddItem("food", 3 - geared.CountOf("food"));
                    GameStateManager.Instance.State.player = geared;
                    p = geared;
                }

                p.currentHp = p.maxHp;
                p.currentEnergy = p.maxEnergy;

                if (s.target == "lose")
                {
                    p.currentHp = 1;
                    p.gold = 100;
                }
            }
            else if (s.kind == "comicPlay")
            {
                comicCallback = false;
                var comic = AssetDatabase.LoadAssetAtPath<ComicData>(s.target);
                ComicViewer.Instance.Play(comic, () => comicCallback = true);
            }
            else if (s.kind == "comicAdvance")
            {
                // 등장 중이면 첫 클릭은 등장 완료 (실제 플레이와 같음)
                if (ComicViewer.Instance.Animating) ClickPath("Canvas/ComicPanel");
                ClickPath("Canvas/ComicPanel");
            }
            // [C-2]
            else if (s.kind == "mark")
            {
                markGold = P.gold;
                markFood = P.CountOf("food");
                markHp = P.currentHp;
            }
            else if (s.kind == "newPlayer")
            {
                // 새 게임 주인공 (진행 플래그는 그대로)
                var np = PlayerState.CreateFrom(battle.settings.playerBase);
                PlayerStats.Recalculate(np, battle.settings, battle.database);
                GameStateManager.Instance.State.player = np;
            }
            else if (s.kind == "hurt")
            {
                P.currentHp = Mathf.Max(1, P.maxHp * 3 / 10);
                P.currentEnergy = Mathf.Min(P.currentEnergy, 2);
            }
            else if (s.kind == "grant")
            {
                P.gold += 1000;
                P.AddItem(eco.materialItem.itemId, 3 - P.CountOf(eco.materialItem.itemId));
            }
            else if (s.kind == "openShop")
            {
                var kv = s.target.Split(':');
                shopUi.Open(places[kv[0]].interactions.Find(x => x.interactionId == kv[1]).shop);
            }
            else if (s.kind == "wait") { }
            else if (s.kind == "setHp")
            {
                var bp = battle.Engine.player;
                bp.currentHp = Mathf.Max(1, Mathf.RoundToInt(bp.maxHp * int.Parse(s.target) / 100f));
            }
            else if (s.kind == "addMental")
            {
                var morale = AssetDatabase.LoadAssetAtPath<StatusEffectData>("Assets/Data/Battle/Effects/Effect_Morale.asset");
                battle.Engine.playerEffects.Add(new ActiveEffect { data = morale, turnsLeft = morale.duration });
            }
            else if (s.kind == "clearMental")
            {
                battle.Engine.playerEffects.RemoveAll(x => x.data != null && x.data.isMental);
            }
            else if (s.kind == "grantLevels")
            {
                for (int i = 0; i < int.Parse(s.target); i++) GrowthLogic.AddExp(P, eco, eco.ExpToNext(P.level) - P.exp);
            }
            else if (s.kind == "grantMastery")
            {
                var m = P.MasteryOf(WeaponType.Dice);
                var rule = eco.MasteryFor(WeaponType.Dice);
                m.level = 9;
                m.exp = rule.expPerLevel - 1;   // 공격 1번이면 10레벨
                PlayerStats.Recalculate(P, battle.settings, battle.database);
            }
            else if (s.kind == "homeRow")
            {
                int row = -1;
                for (int i = 0; i < homeUi.Visible.Count; i++)
                {
                    var r = homeUi.Visible[i];
                    if (r.item != null && "item:" + r.item.itemId == s.target && !P.IsEquipped(r.uid)) { row = i; break; }
                }
                if (row < 0) return " rowNotFound";
                var go = GameObject.Find(HOME + "ListArea/Row" + row + "/ActionButton");
                if (go == null) return " rowInactive";
                var sp = Center(go);
                var h = HandlerAt(sp);
                if (h != go) return " clickBlockedBy:" + (h == null ? "none" : h.name);
                ExecuteEvents.Execute(go, new PointerEventData(EventSystem.current) { position = sp }, ExecuteEvents.pointerClickHandler);
            }
            else if (s.kind == "clickRow")
            {
                int row = -1;
                for (int i = 0; i < shopUi.Visible.Count; i++)
                {
                    var r = shopUi.Visible[i];
                    bool hit = s.target == "equippedWeapon"
                        ? r.uid != 0 && r.uid == P.equippedWeaponUid
                        : r.item != null && "item:" + r.item.itemId == s.target && !P.IsEquipped(r.uid);
                    if (hit) { row = i; break; }
                }
                if (row < 0) return " rowNotFound";
                var go = GameObject.Find(SHOP_ROW + row + "/ActionButton");
                if (go == null) return " rowInactive";
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
                // [B-4] 만화·전투 결과
                else if (e == "comic" || e == "comicOpen") v = ComicViewer.Instance != null && ComicViewer.Instance.IsPlaying;
                else if (e == "comicDone") v = comicCallback;
                else if (key == "comicPanel") v = ComicViewer.Instance.PanelIndex + 1 == int.Parse(val);
                else if (e.StartsWith("placeholder:"))
                {
                    var kv = e.Substring(12).Split(':');
                    string want = "[만화 " + kv[0] + "페이지 " + kv[1] + "칸 미정]";
                    v = false;
                    foreach (var t in canvas.Find("ComicPanel").GetComponentsInChildren<TMP_Text>()) if (t.text == want) v = true;
                }
                else if (key == "lines")
                {
                    int n = 0;
                    foreach (var t in canvas.Find("ComicPanel/Page/Panels").GetComponentsInChildren<RectTransform>()) if (t.name == "Line") n++;
                    v = n == int.Parse(val);
                }
                else if (e == "won") v = botResult != null && botResult.StartsWith("승리");
                // [C-1] 승리 보상: 종류가 맞고, 골드가 들어왔고, 결과 창에 표시됨
                else if (e.StartsWith("rewarded:"))
                {
                    var r = battle.LastReward;
                    v = r != null && r.kind.ToString() == e.Substring(9) && r.gold > 0 && r.exp > 0 &&
                        botDetail != null && botDetail.Contains("골드 +" + r.gold) && g.State.player.gold >= r.gold;
                }
                else if (e == "lost") v = botResult != null && botResult.StartsWith("패배");
                else if (e == "fled") v = botResult != null && botResult.StartsWith("도망");
                else if (e == "introComicFirst") v = botComicsBeforeTurn1 >= 1;
                else if (key == "phaseComics") v = botComics - botComicsBeforeTurn1 == int.Parse(val);
                else if (e.StartsWith("cleared:")) v = g.HasFlag(StateKeys.BattleCleared(e.Substring(8)));
                else if (e.StartsWith("bossFlag:")) v = g.HasFlag(StateKeys.BossDefeated(e.Substring(9)));
                else if (e == "respawned")
                {
                    var st = battle.settings;
                    var p = g.State.player;
                    v = mapNow == st.defaultRespawnArea.areaId && Active("PlaceInteriorPanel") &&
                        canvas.Find("PlaceInteriorPanel/PlaceNameText").GetComponent<TMP_Text>().text == st.defaultRespawnPlace.placeName &&
                        p.currentHp == Mathf.Max(1, Mathf.RoundToInt(p.maxHp * st.reviveHpRatio)) && p.gold == 90;
                }
                // [C-5] 훈련·연출
                else if (e == "trainOpen") v = Active("TrainingPanel");
                else if (key == "trainStat") v = trainUi.Data != null && trainUi.Data.stat.ToString() == val;
                else if (key == "trainCost") v = TrainingLogic.Cost(P, trainUi.Data) == int.Parse(val) && canvas.Find("TrainingPanel/Window/StartButton").GetComponentInChildren<TMP_Text>().text.Contains(val + "G");
                else if (e == "trainRunning") v = trainUi.Gauge != null && trainUi.Gauge.Result == TrainingResult.Running;
                else if (e == "tapArea") v = Active("TrainingPanel/TapArea");
                else if (key == "trainResult") v = trainUi.LastResult.ToString() == val;
                else if (e.StartsWith("statGain:")) { var kv = e.Substring(9).Split('='); v = P.trainCount.Get((StatType)System.Enum.Parse(typeof(StatType), kv[0])) == int.Parse(kv[1]) && P.stats.Get((StatType)System.Enum.Parse(typeof(StatType), kv[0])) >= int.Parse(kv[1]); }
                else if (key == "zoneWidth") { float mn, mx; TrainingLogic.Zone(P, trainUi.Data, out mn, out mx); v = Mathf.Abs((mx - mn) - float.Parse(val)) < 0.01f; }
                else if (e == "edgeLow" && eq < 0) v = edgeFx.LowHpAlpha > 0.001f;
                else if (key == "edgeLow") v = Mathf.Abs(edgeFx.LowHpAlpha - float.Parse(val)) < 0.01f && Mathf.Abs(edgeFx.LowHpTarget - float.Parse(val)) < 0.01f;
                else if (e == "edgeMental" && eq < 0) v = edgeFx.MentalAlpha > 0.001f;
                else if (key == "edgeMental") v = Mathf.Abs(edgeFx.MentalAlpha - float.Parse(val)) < 0.01f;
                // [C-4] 성장
                else if (e == "profileDot") v = Active("TopBar/ProfileButton/Badge");
                else if (e == "pickOpen") v = Active("PickPanel");
                else if (key == "pickMode") v = pickUi.CurrentMode.ToString() == val;
                else if (key == "pickCards") { int n = 0; for (int i = 0; i < 4; i++) if (Active("PickPanel/Card" + i)) n++; v = n == int.Parse(val); }
                else if (e.StartsWith("talent:")) v = P.talentIds.Contains(e.Substring(7));
                else if (key == "points") v = P.statPoints == int.Parse(val);
                else if (key == "charm") v = P.totalStats.charm == int.Parse(val) && canvas.Find("HomePanel/Window/StatArea/StatRow4/ValueText").GetComponent<TMP_Text>().text == val;
                else if (key == "discount") v = Mathf.RoundToInt(P.shopDiscount * 100) == int.Parse(val);
                else if (e.StartsWith("revealed:")) v = g.HasFlag(StateKeys.Revealed("place:" + e.Substring(9)));
                else if (e == "toast") v = NoticeToast.LastText == RevealScanner.Notice && Active("NoticeToast/Box");
                else if (e.StartsWith("marker:")) v = GameObject.Find("MapView/Marker_" + e.Substring(7)) != null;
                else if (e.StartsWith("priceShown:"))
                {
                    var kv = e.Substring(11).Split(new[] { ':' }, 2);
                    var row = shopUi.Visible.FindIndex(r => r.item != null && r.item.itemId == kv[0]);
                    v = row >= 0 && canvas.Find("ShopPanel/Window/Row" + row + "/ActionButton").GetComponentInChildren<TMP_Text>().text.StartsWith(kv[1]);
                }
                else if (key == "diceCount") v = P.diceCount == int.Parse(val);
                // [C-3] 홈 창
                else if (e == "homeOpen") v = Active("HomePanel");
                else if (e == "homeReadOnly") v = homeUi.ReadOnly;
                else if (key == "homeTab") v = homeUi.CurrentTab.ToString() == val;
                else if (e == "homeMsg") v = homeUi.Message != "";
                else if (e == "noWeapon") v = P.equippedWeaponUid == 0;
                else if (e == "hpUp") v = P.currentHp > markHp;
                else if (e == "homeRowsLocked") v = homeUi.Visible.Count > 0 && homeUi.Visible.TrueForAll(r => !r.enabled) &&
                                                    !canvas.Find("HomePanel/Window/ListArea/Row0/ActionButton").GetComponent<Button>().interactable;
                else if (key == "attackShown") v = canvas.Find("HomePanel/Window/TextArea/RightText").GetComponent<TMP_Text>().text.Contains("공격  " + val);
                // [C-2] 상점·병원·경제
                else if (e == "notice") v = res == RESPAWN_NOTICE;
                else if (e.StartsWith("respawnIs:")) v = P.respawnPlaceId == e.Substring(10) && P.respawnAreaId == mapNow;
                else if (e.StartsWith("btn:")) v = GameObject.Find(IA + e.Substring(4)) != null;
                else if (e == "shopOpen") v = Active("ShopPanel");
                else if (e == "hospOpen") v = Active("HospitalPanel");
                else if (e == "sellTab") v = Active("ShopPanel/Window/SellTab");
                else if (e == "confirm") v = shopUi.ConfirmOpen;
                else if (e == "shopMsg") v = shopUi.Message != "";
                else if (e == "hospMsg") v = canvas.Find("HospitalPanel/Window/MessageText").GetComponent<TMP_Text>().text != "";
                else if (key == "tab") v = shopUi.CurrentTab.ToString() == val;
                else if (key == "shopRows")
                {
                    int n = 0;
                    for (int i = 0; i < 4; i++) if (Active("ShopPanel/Window/Row" + i)) n++;
                    v = n == int.Parse(val) && shopUi.Visible.Count == n;
                }
                else if (e == "noRare") v = !shopUi.Visible.Exists(r => r.item != null && r.item.grade == ItemGrade.Rare);
                else if (e == "allRare") v = shopUi.Visible.Count > 0 && shopUi.Visible.TrueForAll(r => r.item != null && r.item.grade == ItemGrade.Rare);
                else if (e == "equippedLocked") v = shopUi.Visible.Exists(r => P.IsEquipped(r.uid) && !r.enabled) && !shopUi.Visible.Exists(r => P.IsEquipped(r.uid) && r.enabled);
                else if (key == "gold") v = P.gold == int.Parse(val);
                else if (e == "goldMore") v = P.gold > markGold;
                else if (key == "goldDelta") v = P.gold - markGold == int.Parse(val);
                else if (key == "foodDelta") v = P.CountOf("food") - markFood == int.Parse(val);
                else if (e == "fullHp") v = P.currentHp == P.maxHp && P.currentEnergy == P.maxEnergy;
                else if (e.StartsWith("weaponIs:")) { var w = P.FindEquipment(P.equippedWeaponUid); v = w != null && w.itemId == e.Substring(9); }
                else if (key == "weaponEnhance") { var w = P.FindEquipment(P.equippedWeaponUid); v = w != null && w.enhance == int.Parse(val); }
                else if (key == "attack") v = P.attack == int.Parse(val);
                else if (key == "skills") v = P.skillIds.Count == int.Parse(val);
                else if (key == "stones") v = P.CountOf(eco.materialItem.itemId) == int.Parse(val);
                else if (e.StartsWith("owns:")) v = P.equipment.Exists(x => x.itemId == e.Substring(5));
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

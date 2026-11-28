using UnityEngine;
using UnityEngine.UI;
using TMPro;

// [C-5] 훈련 창 (Canvas/TrainingPanel, EconomyUIBuilder가 만든다).
// 시작 전: 스탯·비용·목표 구간 안내 → [훈련 시작] → 4초 동안 화면 어디든 클릭 → 결과.
public class TrainingController : MonoBehaviour
{
    [Header("데이터")]
    public BattleSettings settings;
    public GameDatabase database;
    public MapView mapView;
    public LocationSelector selector;

    [Header("화면")]
    public TMP_Text titleText;
    public TMP_Text infoText;
    public TMP_Text timerText;
    public TMP_Text resultText;
    public Button startButton;
    public TMP_Text startButtonText;
    public Button closeButton;
    [Tooltip("훈련 중 화면 전체 클릭 영역")]
    public GameObject tapArea;

    [Header("게이지 (가로 막대, 0~임계점)")]
    public RectTransform gaugeArea;
    public RectTransform zoneRect;
    public Image fill;

    public bool IsOpen => gameObject.activeSelf;
    public TrainingGauge Gauge { get; private set; }
    public TrainingResult LastResult { get; private set; } = TrainingResult.Running;
    public TrainingData Data { get; private set; }

    PlaceData place;

    PlayerState Player => GameStateManager.Instance.EnsurePlayer(settings, database);

    public void Open(PlaceData p, TrainingData data)
    {
        place = p;
        Data = data;
        Gauge = null;
        LastResult = TrainingResult.Running;
        gameObject.SetActive(true);
        if (resultText != null) resultText.text = "[훈련 대사 미정]";
        ShowIdle();
    }

    // 닫기 (CloseButton OnClick) — 훈련 중에는 닫을 수 없음
    public void Close()
    {
        if (Gauge != null && Gauge.Result == TrainingResult.Running) return;
        gameObject.SetActive(false);
    }

    // [훈련 시작] (StartButton OnClick)
    public void OnStart()
    {
        if (Data == null || (Gauge != null && Gauge.Result == TrainingResult.Running)) return;

        var g = TrainingLogic.Begin(Player, Data);

        if (g == null)
        {
            resultText.text = "골드가 부족하다. (" + TrainingLogic.Cost(Player, Data) + "G 필요)";
            return;
        }

        Gauge = g;
        LastResult = TrainingResult.Running;
        resultText.text = "연타! 목표 구간에서 멈춰라";
        startButton.gameObject.SetActive(false);
        closeButton.interactable = false;
        tapArea.SetActive(true);
        DrawGauge();
    }

    // 화면 클릭 (TapArea OnClick)
    public void OnTap()
    {
        if (Gauge == null || Gauge.Result != TrainingResult.Running) return;
        Gauge.Tap();
        DrawGauge();
        if (Gauge.Result != TrainingResult.Running) EndGame();
    }

    void Update()
    {
        if (Gauge == null || Gauge.Result != TrainingResult.Running) return;
        Gauge.Tick(Time.unscaledDeltaTime);
        DrawGauge();
        if (Gauge.Result != TrainingResult.Running) EndGame();
    }

    void EndGame()
    {
        var p = Player;
        LastResult = Gauge.Result;
        TrainingLogic.Finish(p, Data, LastResult);
        PlayerStats.Recalculate(p, settings, database);
        tapArea.SetActive(false);
        closeButton.interactable = true;

        string stat = DisplayNames.Stat(Data.stat);

        switch (LastResult)
        {
            case TrainingResult.Success: resultText.text = "성공! " + stat + " +" + Data.gain; break;
            case TrainingResult.Over: resultText.text = "실패... 너무 몰아붙였다. (비용은 돌려받지 못한다)"; break;
            default: resultText.text = "실패... 목표에 닿지 못했다. (비용은 돌려받지 못한다)"; break;
        }

        // 매력 훈련 등으로 숨겨진 것이 드러났으면 알림
        if (LastResult == TrainingResult.Success && mapView != null &&
            RevealScanner.Scan(GameStateManager.Instance.State, mapView.CurrentRegion ?? mapView.startRegion) > 0)
        {
            NoticeToast.Show(RevealScanner.Notice);
            if (selector != null) selector.RefreshMap();
        }

        ShowIdle(false);
    }

    void ShowIdle(bool resetGauge = true)
    {
        var p = Player;
        int cost = TrainingLogic.Cost(p, Data);
        float min, max;
        TrainingLogic.Zone(p, Data, out min, out max);

        if (titleText != null) titleText.text = place != null ? place.placeName : "[훈련 장소 미정]";
        infoText.text = DisplayNames.Stat(Data.stat) + " " + ConditionChecker.StatValue(p, Data.stat) + "   ·   비용 " + cost + "G (소지 " + p.gold + "G)" +
                        "\n목표 구간 " + Mathf.RoundToInt(min) + "~" + Mathf.RoundToInt(max) + " (" + Mathf.RoundToInt(Data.limit) + "을 넘기면 실패)";
        timerText.text = "";
        startButton.gameObject.SetActive(true);
        startButtonText.text = "훈련 시작 (" + cost + "G)";
        startButton.interactable = p.gold >= cost;
        tapArea.SetActive(false);

        if (resetGauge)
        {
            SetZone(min, max);
            fill.fillAmount = 0f;
        }
    }

    void DrawGauge()
    {
        SetZone(Gauge.ZoneMin, Gauge.ZoneMax);
        fill.fillAmount = Mathf.Clamp01(Gauge.Value / Data.limit);
        fill.color = Gauge.InZone ? new Color(0.2f, 0.75f, 0.3f) : new Color(0.95f, 0.65f, 0.15f);
        timerText.text = Gauge.TimeLeft.ToString("0.0") + "초";
    }

    void SetZone(float min, float max)
    {
        if (zoneRect == null) return;
        zoneRect.anchorMin = new Vector2(min / Data.limit, 0f);
        zoneRect.anchorMax = new Vector2(max / Data.limit, 1f);
        zoneRect.offsetMin = zoneRect.offsetMax = Vector2.zero;
    }
}

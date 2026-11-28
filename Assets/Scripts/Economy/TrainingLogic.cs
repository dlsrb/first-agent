using System;

// [C-5] 스탯 훈련 (성장경제규칙 5번 "스탯 훈련 장소", 설계안 1-8). 공통 미니게임 1개를 장소마다 데이터로 다르게 쓴다.
// 연속 클릭으로 게이지를 올리되 임계점(100)을 넘기지 않고, 시간이 끝날 때 목표 구간 안에 있으면 성공.

// 장소 상호작용(Train)에 붙는 훈련 설정
[Serializable]
public class TrainingData
{
    public StatType stat = StatType.Strength;

    [UnityEngine.Header("비용 (그 스탯을 훈련한 횟수마다 증가)")]
    public int baseCost = 80;
    public int costStep = 40;
    [UnityEngine.Tooltip("성공 시 스탯 증가량")]
    public int gain = 1;

    [UnityEngine.Header("게이지 (0~임계점)")]
    public float limit = 100f;
    public float tapIncrease = 7f;
    [UnityEngine.Tooltip("초당 줄어드는 양")]
    public float decayPerSecond = 12f;
    public float duration = 4f;
    public float targetMin = 65f;
    public float targetMax = 85f;
    [UnityEngine.Tooltip("훈련할 때마다 목표 구간이 좁아지는 폭 (양쪽 합)")]
    public float narrowPerTrain = 1f;
    [UnityEngine.Tooltip("목표 구간 최소 폭")]
    public float minWidth = 8f;
}

public enum TrainingResult
{
    Running,
    Success,   // 시간이 끝날 때 목표 구간 안
    Missed,    // 시간이 끝날 때 목표 구간 밖
    Over       // 임계점을 넘김 (즉시 실패)
}

// 게이지 판정 (순수 C#, 시간은 바깥에서 넣어 준다 → 테스트 가능)
public class TrainingGauge
{
    public float Value { get; private set; }
    public float TimeLeft { get; private set; }
    public float ZoneMin { get; private set; }
    public float ZoneMax { get; private set; }
    public TrainingResult Result { get; private set; }

    readonly TrainingData data;

    public TrainingGauge(TrainingData data, float zoneMin, float zoneMax)
    {
        this.data = data;
        ZoneMin = zoneMin;
        ZoneMax = zoneMax;
        TimeLeft = data.duration;
        Result = TrainingResult.Running;
    }

    public bool InZone => Value >= ZoneMin && Value <= ZoneMax;

    public void Tap()
    {
        if (Result != TrainingResult.Running) return;
        Value += data.tapIncrease;
        if (Value > data.limit) Result = TrainingResult.Over;
    }

    public void Tick(float dt)
    {
        if (Result != TrainingResult.Running) return;

        Value = Math.Max(0f, Value - data.decayPerSecond * dt);
        TimeLeft -= dt;

        if (TimeLeft <= 0f)
        {
            TimeLeft = 0f;
            Result = InZone ? TrainingResult.Success : TrainingResult.Missed;
        }
    }
}

public static class TrainingLogic
{
    // [11/28] 하루 한도의 "오늘" (기기 시계). 테스트에서 가짜 날짜로 바꾼다
    public static System.Func<DateTime> Clock = () => DateTime.Now;

    public static string Today => Clock().ToString("yyyy-MM-dd");

    public const string DailyLimitText = "오늘은 더 훈련할 수 없다";
    public const string TotalLimitText = "더 이상 훈련으로는 오르지 않는다";

    // 오늘 이 스탯을 성공한 횟수
    public static int TodayCount(PlayerState p, StatType stat)
    {
        var d = p.trainDays.Find(x => x.stat == stat.ToString());
        return d != null && d.date == Today ? d.count : 0;
    }

    // 훈련할 수 있는지 (막히면 이유). eco가 없으면 한도 없음
    public static bool CanTrain(PlayerState p, TrainingData d, EconomySettings eco, out string reason)
    {
        reason = "";
        if (eco == null) return true;

        if (eco.trainTotalLimit > 0 && p.trainCount.Get(d.stat) * d.gain >= eco.trainTotalLimit)
        {
            reason = TotalLimitText;
            return false;
        }

        if (eco.trainDailyLimit > 0 && TodayCount(p, d.stat) >= eco.trainDailyLimit)
        {
            reason = DailyLimitText;
            return false;
        }

        return true;
    }

    public static int Cost(PlayerState p, TrainingData d)
    {
        return d.baseCost + d.costStep * p.trainCount.Get(d.stat);
    }

    // 목표 구간: 가운데를 유지하며 훈련 횟수만큼 좁아진다 (최소 폭)
    public static void Zone(PlayerState p, TrainingData d, out float min, out float max)
    {
        float width = Math.Max(d.minWidth, (d.targetMax - d.targetMin) - d.narrowPerTrain * p.trainCount.Get(d.stat));
        float mid = (d.targetMin + d.targetMax) / 2f;
        min = mid - width / 2f;
        max = mid + width / 2f;
    }

    // 비용을 내고 게이지 시작 (골드 부족·한도면 null). 실패해도 환불 없음
    public static TrainingGauge Begin(PlayerState p, TrainingData d, EconomySettings eco = null)
    {
        int cost = Cost(p, d);
        string reason;
        if (p.gold < cost || !CanTrain(p, d, eco, out reason)) return null;

        p.gold -= cost;
        float min, max;
        Zone(p, d, out min, out max);
        return new TrainingGauge(d, min, max);
    }

    // 결과 반영: 성공이면 스탯 +gain, 훈련 횟수 +1 (규칙 "올릴수록 비용이 오른다" → 성공한 횟수 기준).
    // 실패는 비용만 나가고 횟수는 그대로
    public static void Finish(PlayerState p, TrainingData d, TrainingResult result)
    {
        if (result != TrainingResult.Success) return;
        p.stats.Add(d.stat, d.gain);
        p.trainCount.Add(d.stat, 1);

        // 하루 한도 기록
        var day = p.trainDays.Find(x => x.stat == d.stat.ToString());
        if (day == null) { day = new TrainDay { stat = d.stat.ToString() }; p.trainDays.Add(day); }
        if (day.date != Today) { day.date = Today; day.count = 0; }
        day.count++;
    }
}

using NUnit.Framework;

// C-5 스탯 훈련: 게이지 판정(성공·임계점 초과·구간 밖), 비용 증가, 목표 구간 좁아짐, 실패 시 환불 없음
public class TrainingTests
{
    TrainingData data;
    PlayerState p;

    [SetUp]
    public void SetUp()
    {
        data = new TrainingData { stat = StatType.Strength };   // 기본값: 80G +40, 클릭 +7, 초당 −12, 4초, 65~85, 임계 100
        p = new PlayerState { gold = 1000 };
    }

    [Test]
    public void Gauge_TapRaises_DecayLowers()
    {
        var g = new TrainingGauge(data, 65, 85);
        g.Tap(); g.Tap();
        Assert.AreEqual(14f, g.Value, 1e-4);
        g.Tick(0.5f);
        Assert.AreEqual(8f, g.Value, 1e-4);
        Assert.AreEqual(3.5f, g.TimeLeft, 1e-4);
        g.Tick(10f);
        Assert.AreEqual(0f, g.Value, 1e-4, "0 아래로 내려가지 않음");
    }

    [Test]
    public void Gauge_InZoneAtEnd_Success()
    {
        var g = new TrainingGauge(data, 65, 85);
        for (int i = 0; i < 11; i++) g.Tap();          // 77
        g.Tick(0.2f);                                  // 74.6
        Assert.AreEqual(TrainingResult.Running, g.Result);
        g.Tick(3.8f);                                  // 시간 끝: 74.6 − 45.6 = 29 → 구간 밖
        Assert.AreEqual(TrainingResult.Missed, g.Result);

        var g2 = new TrainingGauge(data, 65, 85);
        g2.Tick(3.9f);
        for (int i = 0; i < 11; i++) g2.Tap();         // 마지막에 몰아서 77
        g2.Tick(0.1f);                                 // 75.8, 시간 끝
        Assert.AreEqual(TrainingResult.Success, g2.Result);
    }

    [Test]
    public void Gauge_OverLimit_FailsImmediately()
    {
        var g = new TrainingGauge(data, 65, 85);
        for (int i = 0; i < 14; i++) g.Tap();          // 98
        Assert.AreEqual(TrainingResult.Running, g.Result);
        g.Tap();                                       // 105 > 100
        Assert.AreEqual(TrainingResult.Over, g.Result);
        g.Tap();
        g.Tick(1f);
        Assert.AreEqual(TrainingResult.Over, g.Result, "끝난 뒤에는 바뀌지 않음");
    }

    [Test]
    public void Begin_PaysCost_NotEnoughGold_Null()
    {
        Assert.IsNotNull(TrainingLogic.Begin(p, data));
        Assert.AreEqual(920, p.gold);

        p.gold = 79;
        Assert.IsNull(TrainingLogic.Begin(p, data));
        Assert.AreEqual(79, p.gold);
    }

    [Test]
    public void Success_RaisesStat_CostUp_ZoneNarrower()
    {
        TrainingLogic.Begin(p, data);
        TrainingLogic.Finish(p, data, TrainingResult.Success);

        Assert.AreEqual(1, p.stats.strength);
        Assert.AreEqual(120, TrainingLogic.Cost(p, data));

        float min, max;
        TrainingLogic.Zone(p, data, out min, out max);
        Assert.AreEqual(65.5f, min, 1e-4);
        Assert.AreEqual(84.5f, max, 1e-4);
    }

    [Test]
    public void Failure_NoRefund_NoStat_CountUnchanged()
    {
        TrainingLogic.Begin(p, data);
        TrainingLogic.Finish(p, data, TrainingResult.Over);
        TrainingLogic.Begin(p, data);
        TrainingLogic.Finish(p, data, TrainingResult.Missed);

        Assert.AreEqual(1000 - 80 - 80, p.gold, "실패해도 비용은 나감");
        Assert.AreEqual(0, p.stats.strength);
        Assert.AreEqual(80, TrainingLogic.Cost(p, data), "실패는 비용을 올리지 않음");
    }

    [Test]
    public void Zone_NeverNarrowerThanMinWidth()
    {
        p.trainCount.strength = 100;
        float min, max;
        TrainingLogic.Zone(p, data, out min, out max);
        Assert.AreEqual(8f, max - min, 1e-4);
        Assert.AreEqual(75f, (min + max) / 2f, 1e-4, "가운데 유지");
    }
}

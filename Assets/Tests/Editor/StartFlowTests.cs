using NUnit.Framework;
using UnityEngine;

// C-6 시작 흐름 잠금 규칙: 시작 구역·시작 병원만 열림 → 시작 병원에 들어가면 전부 열림
public class StartFlowTests
{
    EconomySettings eco;
    AreaData downtown, mountain;
    PlaceData hospital, store;
    GameState state;

    [SetUp]
    public void SetUp()
    {
        downtown = ScriptableObject.CreateInstance<AreaData>();
        mountain = ScriptableObject.CreateInstance<AreaData>();
        hospital = ScriptableObject.CreateInstance<PlaceData>(); hospital.locationId = "hospital";
        store = ScriptableObject.CreateInstance<PlaceData>(); store.locationId = "store";
        eco = ScriptableObject.CreateInstance<EconomySettings>();
        eco.startArea = downtown;
        eco.startHospital = hospital;
        state = new GameState();
    }

    [Test]
    public void BeforeIntro_OnlyStartAreaAndHospitalOpen()
    {
        Assert.IsFalse(StartFlowController.IsLocked(state, eco, null, downtown));
        Assert.IsTrue(StartFlowController.IsLocked(state, eco, null, mountain));
        Assert.IsFalse(StartFlowController.IsLocked(state, eco, hospital, null));
        Assert.IsTrue(StartFlowController.IsLocked(state, eco, store, null));
    }

    [Test]
    public void EnteringStartHospital_UnlocksEverything_Once()
    {
        Assert.IsFalse(StartFlowController.TryUnlock(state, eco, store), "다른 장소로는 안 열림");
        Assert.IsTrue(StartFlowController.TryUnlock(state, eco, hospital));
        Assert.IsFalse(StartFlowController.TryUnlock(state, eco, hospital), "한 번만");

        Assert.IsFalse(StartFlowController.IsLocked(state, eco, null, mountain));
        Assert.IsFalse(StartFlowController.IsLocked(state, eco, store, null));
    }

    [Test]
    public void NoStartRule_NothingLocked()
    {
        eco.startHospital = null;
        Assert.IsFalse(StartFlowController.IsLocked(state, eco, store, null));
        Assert.IsFalse(StartFlowController.IsLocked(state, null, store, null));
    }
}

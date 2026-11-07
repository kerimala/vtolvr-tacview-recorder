using UnityEngine;
using UnityEngine.SceneManagement;
using VTOLVR.Multiplayer;
using VTOLVRTacviewSPRecorder.Main;
using Xunit;

namespace VTOLVRTacviewSPRecorder.Lifecycle.Tests;

public sealed class RecorderLifecycleTests : IDisposable
{
    private readonly MonoBehaviour _host = new();
    private readonly RecorderLifecycle _lifecycle;

    public RecorderLifecycleTests()
    {
        SceneManager.Reset();
        Time.realtimeSinceStartupAsDouble = 0;
        FlightSceneManager.instance = new FlightSceneManager();
        VTMapManager.fetch = new VTMapManager();
        VTScenario.current = new VTScenario();
        TargetManager.instance = new TargetManager();
        VTOLMPUtils.Multiplayer = false;
        VTOLMPUtils.ThrowOnCheck = false;
        VtolTerrainExporter.Available = true;
        VtolTerrainExporter.ThrowOnProbe = false;
        VtolTerrainExporter.ProbeCalls = 0;
        VtolTerrainSourceFactory.ClearCalls = 0;
        RecorderSession.Instances.Clear();
        RecorderSession.StartFailuresRemaining = 0;
        RecorderSession.StartThrowsRemaining = 0;
        Logger.Messages.Clear();
        _lifecycle = new RecorderLifecycle(_host);
    }

    public void Dispose() => _lifecycle.Dispose();

    [Theory]
    [InlineData("Akutan")]
    [InlineData("CustomMapBase")]
    [InlineData("CustomMapBase_OverCloud")]
    public void ReadyMissionStartsWithoutVtolApiOrSceneEvents(string scene)
    {
        SceneManager.ChangeScene(scene, notify: false);
        _lifecycle.StartListening();
        Assert.True(Assert.Single(RecorderSession.Instances).IsActive);
        Assert.Contains(Logger.Messages, line => line.Contains("VTOLAPI is not required"));
    }

    [Theory]
    [InlineData("ReadyRoom")]
    [InlineData("LoadingScene")]
    [InlineData("VehicleConfiguration")]
    [InlineData("ScenarioEditor")]
    [InlineData("VTMapEditor")]
    public void NonMissionScenesNeverRecordEvenWithStaleReadyState(string scene)
    {
        SceneManager.ChangeScene(scene);
        _lifecycle.StartListening();
        _host.Tick(140);
        Assert.Empty(RecorderSession.Instances);
        Assert.Equal(0, VtolTerrainExporter.ProbeCalls);
    }

    [Theory]
    [InlineData("manager")]
    [InlineData("map")]
    [InlineData("ready")]
    [InlineData("scenario")]
    [InlineData("actors")]
    public void IncompleteNativeStateWaitsAndRecovers(string missing)
    {
        SceneManager.ChangeScene("Akutan");
        switch (missing)
        {
            case "manager": VTMapManager.fetch = null; break;
            case "map": VTMapManager.fetch.map = null; break;
            case "ready": VTMapManager.fetch.scenarioReady = false; break;
            case "scenario": VTScenario.current = null; break;
            case "actors": TargetManager.instance = null; break;
        }
        _lifecycle.StartListening();
        _host.Tick(4);
        Assert.Empty(RecorderSession.Instances);
        VTMapManager.fetch ??= new VTMapManager();
        VTMapManager.fetch.map ??= new object();
        VTMapManager.fetch.scenarioReady = true;
        VTScenario.current ??= new VTScenario();
        TargetManager.instance ??= new TargetManager();
        _host.Tick();
        Assert.True(Assert.Single(RecorderSession.Instances).IsActive);
    }

    [Fact]
    public void RepeatedReadyPollsAndListeningCallsDoNotDuplicateRecording()
    {
        StartReadyMission();
        _lifecycle.StartListening();
        _host.Tick(80);
        Assert.Single(RecorderSession.Instances);
        Assert.Equal(1, _host.CoroutineCount);
        Assert.Equal(1, SceneManager.SubscriberCount);
        Assert.Equal(1, FlightSceneManager.instance.SubscriberCount);
    }

    [Fact]
    public void NativeExitFinalizesBeforeNextPollAndBlocksOldReadyScenario()
    {
        StartReadyMission();
        RecorderSession first = Assert.Single(RecorderSession.Instances);
        FlightSceneManager.instance.Exit();
        Assert.Equal(1, first.SuccessfulStops);
        Assert.Equal(1, first.DisposeCalls);
        _host.Tick(12);
        Assert.Single(RecorderSession.Instances);
        VTScenario.current = new VTScenario();
        _host.Tick();
        Assert.Equal(2, RecorderSession.Instances.Count);
        Assert.True(RecorderSession.Instances[1].IsActive);
    }

    [Fact]
    public void InstantRestartIsDetectedWhenReadinessTransitionOccursBetweenPolls()
    {
        StartReadyMission();
        // The native ScenarioStartRoutine allocates a fresh VTScenario.
        VTMapManager.fetch.scenarioReady = false;
        VTScenario.current = new VTScenario();
        VTMapManager.fetch.scenarioReady = true;
        _host.Tick();
        Assert.Equal(2, RecorderSession.Instances.Count);
        Assert.Equal(1, RecorderSession.Instances[0].SuccessfulStops);
    }

    [Fact]
    public void ReplacementMapManagerStartsFreshRecordingForSameScenario()
    {
        StartReadyMission();
        VTMapManager.fetch = new VTMapManager();
        _host.Tick();
        Assert.Equal(2, RecorderSession.Instances.Count);
        Assert.Equal(1, RecorderSession.Instances[0].SuccessfulStops);
    }

    [Fact]
    public void ReadinessLossFinalizesAndRestorationCanStartAgain()
    {
        StartReadyMission();
        for (int index = 0; index < 4; index++)
        {
            VTMapManager.fetch.scenarioReady = false;
            _host.Tick();
            Assert.False(RecorderSession.Instances[index].IsActive);
            VTMapManager.fetch.scenarioReady = true;
            _host.Tick();
        }
        Assert.Equal(5, RecorderSession.Instances.Count);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void LeavingMissionFinalizesWithOrWithoutUnityNotification(bool notify)
    {
        StartReadyMission();
        RecorderSession session = Assert.Single(RecorderSession.Instances);
        SceneManager.ChangeScene("ReadyRoom", notify);
        if (notify) Assert.Equal(1, session.SuccessfulStops);
        _host.Tick(10);
        Assert.Equal(1, session.SuccessfulStops);
        Assert.Single(RecorderSession.Instances);
    }

    [Fact]
    public void MultiplayerNeverStartsAndSuppressionLogIsNotRepeated()
    {
        VTOLMPUtils.Multiplayer = true;
        StartReadyMission();
        _host.Tick(120);
        Assert.Empty(RecorderSession.Instances);
        Assert.Equal(0, VtolTerrainExporter.ProbeCalls);
        Assert.Single(Logger.Messages, line => line.Contains("start ignored: multiplayer"));
    }

    [Fact]
    public void MultiplayerTransitionStopsActiveRecordingAndAllowsLaterSinglePlayer()
    {
        StartReadyMission();
        VTOLMPUtils.Multiplayer = true;
        _host.Tick(8);
        Assert.Equal(1, Assert.Single(RecorderSession.Instances).SuccessfulStops);
        VTOLMPUtils.Multiplayer = false;
        VTScenario.current = new VTScenario();
        _host.Tick();
        Assert.Equal(2, RecorderSession.Instances.Count);
    }

    [Fact]
    public void MultiplayerDuringTerrainDiscoveryCancelsPendingStart()
    {
        VtolTerrainExporter.Available = false;
        StartReadyMission();
        _host.Tick(10);
        VTOLMPUtils.Multiplayer = true;
        _host.Tick(30);
        Assert.Empty(RecorderSession.Instances);
    }

    [Fact]
    public void SlowMissionLogsReadinessOnceAndCanStartAfterThirtySeconds()
    {
        VTMapManager.fetch.scenarioReady = false;
        StartReadyMission();
        _host.Tick(250);
        Assert.Empty(RecorderSession.Instances);
        Assert.Single(Logger.Messages, line => line.Contains("still waiting for native mission readiness"));
        VTMapManager.fetch.scenarioReady = true;
        _host.Tick();
        Assert.Single(RecorderSession.Instances);
    }

    [Fact]
    public void TerrainReadinessCanRecoverWithinDiscoveryBudget()
    {
        VtolTerrainExporter.Available = false;
        StartReadyMission();
        _host.Tick(10);
        Assert.Empty(RecorderSession.Instances);
        VtolTerrainExporter.Available = true;
        _host.Tick();
        Assert.Single(RecorderSession.Instances);
        Assert.DoesNotContain(Logger.Messages, line => line.Contains("without a MapId"));
    }

    [Fact]
    public void UnsupportedTerrainCannotPreventRecordingIndefinitely()
    {
        VtolTerrainExporter.Available = false;
        StartReadyMission();
        _host.Tick(18);
        Assert.Empty(RecorderSession.Instances);
        _host.Tick();
        Assert.Single(RecorderSession.Instances);
        Assert.Contains(Logger.Messages, line => line.Contains("without a MapId"));
    }

    [Fact]
    public void SceneExitCancelsTerrainWaitAndNextMissionGetsFullBudget()
    {
        VtolTerrainExporter.Available = false;
        StartReadyMission();
        _host.Tick(10);
        int clearCalls = VtolTerrainSourceFactory.ClearCalls;
        SceneManager.ChangeScene("ReadyRoom");
        Assert.True(VtolTerrainSourceFactory.ClearCalls > clearCalls);
        _host.Tick(30);
        Assert.Empty(RecorderSession.Instances);
        VTMapManager.fetch = new VTMapManager();
        VTScenario.current = new VTScenario();
        SceneManager.ChangeScene("Akutan");
        _host.Tick(19);
        Assert.Empty(RecorderSession.Instances);
        _host.Tick();
        Assert.Single(RecorderSession.Instances);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void TransientNativeOrTerrainExceptionDoesNotKillMonitor(bool nativeFailure)
    {
        VTOLMPUtils.ThrowOnCheck = nativeFailure;
        VtolTerrainExporter.ThrowOnProbe = !nativeFailure;
        StartReadyMission();
        _host.Tick(20);
        Assert.Equal(1, _host.CoroutineCount);
        Assert.Empty(RecorderSession.Instances);
        Assert.Single(Logger.Messages, line => line.Contains("Monitoring will retry"));
        VTOLMPUtils.ThrowOnCheck = false;
        VtolTerrainExporter.ThrowOnProbe = false;
        _host.Tick();
        Assert.True(Assert.Single(RecorderSession.Instances).IsActive);
    }

    [Fact]
    public void UnknownScopeDuringNativeFailureStopsRecordingAndCanRecover()
    {
        StartReadyMission();
        VTOLMPUtils.ThrowOnCheck = true;
        _host.Tick();
        Assert.Equal(1, Assert.Single(RecorderSession.Instances).SuccessfulStops);
        VTOLMPUtils.ThrowOnCheck = false;
        _host.Tick();
        Assert.Equal(2, RecorderSession.Instances.Count);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void FailedSessionStartsRetryWithDelayAndRecover(bool throws)
    {
        if (throws) RecorderSession.StartThrowsRemaining = 2;
        else RecorderSession.StartFailuresRemaining = 2;
        StartReadyMission();
        Assert.Equal(1, Assert.Single(RecorderSession.Instances).DisposeCalls);
        _host.Tick(19);
        Assert.Single(RecorderSession.Instances);
        _host.Tick();
        Assert.Equal(2, RecorderSession.Instances.Count);
        Assert.Equal(1, RecorderSession.Instances[1].DisposeCalls);
        _host.Tick(20);
        Assert.Equal(3, RecorderSession.Instances.Count);
        Assert.True(RecorderSession.Instances[2].IsActive);
    }

    [Fact]
    public void PersistentStartFailureStopsAfterThreeAttemptsUntilNewMission()
    {
        RecorderSession.StartFailuresRemaining = 3;
        StartReadyMission();
        _host.Tick(200);
        Assert.Equal(3, RecorderSession.Instances.Count);
        Assert.All(RecorderSession.Instances, session => Assert.Equal(1, session.DisposeCalls));
        Assert.Contains(Logger.Messages, line => line.Contains("failed after 3 attempts"));
        VTScenario.current = new VTScenario();
        _host.Tick();
        Assert.Equal(4, RecorderSession.Instances.Count);
        Assert.True(RecorderSession.Instances[3].IsActive);
    }

    [Fact]
    public void WriterFaultRetainsFailureAndDoesNotOpenRepeatedFiles()
    {
        StartReadyMission();
        RecorderSession session = Assert.Single(RecorderSession.Instances);
        session.Fault();
        _host.Tick(200);
        Assert.Single(RecorderSession.Instances);
        Assert.Equal(1, session.DisposeCalls);
        Assert.Contains(Logger.Messages, line => line.Contains("incomplete ACMI") && line.Contains(".tmp"));
    }

    [Fact]
    public void ReplacingFlightSceneManagerDetachesOldExitHandler()
    {
        StartReadyMission();
        FlightSceneManager oldManager = FlightSceneManager.instance;
        FlightSceneManager.instance = new FlightSceneManager();
        _host.Tick();
        Assert.Equal(0, oldManager.SubscriberCount);
        Assert.Equal(1, FlightSceneManager.instance.SubscriberCount);
        oldManager.Exit();
        Assert.True(Assert.Single(RecorderSession.Instances).IsActive);
        FlightSceneManager.instance.Exit();
        Assert.False(RecorderSession.Instances[0].IsActive);
    }

    [Fact]
    public void DisposeFinalizesOnceAndDetachesEveryHandlerAndCoroutine()
    {
        StartReadyMission();
        RecorderSession session = Assert.Single(RecorderSession.Instances);
        _lifecycle.Dispose();
        _lifecycle.Dispose();
        _lifecycle.StartListening();
        Assert.Equal(1, session.SuccessfulStops);
        Assert.Equal(1, session.DisposeCalls);
        Assert.Equal(0, SceneManager.SubscriberCount);
        Assert.Equal(0, FlightSceneManager.instance.SubscriberCount);
        Assert.Equal(0, _host.CoroutineCount);
        FlightSceneManager.instance.Exit();
        SceneManager.ChangeScene("CustomMapBase");
        _host.Tick(20);
        Assert.Single(RecorderSession.Instances);
    }

    [Fact]
    public void DisposeDuringTerrainWaitClearsCacheAndPreventsLateStart()
    {
        VtolTerrainExporter.Available = false;
        StartReadyMission();
        int clearCalls = VtolTerrainSourceFactory.ClearCalls;
        _lifecycle.Dispose();
        VtolTerrainExporter.Available = true;
        _host.Tick(30);
        Assert.Empty(RecorderSession.Instances);
        Assert.True(VtolTerrainSourceFactory.ClearCalls > clearCalls);
    }

    [Theory]
    [InlineData("native")]
    [InlineData("unity")]
    [InlineData("unload")]
    public void FinalizationExceptionDoesNotEscapeGameCallbacks(string boundary)
    {
        StartReadyMission();
        RecorderSession session = Assert.Single(RecorderSession.Instances);
        session.ThrowOnStop = true;
        bool nextNativeSubscriberRan = false;
        FlightSceneManager.instance.OnExitScene += () => nextNativeSubscriberRan = true;
        switch (boundary)
        {
            case "native": FlightSceneManager.instance.Exit(); break;
            case "unity": SceneManager.ChangeScene("ReadyRoom"); break;
            case "unload": _lifecycle.Dispose(); break;
        }
        Assert.Equal(1, session.DisposeCalls);
        Assert.False(session.IsActive);
        Assert.Contains(Logger.Messages, line => line.Contains("mission cleanup failed"));
        if (boundary == "native") Assert.True(nextNativeSubscriberRan);
    }

    [Fact]
    public void NativeExitDuringTerrainWaitPreventsLateStartOfExitingMission()
    {
        VtolTerrainExporter.Available = false;
        StartReadyMission();
        _host.Tick(10);
        FlightSceneManager.instance.Exit();
        VtolTerrainExporter.Available = true;
        _host.Tick(30);
        Assert.Empty(RecorderSession.Instances);
        VTScenario.current = new VTScenario();
        _host.Tick();
        Assert.Single(RecorderSession.Instances);
    }

    [Fact]
    public void PersistentNativeErrorsAreThrottledWhileMonitoringContinues()
    {
        VTOLMPUtils.ThrowOnCheck = true;
        StartReadyMission();
        _host.Tick(80);
        Assert.Equal(3, Logger.Messages.Count(line => line.Contains("Monitoring will retry")));
        Assert.Equal(1, _host.CoroutineCount);
    }

    [Theory]
    [InlineData("Visible mission", "internal", "Visible mission")]
    [InlineData(" ", "internal", "internal")]
    [InlineData(null, null, "unnamed mission")]
    public void MissionNameUsesNativeNameThenIdThenFallback(string name, string id, string expected)
    {
        VTScenario.current.scenarioName = name;
        VTScenario.current.scenarioID = id;
        StartReadyMission();
        Assert.Equal(expected, Assert.Single(RecorderSession.Instances).MissionName);
    }

    private void StartReadyMission()
    {
        SceneManager.ChangeScene("Akutan", notify: false);
        _lifecycle.StartListening();
    }
}

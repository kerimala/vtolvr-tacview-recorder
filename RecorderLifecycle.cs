using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using VTOLVR.Multiplayer;

namespace VTOLVRTacviewSPRecorder.Main;

internal sealed class RecorderLifecycle : IDisposable
{
    private const float MissionStatePollSeconds = 0.25f;
    private const int MissionReadyWarningAttempts = 120;
    private const int TerrainDiscoveryMaxAttempts = 20;
    private const int SessionStartMaxAttempts = 3;
    private const double SessionStartRetrySeconds = 5.0;
    private const double LifecycleWarningIntervalSeconds = 10.0;

    private readonly MonoBehaviour _coroutineHost;
    private RecorderSession _activeSession;
    private IEnumerator _missionMonitorRoutine;
    private FlightSceneManager _flightSceneManager;
    private VTMapManager _pendingMapManager;
    private VTScenario _pendingScenario;
    private string _pendingScene;
    private VTMapManager _exitingMapManager;
    private VTScenario _exitingScenario;
    private bool _missionExiting;
    private int _missionReadyAttempts;
    private int _terrainDiscoveryAttempts;
    private int _sessionStartAttempts;
    private double _nextSessionStartTime;
    private double _nextLifecycleWarningTime;
    private bool _multiplayerSuppressionLogged;
    private bool _eventsSubscribed;
    private bool _disposed;

    public RecorderLifecycle(MonoBehaviour coroutineHost)
    {
        _coroutineHost = coroutineHost ?? throw new ArgumentNullException(nameof(coroutineHost));
    }

    public void StartListening()
    {
        if (_eventsSubscribed || _disposed)
        {
            return;
        }

        SceneManager.activeSceneChanged += OnActiveSceneChanged;
        _eventsSubscribed = true;
        Log("Recorder lifecycle is monitoring native VTOL mission state (VTOLAPI is not required).");
        _missionMonitorRoutine = MonitorMissionState();
        _coroutineHost.StartCoroutine(_missionMonitorRoutine);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        if (_eventsSubscribed)
        {
            SceneManager.activeSceneChanged -= OnActiveSceneChanged;
            _eventsSubscribed = false;
        }

        AttachFlightSceneManager(null);
        IEnumerator routine = _missionMonitorRoutine;
        _missionMonitorRoutine = null;
        if (routine != null)
        {
            try
            {
                _coroutineHost.StopCoroutine(routine);
            }
            catch (Exception ex)
            {
                LogWarn($"Failed to stop recorder mission monitor: {ex.Message}");
            }
        }

        EndMission("mod unloaded");
        ClearExitBoundary();
    }

    private IEnumerator MonitorMissionState()
    {
        var pollDelay = new WaitForSecondsRealtime(MissionStatePollSeconds);
        while (!_disposed && _eventsSubscribed)
        {
            try
            {
                CheckMissionState();
            }
            catch (Exception ex)
            {
                // A transient native-state failure must not kill the coroutine.
                // Stop recording while scope/readiness cannot be established.
                try
                {
                    StopSession("native mission-state check failed");
                }
                catch (Exception stopError)
                {
                    LogError($"Recorder cleanup after a mission-state failure failed: {stopError.Message}");
                }

                ResetPendingMission();
                if (Time.realtimeSinceStartupAsDouble >= _nextLifecycleWarningTime)
                {
                    _nextLifecycleWarningTime = Time.realtimeSinceStartupAsDouble + LifecycleWarningIntervalSeconds;
                    LogWarn($"Recorder mission-state check failed: {ex.GetType().Name}: {ex.Message}. Monitoring will retry.");
                }
            }

            yield return pollDelay;
        }

        _missionMonitorRoutine = null;
    }

    private void CheckMissionState()
    {
        AttachFlightSceneManager(FlightSceneManager.instance);
        string scene = SceneManager.GetActiveScene().name;
        if (!IsMissionScene(scene))
        {
            if (_pendingScene != null || _activeSession != null || _missionExiting)
            {
                StopSession($"left mission scene for {scene}");
                ResetPendingMission();
                ClearExitBoundary();
            }
            _multiplayerSuppressionLogged = false;
            return;
        }

        if (VTOLMPUtils.IsMultiplayer())
        {
            StopSession("multiplayer mission detected");
            ResetPendingMission();
            if (!_multiplayerSuppressionLogged)
            {
                Log("Recorder session start ignored: multiplayer mission.");
                _multiplayerSuppressionLogged = true;
            }
            return;
        }

        _multiplayerSuppressionLogged = false;
        VTMapManager mapManager = VTMapManager.fetch;
        VTScenario scenario = VTScenario.current;

        // OnExitScene fires before native objects are torn down. Do not reopen
        // the old, still-ready scenario during the exit fade or instant restart.
        if (_missionExiting)
        {
            if (ReferenceEquals(_exitingMapManager, mapManager) &&
                ReferenceEquals(_exitingScenario, scenario))
            {
                return;
            }
            ClearExitBoundary();
        }

        bool missionReady = mapManager != null && mapManager.map != null &&
            mapManager.scenarioReady && scenario != null && TargetManager.instance != null;
        if (_activeSession != null &&
            (!missionReady || !_activeSession.Matches(scene, mapManager, scenario)))
        {
            StopSession("native mission changed or became unavailable");
            ResetPendingMission();
        }

        if (!string.Equals(_pendingScene, scene, StringComparison.Ordinal) ||
            !ReferenceEquals(_pendingMapManager, mapManager) ||
            !ReferenceEquals(_pendingScenario, scenario))
        {
            ResetPendingMission();
            VtolTerrainSourceFactory.ClearMeshCache();
            _pendingScene = scene;
            _pendingMapManager = mapManager;
            _pendingScenario = scenario;
        }

        if (_activeSession != null)
        {
            // A writer fault retains its recoverable .tmp. Wait for a new
            // mission rather than repeatedly opening files on a failed drive.
            if (!_activeSession.IsActive)
            {
                StopSession("recording faulted");
                _sessionStartAttempts = SessionStartMaxAttempts;
            }
            return;
        }

        if (!missionReady)
        {
            if (++_missionReadyAttempts == MissionReadyWarningAttempts)
            {
                LogWarn($"Recorder is still waiting for native mission readiness in {scene} after " +
                    $"{MissionReadyWarningAttempts * MissionStatePollSeconds:0.#}s " +
                    $"(map={mapManager != null && mapManager.map != null}, " +
                    $"scenarioReady={mapManager != null && mapManager.scenarioReady}, " +
                    $"scenario={scenario != null}, actors={TargetManager.instance != null}). Monitoring continues.");
            }
            return;
        }

        _missionReadyAttempts = 0;
        if (_sessionStartAttempts >= SessionStartMaxAttempts ||
            Time.realtimeSinceStartupAsDouble < _nextSessionStartTime)
        {
            return;
        }

        if (!VtolTerrainExporter.IsTerrainSourceAvailable(mapManager, out string terrainSourceError))
        {
            if (++_terrainDiscoveryAttempts < TerrainDiscoveryMaxAttempts)
            {
                return;
            }
            if (_terrainDiscoveryAttempts == TerrainDiscoveryMaxAttempts)
            {
                LogWarn($"Tacview terrain source discovery did not complete after " +
                    $"{TerrainDiscoveryMaxAttempts * MissionStatePollSeconds:0.#}s: {terrainSourceError}. " +
                    "Recording will start without a MapId if the source remains unsupported.");
            }
        }

        _sessionStartAttempts++;
        if (!StartSession(scene, mapManager, scenario))
        {
            _nextSessionStartTime = Time.realtimeSinceStartupAsDouble + SessionStartRetrySeconds;
            LogWarn(_sessionStartAttempts < SessionStartMaxAttempts
                ? $"Recorder session start failed; retrying in {SessionStartRetrySeconds:0.#}s " +
                    $"(attempt {_sessionStartAttempts}/{SessionStartMaxAttempts})."
                : $"Recorder session start failed after {SessionStartMaxAttempts} attempts. Waiting for a new mission.");
        }
    }

    private void AttachFlightSceneManager(FlightSceneManager manager)
    {
        if (ReferenceEquals(_flightSceneManager, manager))
        {
            return;
        }

        if (!ReferenceEquals(_flightSceneManager, null))
        {
            _flightSceneManager.OnExitScene -= OnNativeMissionExit;
        }
        _flightSceneManager = manager;
        if (manager != null)
        {
            manager.OnExitScene += OnNativeMissionExit;
        }
    }

    private void OnNativeMissionExit()
    {
        _missionExiting = true;
        _exitingMapManager = VTMapManager.fetch;
        _exitingScenario = VTScenario.current;
        EndMission("native mission exit/restart");
    }

    private void OnActiveSceneChanged(Scene previous, Scene next)
    {
        EndMission($"Unity scene changed from {previous.name} to {next.name}");
        ClearExitBoundary();
    }

    private void EndMission(string reason)
    {
        try
        {
            StopSession(reason);
        }
        catch (Exception ex)
        {
            // Do not propagate recorder cleanup failures into the game's exit
            // callbacks or prevent other native event subscribers from running.
            LogError($"Recorder mission cleanup failed ({reason}): {ex}");
        }
        finally
        {
            ResetPendingMission();
        }
    }

    private void ClearExitBoundary()
    {
        _missionExiting = false;
        _exitingMapManager = null;
        _exitingScenario = null;
    }

    private void ResetPendingMission()
    {
        _pendingScene = null;
        _pendingMapManager = null;
        _pendingScenario = null;
        _missionReadyAttempts = 0;
        _terrainDiscoveryAttempts = 0;
        _sessionStartAttempts = 0;
        _nextSessionStartTime = 0;
    }

    private bool StartSession(string scene, VTMapManager mapManager, VTScenario scenario)
    {
        var session = new RecorderSession(_coroutineHost, scene, scenario, GetMissionName(scenario), mapManager);
        bool started = false;
        try
        {
            started = session.TryStart();
            if (started)
            {
                _activeSession = session;
            }
        }
        catch (Exception ex)
        {
            LogError($"Recorder session could not start: '{session.MissionName}': {ex}");
        }
        finally
        {
            VtolTerrainSourceFactory.ClearMeshCache();
            if (!started)
            {
                session.Dispose();
            }
        }

        if (started)
        {
            Log($"Recorder session started: '{session.MissionName}' ({scene}), output '{session.OutputPath}'.");
        }
        return started;
    }

    private void StopSession(string reason)
    {
        VtolTerrainSourceFactory.ClearMeshCache();
        RecorderSession session = _activeSession;
        _activeSession = null;
        if (session == null)
        {
            return;
        }

        try
        {
            bool stopped = session.TryStop();
            if (session.HasFinalizationFailure)
            {
                LogError($"Recorder session stopped with incomplete ACMI output: '{session.MissionName}' " +
                    $"({reason}), retained temporary path '{session.OutputPath}.tmp'.");
            }
            else if (stopped)
            {
                Log($"Recorder session stopped: '{session.MissionName}' ({reason}), output '{session.OutputPath}'.");
            }
        }
        finally
        {
            session.Dispose();
        }
    }

    private static bool IsMissionScene(string scene)
    {
        return scene == "Akutan" || scene == "CustomMapBase" || scene == "CustomMapBase_OverCloud";
    }

    private static string GetMissionName(VTScenario scenario)
    {
        if (!string.IsNullOrWhiteSpace(scenario.scenarioName))
        {
            return scenario.scenarioName;
        }
        return !string.IsNullOrWhiteSpace(scenario.scenarioID) ? scenario.scenarioID : "unnamed mission";
    }
}

using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using VTOLVR.Multiplayer;

namespace VTOLVRTacviewSPRecorder.Main;

internal sealed class RecorderLifecycle : IDisposable
{
    private const float MissionStatePollSeconds = 0.25f;
    // Campaign scenes can advertise scenarioReady before their static Unity
    // Terrain tiles become active. Keep this bounded and before writer/header
    // creation: a source which never appears still records cleanly without a
    // MapId instead of delaying a mission indefinitely.
    private const int TerrainDiscoveryMaxAttempts = 20;

    private readonly MonoBehaviour _coroutineHost;
    private RecorderSession _activeSession;
    private IEnumerator _missionMonitorRoutine;
    private bool _eventsSubscribed;
    private bool _disposed;

    public RecorderLifecycle(MonoBehaviour coroutineHost)
    {
        _coroutineHost = coroutineHost ?? throw new ArgumentNullException(nameof(coroutineHost));
    }

    public void StartListening()
    {
        if (_eventsSubscribed)
        {
            Log("Recorder lifecycle subscription ignored: already listening.");
            return;
        }

        if (_disposed)
        {
            LogWarn("Recorder lifecycle subscription ignored: lifecycle is disposed.");
            return;
        }

        SceneManager.activeSceneChanged += OnActiveSceneChanged;
        _eventsSubscribed = true;

        _missionMonitorRoutine = MonitorMissionState();
        _coroutineHost.StartCoroutine(_missionMonitorRoutine);
        Log("Recorder lifecycle is monitoring VTOL mission state.");
    }

    public void Dispose()
    {
        if (_disposed)
        {
            Log("Recorder lifecycle dispose ignored: already disposed.");
            return;
        }

        _disposed = true;

        if (_eventsSubscribed)
        {
            SceneManager.activeSceneChanged -= OnActiveSceneChanged;
            _eventsSubscribed = false;
        }

        StopMissionMonitor();
        StopSession("mod unloaded");
    }

    private void OnActiveSceneChanged(Scene previous, Scene next)
    {
        Log($"Unity scene changed from {previous.name} to {next.name}; mission-state monitoring continues.");
    }

    private IEnumerator MonitorMissionState()
    {
        var retryDelay = new WaitForSecondsRealtime(MissionStatePollSeconds);
        VTMapManager pendingMapManager = null;
        VTScenario pendingScenario = null;
        int terrainDiscoveryAttempts = 0;
        bool sessionStartAttempted = false;
        bool multiplayerSuppressionLogged = false;

        while (!_disposed && _eventsSubscribed)
        {
            VTMapManager mapManager = VTMapManager.fetch;
            VTScenario scenario = VTScenario.current;
            bool missionReady = mapManager != null
                && mapManager.map != null
                && mapManager.scenarioReady
                && scenario != null
                && TargetManager.instance != null;

            if (_activeSession != null)
            {
                if (!missionReady || !_activeSession.Matches(mapManager, scenario))
                {
                    StopSession("active VTOL mission changed or became unavailable");
                }
                else if (VTOLMPUtils.IsMultiplayer())
                {
                    StopSession("multiplayer mission detected");
                    Log("Recorder session stopped: multiplayer mission.");
                }
            }

            if (_activeSession != null || !missionReady)
            {
                pendingMapManager = null;
                pendingScenario = null;
                terrainDiscoveryAttempts = 0;
                sessionStartAttempted = false;
                multiplayerSuppressionLogged = false;
                yield return retryDelay;
                continue;
            }

            if (VTOLMPUtils.IsMultiplayer())
            {
                pendingMapManager = null;
                pendingScenario = null;
                terrainDiscoveryAttempts = 0;
                sessionStartAttempted = false;
                if (!multiplayerSuppressionLogged)
                {
                    Log("Recorder session start ignored: multiplayer mission.");
                    multiplayerSuppressionLogged = true;
                }
                yield return retryDelay;
                continue;
            }

            multiplayerSuppressionLogged = false;
            if (!ReferenceEquals(pendingMapManager, mapManager) ||
                !ReferenceEquals(pendingScenario, scenario))
            {
                pendingMapManager = mapManager;
                pendingScenario = scenario;
                terrainDiscoveryAttempts = 0;
                sessionStartAttempted = false;
            }

            if (sessionStartAttempted)
            {
                yield return retryDelay;
                continue;
            }

            if (VtolTerrainExporter.IsTerrainSourceAvailable(
                mapManager,
                out string terrainSourceError))
            {
                sessionStartAttempted = true;
                StartSession(mapManager, scenario);
            }
            else
            {
                terrainDiscoveryAttempts++;
                if (terrainDiscoveryAttempts >= TerrainDiscoveryMaxAttempts)
                {
                    LogWarn(
                        $"Tacview terrain source discovery did not complete after " +
                        $"{TerrainDiscoveryMaxAttempts * MissionStatePollSeconds:0.#}s: " +
                        $"{terrainSourceError}. Recording will start without a MapId if the source remains unsupported.");
                    sessionStartAttempted = true;
                    StartSession(mapManager, scenario);
                }
            }

            yield return retryDelay;
        }

        _missionMonitorRoutine = null;
    }

    private void StopMissionMonitor()
    {
        IEnumerator routine = _missionMonitorRoutine;
        _missionMonitorRoutine = null;
        if (routine == null)
        {
            return;
        }

        try
        {
            _coroutineHost.StopCoroutine(routine);
        }
        catch (Exception ex)
        {
            LogWarn($"Failed to stop recorder mission monitor: {ex.Message}");
        }
    }

    private void StartSession(VTMapManager mapManager, VTScenario scenario)
    {
        if (_activeSession != null)
        {
            if (_activeSession.Matches(mapManager, scenario))
            {
                Log($"Recorder session start ignored: '{_activeSession.MissionName}' is already active.");
                return;
            }

            StopSession("mission changed");
        }

        string missionName = GetMissionName(scenario);
        var session = new RecorderSession(_coroutineHost, scenario, missionName, mapManager);

        bool started;
        try
        {
            started = session.TryStart();
        }
        finally
        {
            // TryStart hands a successful source to the session-owned exporter.
            // On failure nothing should retain the 1.15M-triangle factory
            // snapshot. Either way the static handoff cache is no longer needed.
            VtolTerrainSourceFactory.ClearMeshCache();
        }

        if (!started)
        {
            session.Dispose();
            LogError($"Recorder session could not start: '{missionName}'.");
            return;
        }

        _activeSession = session;
        Log($"Recorder session started: '{missionName}', output '{session.OutputPath}'.");
    }

    private void StopSession(string reason)
    {
        VtolTerrainSourceFactory.ClearMeshCache();
        if (_activeSession == null)
        {
            Log($"Recorder session stop ignored: no active session ({reason}).");
            return;
        }

        RecorderSession session = _activeSession;
        _activeSession = null;

        if (!session.TryStop())
        {
            Log($"Recorder session stop ignored: '{session.MissionName}' is already stopped ({reason}).");
            session.Dispose();
            return;
        }

        if (session.HasFinalizationFailure)
        {
            LogError($"Recorder session stopped with incomplete ACMI output: '{session.MissionName}' " +
                $"({reason}), retained temporary path '{session.OutputPath}.tmp'.");
        }
        else
        {
            Log($"Recorder session stopped: '{session.MissionName}' ({reason}), output '{session.OutputPath}'.");
        }

        session.Dispose();
    }

    private static string GetMissionName(VTScenario scenario)
    {
        if (!string.IsNullOrWhiteSpace(scenario.scenarioName))
        {
            return scenario.scenarioName;
        }

        if (!string.IsNullOrWhiteSpace(scenario.scenarioID))
        {
            return scenario.scenarioID;
        }

        return "unnamed mission";
    }
}

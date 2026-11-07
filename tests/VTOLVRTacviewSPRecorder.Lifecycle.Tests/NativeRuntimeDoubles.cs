global using static VTOLVRTacviewSPRecorder.Main.Logger;
using System.Collections;

// These doubles model only the native members used by RecorderLifecycle.
// They do not load Unity, proprietary assemblies, VTOLAPI, or write ACMI files.
namespace UnityEngine
{
    public static class Time
    {
        public static double realtimeSinceStartupAsDouble;
    }

    public sealed class WaitForSecondsRealtime
    {
        public WaitForSecondsRealtime(float seconds) { Seconds = seconds; }
        public float Seconds { get; }
    }

    public sealed class Coroutine { }

    public sealed class MonoBehaviour
    {
        private readonly List<IEnumerator> _routines = new();
        public int CoroutineCount => _routines.Count;

        public Coroutine StartCoroutine(IEnumerator routine)
        {
            _routines.Add(routine);
            Advance(routine); // Unity runs through the first yield immediately.
            return new Coroutine();
        }

        public void StopCoroutine(IEnumerator routine) => _routines.Remove(routine);

        public void Tick(int count = 1)
        {
            for (int index = 0; index < count; index++)
            {
                Time.realtimeSinceStartupAsDouble += 0.25;
                foreach (IEnumerator routine in _routines.ToArray())
                {
                    Advance(routine);
                }
            }
        }

        private void Advance(IEnumerator routine)
        {
            try
            {
                if (!routine.MoveNext()) _routines.Remove(routine);
            }
            catch
            {
                _routines.Remove(routine);
                throw;
            }
        }
    }
}

namespace UnityEngine.SceneManagement
{
    public readonly struct Scene
    {
        public Scene(string name) { this.name = name; }
        public string name { get; }
    }

    public static class SceneManager
    {
        private static Scene _scene;
        public static event Action<Scene, Scene> activeSceneChanged;
        public static int SubscriberCount => activeSceneChanged?.GetInvocationList().Length ?? 0;
        public static Scene GetActiveScene() => _scene;
        public static void ChangeScene(string name, bool notify = true)
        {
            Scene previous = _scene;
            _scene = new Scene(name);
            if (notify) activeSceneChanged?.Invoke(previous, _scene);
        }
        public static void Reset() { activeSceneChanged = null; _scene = new Scene("ReadyRoom"); }
    }
}

namespace VTOLVR.Multiplayer
{
    public static class VTOLMPUtils
    {
        public static bool Multiplayer;
        public static bool ThrowOnCheck;
        public static bool IsMultiplayer()
        {
            if (ThrowOnCheck) throw new InvalidOperationException("synthetic native-state failure");
            return Multiplayer;
        }
    }
}

public sealed class FlightSceneManager
{
    public static FlightSceneManager instance;
    public event Action OnExitScene;
    public int SubscriberCount => OnExitScene?.GetInvocationList().Length ?? 0;
    public void Exit() => OnExitScene?.Invoke();
}

public sealed class VTMapManager
{
    public static VTMapManager fetch;
    public object map = new();
    public bool scenarioReady = true;
}

public sealed class VTScenario
{
    public static VTScenario current;
    public string scenarioName = "Synthetic mission";
    public string scenarioID = "synthetic-mission";
}

public sealed class TargetManager { public static TargetManager instance; }

namespace VTOLVRTacviewSPRecorder.Main
{
    internal static class Logger
    {
        public static readonly List<string> Messages = new();
        public static void Log(object message) => Messages.Add("INFO: " + message);
        public static void LogWarn(object message) => Messages.Add("WARN: " + message);
        public static void LogError(object message) => Messages.Add("ERROR: " + message);
    }

    internal static class VtolTerrainSourceFactory
    {
        public static int ClearCalls;
        public static void ClearMeshCache() => ClearCalls++;
    }

    internal static class VtolTerrainExporter
    {
        public static bool Available = true;
        public static bool ThrowOnProbe;
        public static int ProbeCalls;
        public static bool IsTerrainSourceAvailable(VTMapManager manager, out string error)
        {
            ProbeCalls++;
            if (ThrowOnProbe) throw new InvalidOperationException("synthetic terrain probe failure");
            error = Available ? null : "synthetic unsupported terrain";
            return Available;
        }
    }

    internal sealed class RecorderSession : IDisposable
    {
        public static readonly List<RecorderSession> Instances = new();
        public static int StartFailuresRemaining;
        public static int StartThrowsRemaining;
        private readonly string _scene;
        private readonly VTMapManager _mapManager;
        private readonly object _scenario;
        public bool IsActive { get; private set; }
        public bool HasFinalizationFailure { get; set; }
        public bool ThrowOnStop { get; set; }
        public string MissionName { get; }
        public string OutputPath => "synthetic.acmi";
        public int SuccessfulStops { get; private set; }
        public int DisposeCalls { get; private set; }

        public RecorderSession(UnityEngine.MonoBehaviour host, string scene, object scenario, string missionName, VTMapManager manager)
        {
            _scene = scene; _mapManager = manager; _scenario = scenario;
            MissionName = missionName;
            Instances.Add(this);
        }

        public bool Matches(string scene, VTMapManager manager, object scenario) =>
            _scene == scene && ReferenceEquals(_mapManager, manager) && ReferenceEquals(_scenario, scenario);

        public bool TryStart()
        {
            if (StartThrowsRemaining > 0)
            {
                StartThrowsRemaining--;
                throw new InvalidOperationException("synthetic session startup failure");
            }
            if (StartFailuresRemaining > 0) { StartFailuresRemaining--; return false; }
            IsActive = true;
            return true;
        }

        public bool TryStop()
        {
            if (!IsActive) return false;
            IsActive = false;
            SuccessfulStops++;
            if (ThrowOnStop) throw new InvalidOperationException("synthetic finalization failure");
            return true;
        }

        public void Fault() { IsActive = false; HasFinalizationFailure = true; }
        public void Dispose() { DisposeCalls++; TryStop(); }
    }
}

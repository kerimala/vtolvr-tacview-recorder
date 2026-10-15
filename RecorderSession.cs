using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using VTOLVRTacviewSPRecorder.Core;

namespace VTOLVRTacviewSPRecorder.Main;

internal sealed class RecorderSession : IDisposable
{
    private const float TickIntervalSeconds = 0.025f;

    private readonly MonoBehaviour _coroutineHost;
    private readonly VTMapManager _mapManager;
    private readonly VTScenario _scenario;
    private readonly List<EntitySamplingTarget> _samplingTargets =
        new List<EntitySamplingTarget>(128);
    private readonly List<Actor> _activeActors = new List<Actor>(64);
    private readonly List<EntityTransformUpdate> _transformUpdates =
        new List<EntityTransformUpdate>(128);
    private readonly List<TacviewEntityUpdate> _frameUpdates =
        new List<TacviewEntityUpdate>(128);
    private readonly List<TacviewEvent> _frameEvents = new List<TacviewEvent>(16);
    private IEnumerator _samplingRoutine;
    private TacviewEntityWriter _writer;
    private EntityRegistry _entityRegistry;
    private EntitySampleScheduler _sampleScheduler;
    private EntityFrameCollector _frameCollector;
    private WarningThrottle _warningThrottle;
    private VtolEntityRegistryAdapter _entityAdapter;
    private VtolWeaponRegistryAdapter _weaponAdapter;
    private VtolGunTracerAdapter _gunTracerAdapter;
    private VtolCountermeasureRegistryAdapter _countermeasureAdapter;
    private VtolMissionContextAdapter _missionContextAdapter;
    private VtolTerrainExporter _terrainExporter;
    private string _terrainMapId;
    private double _sessionStartRealtime;
    private bool _isActive;

    public RecorderSession(
        MonoBehaviour coroutineHost,
        object scenarioToken,
        string missionName,
        VTMapManager mapManager)
    {
        _coroutineHost = coroutineHost ?? throw new ArgumentNullException(nameof(coroutineHost));
        ScenarioToken = scenarioToken ?? throw new ArgumentNullException(nameof(scenarioToken));
        MissionName = missionName ?? throw new ArgumentNullException(nameof(missionName));
        _mapManager = mapManager ?? throw new ArgumentNullException(nameof(mapManager));
        _scenario = scenarioToken as VTScenario ??
            throw new ArgumentException("The scenario token must be the active VTScenario.", nameof(scenarioToken));
    }

    public bool IsActive => _isActive;

    public string MissionName { get; }

    public string OutputPath { get; private set; }

    public bool HasFinalizationFailure { get; private set; }

    private object ScenarioToken { get; }

    public bool Matches(VTMapManager mapManager, object scenarioToken)
    {
        return ReferenceEquals(_mapManager, mapManager)
            && ReferenceEquals(ScenarioToken, scenarioToken);
    }

    public bool TryStart()
    {
        if (_isActive)
        {
            return false;
        }

        try
        {
            DateTimeOffset startUtc = DateTimeOffset.UtcNow;
            string outputDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "Tacview",
                "VTOL VR");

            _entityRegistry = new EntityRegistry();
            _sampleScheduler = new EntitySampleScheduler();
            _frameCollector = new EntityFrameCollector(_sampleScheduler);
            _warningThrottle = new WarningThrottle(10.0);
            _entityAdapter = new VtolEntityRegistryAdapter(_mapManager, _entityRegistry);
            _weaponAdapter = new VtolWeaponRegistryAdapter(_mapManager, _entityRegistry);
            _entityAdapter.ActorTracked += _weaponAdapter.TrackOwner;
            _entityAdapter.ActorRemoved += _weaponAdapter.UntrackOwner;
            try
            {
                _gunTracerAdapter = new VtolGunTracerAdapter(
                    _mapManager,
                    _entityRegistry);
                _entityAdapter.ActorTracked += _gunTracerAdapter.TrackOwner;
                _entityAdapter.ActorRemoved += _gunTracerAdapter.UntrackOwner;
            }
            catch (Exception ex)
            {
                _gunTracerAdapter = null;
                LogWarn($"Gun tracer capture is unavailable for this session: {ex}");
            }
            try
            {
                _countermeasureAdapter = new VtolCountermeasureRegistryAdapter(
                    _mapManager,
                    _entityRegistry);
                _entityAdapter.ActorTracked += _countermeasureAdapter.TrackOwner;
                _entityAdapter.ActorRemoved += _countermeasureAdapter.UntrackOwner;
            }
            catch (Exception ex)
            {
                _countermeasureAdapter = null;
                LogWarn($"Countermeasure capture is unavailable for this session: {ex}");
            }
            _missionContextAdapter = new VtolMissionContextAdapter(
                _mapManager,
                _scenario,
                _entityRegistry);

            VtolTerrainExporter.TryPrepare(
                _coroutineHost,
                _mapManager,
                _scenario,
                out _terrainExporter,
                out _terrainMapId,
                out string terrainPreparationError);
            if (_terrainExporter == null)
            {
                LogWarn(
                    $"Tacview terrain companion is unavailable: {terrainPreparationError}. " +
                    "Recording will continue without a MapId rather than reference a missing terrain.");
            }

            _sessionStartRealtime = Time.realtimeSinceStartupAsDouble;
            _entityAdapter.Refresh(_sessionStartRealtime);
            _missionContextAdapter.RegisterStaticContext();
            TacviewRecordingMetadata recordingMetadata =
                _missionContextAdapter.BuildRecordingMetadata(_terrainMapId);
            _writer = TacviewEntityWriter.CreateFile(
                outputDirectory,
                MissionName,
                startUtc,
                recordingMetadata);
            OutputPath = _writer.FinalPath;
            _isActive = true;

            _samplingRoutine = SampleEntities();
            _coroutineHost.StartCoroutine(_samplingRoutine);
            StartTerrainExportSafely();
            return true;
        }
        catch (Exception ex)
        {
            LogError($"Failed to initialize recorder session '{MissionName}': {ex}");
            _isActive = false;
            StopTerrainExportSafely();
            DisposeEntityTracking();
            AbortWriterSafely();
            return false;
        }
    }

    public bool TryStop()
    {
        if (!_isActive)
        {
            return false;
        }

        _isActive = false;
        FinalizeTerrainMapIdDecisionSafely();
        StopTerrainExportSafely();
        IEnumerator routine = _samplingRoutine;
        _samplingRoutine = null;

        if (routine != null)
        {
            try
            {
                _coroutineHost.StopCoroutine(routine);
            }
            catch (Exception ex)
            {
                LogWarn($"Failed to stop entity sampling coroutine: {ex.Message}");
            }
        }

        if (!DrainPendingLifecycleSafely())
        {
            DisposeEntityTracking();
            HasFinalizationFailure = true;
            AbortWriterSafely();
            return true;
        }

        DisposeEntityTracking();
        HasFinalizationFailure = !CloseWriterSafely();
        return true;
    }

    public void Dispose()
    {
        TryStop();
        StopTerrainExportSafely();
        DisposeEntityTracking();
        CloseWriterSafely();
    }

    private IEnumerator SampleEntities()
    {
        var tickDelay = new WaitForSecondsRealtime(TickIntervalSeconds);

        while (_isActive)
        {
            double now = Time.realtimeSinceStartupAsDouble;
            _samplingTargets.Clear();
            _activeActors.Clear();
            _transformUpdates.Clear();
            _frameUpdates.Clear();
            _frameEvents.Clear();
            bool ownerSnapshotComplete = false;

            try
            {
                _entityAdapter.Refresh(now);
                _entityAdapter.DrainFrameDataTo(_frameUpdates);
                _entityAdapter.CopyActiveTargetsTo(_samplingTargets);
                if (_weaponAdapter.NeedsOwnerReconciliation(now) ||
                    (_gunTracerAdapter != null && _gunTracerAdapter.NeedsOwnerReconciliation(now)))
                {
                    _entityAdapter.CopyActiveActorsTo(_activeActors);
                    ownerSnapshotComplete = true;
                }
            }
            catch (Exception ex)
            {
                LogRefreshWarning(now, $"Actor registry refresh failed: {ex.Message}");
            }

            try
            {
                _weaponAdapter.Refresh(now, _activeActors, ownerSnapshotComplete);
                _weaponAdapter.DrainFrameDataTo(_frameUpdates, _frameEvents);
                _weaponAdapter.AppendSamplingTargetsTo(_samplingTargets);
            }
            catch (Exception ex)
            {
                LogRefreshWarning(now, $"Weapon registry refresh failed: {ex.Message}");
            }

            if (_gunTracerAdapter != null)
            {
                try
                {
                    _gunTracerAdapter.Refresh(now, _activeActors, ownerSnapshotComplete);
                    _gunTracerAdapter.DrainFrameDataTo(_frameUpdates);
                }
                catch (Exception ex)
                {
                    LogRefreshWarning(now, $"Gun tracer registry refresh failed: {ex.Message}");
                }
            }

            if (_countermeasureAdapter != null)
            {
                try
                {
                    _countermeasureAdapter.Refresh(now);
                    _countermeasureAdapter.DrainFrameDataTo(_frameUpdates);
                }
                catch (Exception ex)
                {
                    LogRefreshWarning(now, $"Countermeasure registry refresh failed: {ex.Message}");
                }
            }

            _missionContextAdapter.AppendSamplingTargetsTo(_samplingTargets);
            ForgetRemovedEntities();

            _frameCollector.Collect(
                now,
                _samplingTargets,
                _transformUpdates,
                (entity, reason) => LogSkippedEntity(now, entity, reason));

            for (int index = 0; index < _transformUpdates.Count; index++)
            {
                EntityTransformUpdate update = _transformUpdates[index];
                _frameUpdates.Add(TacviewEntityUpdate.Upsert(
                    update.Entity.ObjectId,
                    update.Transform,
                    update.Entity.Metadata));
            }

            if (_frameUpdates.Count > 0 || _frameEvents.Count > 0)
            {
                try
                {
                    _writer.WriteFrame(
                        now - _sessionStartRealtime,
                        _frameUpdates,
                        _frameEvents);
                }
                catch (Exception ex)
                {
                    Fault($"ACMI batch write failed: {ex}");
                    yield break;
                }
            }

            yield return tickDelay;
        }

        _samplingRoutine = null;
    }

    private void ForgetRemovedEntities()
    {
        for (int index = 0; index < _frameUpdates.Count; index++)
        {
            TacviewEntityUpdate update = _frameUpdates[index];
            if (!update.IsDelete)
            {
                continue;
            }

            _frameCollector.Forget(update.ObjectId);
            _warningThrottle.Forget(update.ObjectId);
        }
    }

    private void Fault(string message)
    {
        if (!_isActive)
        {
            return;
        }

        _isActive = false;
        _samplingRoutine = null;
        StopTerrainExportSafely();
        LogError($"Recorder session faulted: {message}");
        DisposeEntityTracking();
        HasFinalizationFailure = true;
        AbortWriterSafely();
    }

    private bool DrainPendingLifecycleSafely()
    {
        if (_writer == null || _entityAdapter == null || _weaponAdapter == null)
        {
            return true;
        }

        double stopTime = Time.realtimeSinceStartupAsDouble;
        try
        {
            _entityAdapter.PrepareFinalDrain();
            _countermeasureAdapter?.PrepareFinalDrain(stopTime);

            // Actors and weapons need two distinct frames for a final snapshot
            // and Delete/Event. A short countermeasure or gun tracer can
            // require Spawn, final snapshot, and Delete, so retain one
            // additional bounded pass plus one safety pass.
            int drainPasses = _countermeasureAdapter == null && _gunTracerAdapter == null ? 2 : 4;
            for (int pass = 0; pass < drainPasses; pass++)
            {
                _frameUpdates.Clear();
                _frameEvents.Clear();
                _entityAdapter.DrainFrameDataTo(_frameUpdates);
                _weaponAdapter.PrepareFinalDrain(
                    stopTime,
                    settleUnavailable: pass == 0);
                _weaponAdapter.DrainFrameDataTo(_frameUpdates, _frameEvents);
                _gunTracerAdapter?.PrepareFinalDrain();
                _gunTracerAdapter?.DrainFrameDataTo(_frameUpdates);
                _countermeasureAdapter?.DrainFrameDataTo(_frameUpdates);

                if (_frameUpdates.Count == 0 && _frameEvents.Count == 0)
                {
                    continue;
                }

                _writer.WriteFrame(
                    Math.Max(0.0, stopTime - _sessionStartRealtime) + (pass * 0.001),
                    _frameUpdates,
                    _frameEvents);
            }

            return true;
        }
        catch (Exception ex)
        {
            LogError($"Failed to drain final ACMI lifecycle data: {ex}");
            return false;
        }
        finally
        {
            _frameUpdates.Clear();
            _frameEvents.Clear();
        }
    }

    private void LogSkippedEntity(double now, TrackedEntity entity, string reason)
    {
        WarningKind warningKind = reason.IndexOf("non-finite", StringComparison.OrdinalIgnoreCase) >= 0
            ? WarningKind.NonFiniteTransform
            : reason.IndexOf("unavailable", StringComparison.OrdinalIgnoreCase) >= 0
                ? WarningKind.SourceUnavailable
                : WarningKind.SourceFault;
        if (!_warningThrottle.ShouldLog(entity.ObjectId, warningKind, now))
        {
            return;
        }

        LogWarn($"{entity.Kind} sample skipped ({entity.ObjectId:X}, " +
            $"'{entity.Metadata.Name}'): {reason}.");
    }

    private void LogRefreshWarning(double now, string message)
    {
        if (_warningThrottle.ShouldLog(0, WarningKind.Refresh, now))
        {
            LogWarn(message);
        }
    }

    private void DisposeEntityTracking()
    {
        if (_entityAdapter != null)
        {
            if (_weaponAdapter != null)
            {
                _entityAdapter.ActorTracked -= _weaponAdapter.TrackOwner;
                _entityAdapter.ActorRemoved -= _weaponAdapter.UntrackOwner;
            }

            if (_gunTracerAdapter != null)
            {
                _entityAdapter.ActorTracked -= _gunTracerAdapter.TrackOwner;
                _entityAdapter.ActorRemoved -= _gunTracerAdapter.UntrackOwner;
            }

            if (_countermeasureAdapter != null)
            {
                _entityAdapter.ActorTracked -= _countermeasureAdapter.TrackOwner;
                _entityAdapter.ActorRemoved -= _countermeasureAdapter.UntrackOwner;
            }
        }

        _countermeasureAdapter?.Dispose();
        _countermeasureAdapter = null;
        _gunTracerAdapter?.Dispose();
        _gunTracerAdapter = null;
        _weaponAdapter?.Dispose();
        _weaponAdapter = null;
        _entityAdapter?.Dispose();
        _entityAdapter = null;
        _missionContextAdapter?.Clear();
        _missionContextAdapter = null;
        _frameCollector?.Clear();
        _frameCollector = null;
        _sampleScheduler = null;
        _warningThrottle?.Clear();
        _warningThrottle = null;
        _entityRegistry?.Clear();
        _entityRegistry = null;
        _samplingTargets.Clear();
        _activeActors.Clear();
        _transformUpdates.Clear();
        _frameUpdates.Clear();
        _frameEvents.Clear();
    }

    private void StartTerrainExportSafely()
    {
        if (_terrainExporter == null)
        {
            return;
        }

        try
        {
            _terrainExporter.Start();
        }
        catch (Exception ex)
        {
            LogWarn($"Tacview terrain companion could not start: {ex}. ACMI recording continues.");
            StopTerrainExportSafely(clearMapId: false);
        }
    }

    private void FinalizeTerrainMapIdDecisionSafely()
    {
        if (string.IsNullOrWhiteSpace(_terrainMapId) || _writer == null)
        {
            return;
        }

        if (_terrainExporter == null)
        {
            LogWarn(
                $"Tacview terrain '{_terrainMapId}' did not start. " +
                "The finalized ACMI will omit MapId so it cannot select missing terrain.");
            return;
        }

        try
        {
            if (!_terrainExporter.IsTerrainReady)
            {
                LogWarn(
                    $"Tacview terrain '{_terrainExporter.MapId}' was not ready when the mission ended. " +
                    "The finalized ACMI will omit MapId so it cannot select missing terrain.");
                return;
            }

            // Verify after the worker has completed and before disposing the
            // Unity coroutine. No worker writes remain once IsTerrainReady is true.
            if (!_terrainExporter.VerifyTerrainReadyOnDisk())
            {
                LogWarn(
                    $"Tacview terrain '{_terrainMapId}' changed before ACMI finalization. " +
                    "The finalized ACMI will omit the unverified MapId.");
                return;
            }

            _writer.ConfirmMapId();
        }
        catch (Exception ex)
        {
            LogWarn(
                $"Tacview terrain MapId could not be confirmed: {ex.Message}. " +
                "The finalized ACMI will omit the unverified MapId.");
        }
    }

    private void StopTerrainExportSafely(bool clearMapId = true)
    {
        if (_terrainExporter == null)
        {
            if (clearMapId)
            {
                _terrainMapId = null;
            }

            return;
        }

        VtolTerrainExporter exporter = _terrainExporter;
        _terrainExporter = null;
        if (clearMapId)
        {
            _terrainMapId = null;
        }
        try
        {
            exporter.Dispose();
        }
        catch (Exception ex)
        {
            LogWarn($"Failed to stop Tacview terrain export: {ex.Message}");
        }
    }

    private bool CloseWriterSafely()
    {
        if (_writer == null)
        {
            return true;
        }

        TacviewEntityWriter writer = _writer;
        _writer = null;

        try
        {
            writer.Dispose();
            OutputPath = writer.FinalPath;
            return true;
        }
        catch (Exception ex)
        {
            LogError($"Failed to finalize ACMI file '{OutputPath}': {ex}");
            return false;
        }
    }

    private void AbortWriterSafely()
    {
        if (_writer == null)
        {
            return;
        }

        TacviewEntityWriter writer = _writer;
        _writer = null;

        try
        {
            writer.Abort();
        }
        catch (Exception ex)
        {
            LogError($"Failed to close incomplete ACMI file '{OutputPath}': {ex}");
        }
    }
}

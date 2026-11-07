using System;
using System.Collections.Generic;
using UnityEngine;
using VTOLVRTacviewSPRecorder.Core;

namespace VTOLVRTacviewSPRecorder.Main;

internal sealed class VtolEntityRegistryAdapter : IDisposable
{
    private const double RefreshIntervalSeconds = 2.0;
    private const double UnregistrationGraceSeconds = 0.25;

    private readonly VTMapManager _mapManager;
    private readonly EntityRegistry _registry;
    private readonly Dictionary<object, TrackedSource> _sources =
        new Dictionary<object, TrackedSource>(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<object, PendingUnregistration> _pendingUnregistrations =
        new Dictionary<object, PendingUnregistration>(ReferenceEqualityComparer.Instance);
    private readonly List<EntityRemovalSequence> _pendingRemovals =
        new List<EntityRemovalSequence>();
    private readonly List<Actor> _actorsToRemove = new List<Actor>();
    private TargetManager _targetManager;
    private double _nextRefreshTime;
    private double _nextWarningTime;
    private bool _disposed;

    public event Action<Actor> ActorTracked;

    public event Action<Actor> ActorRemoved;

    public VtolEntityRegistryAdapter(VTMapManager mapManager, EntityRegistry registry)
    {
        _mapManager = mapManager ?? throw new ArgumentNullException(nameof(mapManager));
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        Actor.OnActorKilled += OnActorKilled;
    }

    public void Refresh(double nowSeconds)
    {
        if (_disposed)
        {
            return;
        }

        foreach (TrackedSource source in _sources.Values)
        {
            source.BeginFrame();
        }

        if (nowSeconds >= _nextRefreshTime)
        {
            _nextRefreshTime = nowSeconds + RefreshIntervalSeconds;
            AttachTargetManager(TargetManager.instance);
            TrackPlayerActor();

            if (_targetManager != null)
            {
                List<Actor> actors = _targetManager.allActors;
                for (int index = 0; index < actors.Count; index++)
                {
                    TrackActor(actors[index]);
                }
            }

            RemoveUnavailableSources();
        }

        ProcessPendingUnregistrations(nowSeconds);
    }

    public void CopyActiveTargetsTo(List<EntitySamplingTarget> destination)
    {
        if (destination == null)
        {
            throw new ArgumentNullException(nameof(destination));
        }

        destination.Clear();
        foreach (TrackedSource source in _sources.Values)
        {
            if (source.CanSample)
            {
                destination.Add(new EntitySamplingTarget(source.Entity, source.Source));
            }
        }
    }

    public void PrepareFinalDrain()
    {
        if (_disposed)
        {
            return;
        }

        _actorsToRemove.Clear();
        foreach (PendingUnregistration pending in _pendingUnregistrations.Values)
        {
            _actorsToRemove.Add(pending.Source.Actor);
        }

        for (int index = 0; index < _actorsToRemove.Count; index++)
        {
            RemoveActor(_actorsToRemove[index], TacviewEntityRemovalReason.Deleted);
        }

        _actorsToRemove.Clear();
        foreach (TrackedSource source in _sources.Values)
        {
            if (!source.Source.IsAvailable)
            {
                _actorsToRemove.Add(source.Actor);
            }
        }

        for (int index = 0; index < _actorsToRemove.Count; index++)
        {
            RemoveActor(_actorsToRemove[index], TacviewEntityRemovalReason.Deleted);
        }

        _actorsToRemove.Clear();
    }

    public void CopyActiveActorsTo(List<Actor> destination)
    {
        if (destination == null)
        {
            throw new ArgumentNullException(nameof(destination));
        }

        destination.Clear();
        foreach (TrackedSource source in _sources.Values)
        {
            if (source.Actor != null)
            {
                destination.Add(source.Actor);
            }
        }
    }

    public void DrainFrameDataTo(List<TacviewEntityUpdate> destination)
    {
        if (destination == null)
        {
            throw new ArgumentNullException(nameof(destination));
        }

        foreach (TrackedSource source in _sources.Values)
        {
            if (source.TryQueueMandatorySnapshot(out EntityTransform transform))
            {
                destination.Add(TacviewEntityUpdate.Upsert(
                    source.Entity.ObjectId,
                    transform,
                    source.Entity.Metadata));
            }
        }

        int writeIndex = 0;
        for (int index = 0; index < _pendingRemovals.Count; index++)
        {
            EntityRemovalSequence removal = _pendingRemovals[index];
            if (removal.TryDequeue(out TacviewEntityUpdate update))
            {
                destination.Add(update);
            }

            if (removal.WasSuppressed)
            {
                WarnThrottled(
                    $"{removal.Entity.Kind} {removal.Entity.ObjectId:X} ended before a valid ACMI snapshot; its delete is suppressed.");
            }

            if (!removal.IsComplete)
            {
                _pendingRemovals[writeIndex++] = removal;
            }
        }

        if (writeIndex == 0)
        {
            _pendingRemovals.Clear();
        }
        else if (writeIndex < _pendingRemovals.Count)
        {
            _pendingRemovals.RemoveRange(writeIndex, _pendingRemovals.Count - writeIndex);
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        AttachTargetManager(null);
        Actor.OnActorKilled -= OnActorKilled;

        foreach (TrackedSource source in _sources.Values)
        {
            Unsubscribe(source);
        }

        _sources.Clear();
        _pendingUnregistrations.Clear();
        _pendingRemovals.Clear();
        _actorsToRemove.Clear();
        ActorTracked = null;
        ActorRemoved = null;
    }

    private void AttachTargetManager(TargetManager targetManager)
    {
        if (ReferenceEquals(_targetManager, targetManager))
        {
            return;
        }

        if (_targetManager != null)
        {
            _targetManager.OnRegisteredActor -= OnActorRegistered;
            _targetManager.OnUnregisteredActor -= OnActorUnregistered;
        }

        _targetManager = targetManager;

        if (_targetManager != null)
        {
            _targetManager.OnRegisteredActor += OnActorRegistered;
            _targetManager.OnUnregisteredActor += OnActorUnregistered;
        }
    }

    private void OnActorRegistered(Actor actor)
    {
        TrackActor(actor);
    }

    private void OnActorUnregistered(Actor actor)
    {
        if (!ReferenceEquals(actor, null) && _sources.TryGetValue(actor, out TrackedSource source))
        {
            _pendingUnregistrations[actor] = new PendingUnregistration(
                source,
                Time.realtimeSinceStartupAsDouble + UnregistrationGraceSeconds);
        }
    }

    private void TrackPlayerActor()
    {
        FlightSceneManager manager = FlightSceneManager.instance;
        if (manager != null)
        {
            TrackActor(manager.playerActor, true);
        }
    }

    private void TrackActor(Actor actor, bool isOwnshipHint = false)
    {
        bool isOwnship = actor != null && (isOwnshipHint || actor.isPlayer);
        if (!VtolActorClassifier.TryClassify(
            actor,
            isOwnship,
            out EntityKind kind,
            out SamplingClass _))
        {
            return;
        }

        try
        {
            EntityMetadata metadata = VtolActorClassifier.BuildMetadata(actor, kind, isOwnship);
            if (_sources.TryGetValue(actor, out TrackedSource existing))
            {
                _pendingUnregistrations.Remove(actor);

                if (existing.Entity.Kind != kind)
                {
                    RemoveActor(actor, TacviewEntityRemovalReason.Deleted);
                }
                else if (isOwnship && !existing.Entity.IsOwnship)
                {
                    if (_registry.PromoteToOwnship(
                        actor,
                        metadata,
                        out TrackedEntity previous,
                        out TrackedEntity promoted))
                    {
                        bool previousSnapshotEmitted = existing.InitialSnapshotEmitted;
                        EntityTransform? previousTransform = existing.CaptureCurrentTransform();
                        existing.ReplaceEntity(promoted);
                        _pendingRemovals.Add(new EntityRemovalSequence(
                            previous,
                            TacviewEntityRemovalReason.Deleted,
                            GetRemovalLabel(previous),
                            previousTransform ?? existing.InitialTransform,
                            previousSnapshotEmitted));
                        Log($"Ownship promoted: {promoted.ObjectId:X} '{promoted.Metadata.Name}' " +
                            $"(replaced {previous.ObjectId:X}).");
                    }

                    return;
                }
                else
                {
                    _registry.Register(actor, kind, existing.Entity.IsOwnship, metadata);
                    return;
                }
            }

            TrackedEntity tracked = _registry.Register(actor, kind, isOwnship, metadata);
            Action<Teams> teamHandler = _ => RefreshMetadata(actor);
            Action<Actor.Designation> designationHandler = _ => RefreshMetadata(actor);
            actor.OnSetTeam += teamHandler;
            actor.OnSetDesignation += designationHandler;
            var source = new VtolEntitySource(
                actor.transform,
                _mapManager,
                () => actor != null && actor.alive && actor.isActiveAndEnabled);
            _sources.Add(actor, new TrackedSource(
                tracked,
                actor,
                source,
                teamHandler,
                designationHandler,
                CaptureCurrentTransform(source)));
            NotifyActor(ActorTracked, actor, "tracked");
            Log($"{kind} registered: {tracked.ObjectId:X} '{tracked.Metadata.Name}' " +
                $"({tracked.Metadata.Coalition}).");
        }
        catch (Exception ex)
        {
            WarnThrottled($"Entity registry skipped actor '{SafeActorName(actor)}': {ex.Message}");
        }
    }

    private void RefreshMetadata(Actor actor)
    {
        if (ReferenceEquals(actor, null) || !_sources.TryGetValue(actor, out TrackedSource source))
        {
            return;
        }

        try
        {
            _registry.Register(
                actor,
                source.Entity.Kind,
                source.Entity.IsOwnship,
                VtolActorClassifier.BuildMetadata(
                    actor,
                    source.Entity.Kind,
                    source.Entity.IsOwnship));
        }
        catch (Exception ex)
        {
            WarnThrottled($"Entity metadata refresh skipped for '{SafeActorName(actor)}': {ex.Message}");
        }
    }

    private void OnActorKilled(Actor actor)
    {
        RemoveActor(actor, TacviewEntityRemovalReason.Destroyed);
    }

    private void ProcessPendingUnregistrations(double nowSeconds)
    {
        if (_pendingUnregistrations.Count == 0)
        {
            return;
        }

        _actorsToRemove.Clear();
        foreach (KeyValuePair<object, PendingUnregistration> pending in _pendingUnregistrations)
        {
            if (nowSeconds >= pending.Value.RemoveAfterSeconds)
            {
                _actorsToRemove.Add(pending.Value.Source.Actor);
            }
        }

        for (int index = 0; index < _actorsToRemove.Count; index++)
        {
            RemoveActor(_actorsToRemove[index], TacviewEntityRemovalReason.Deleted);
        }

        _actorsToRemove.Clear();
    }

    private void RemoveUnavailableSources()
    {
        _actorsToRemove.Clear();
        foreach (TrackedSource source in _sources.Values)
        {
            if (!source.Source.IsAvailable)
            {
                _actorsToRemove.Add(source.Actor);
            }
        }

        for (int index = 0; index < _actorsToRemove.Count; index++)
        {
            RemoveActor(_actorsToRemove[index], TacviewEntityRemovalReason.Deleted);
        }

        _actorsToRemove.Clear();
    }

    private void RemoveActor(Actor actor, TacviewEntityRemovalReason reason)
    {
        if (ReferenceEquals(actor, null) || !_sources.TryGetValue(actor, out TrackedSource source))
        {
            return;
        }

        Unsubscribe(source);
        _sources.Remove(actor);
        _pendingUnregistrations.Remove(actor);
        NotifyActor(ActorRemoved, actor, "removed");

        if (_registry.Remove(actor, out TrackedEntity removed))
        {
            EntityTransform? finalTransform = source.CaptureCurrentTransform();
            _pendingRemovals.Add(new EntityRemovalSequence(
                removed,
                reason,
                GetRemovalLabel(removed),
                source.InitialSnapshotEmitted
                    ? finalTransform
                    : finalTransform ?? source.InitialTransform,
                source.InitialSnapshotEmitted));
            Log($"{removed.Kind} removed: {removed.ObjectId:X} '{removed.Metadata.Name}' ({reason}).");
        }
    }

    private static EntityTransform? CaptureCurrentTransform(VtolEntitySource source)
    {
        try
        {
            return source.TryGetCurrentTransform(out EntityTransform transform) &&
                transform.HasFiniteValues
                ? transform
                : (EntityTransform?)null;
        }
        catch
        {
            return null;
        }
    }

    private static string GetRemovalLabel(TrackedEntity entity)
    {
        return VtolActorClassifier.FirstNonEmpty(
            entity.Metadata.CallSign,
            entity.Metadata.Name,
            entity.Kind.ToString());
    }

    private void NotifyActor(Action<Actor> callback, Actor actor, string lifecycle)
    {
        if (callback == null)
        {
            return;
        }

        try
        {
            callback(actor);
        }
        catch (Exception ex)
        {
            WarnThrottled($"Actor {lifecycle} subscriber failed for '{SafeActorName(actor)}': {ex.Message}");
        }
    }

    private static void Unsubscribe(TrackedSource source)
    {
        if (source.Actor == null)
        {
            return;
        }

        source.Actor.OnSetTeam -= source.TeamHandler;
        source.Actor.OnSetDesignation -= source.DesignationHandler;
    }

    private static string SafeActorName(Actor actor)
    {
        return actor == null
            ? "<destroyed>"
            : VtolActorClassifier.FirstNonEmpty(actor.actorName, actor.name, "<unnamed>");
    }

    private void WarnThrottled(string message)
    {
        double now = Time.realtimeSinceStartupAsDouble;
        if (now < _nextWarningTime)
        {
            return;
        }

        _nextWarningTime = now + 10.0;
        LogWarn(message);
    }

    private sealed class TrackedSource
    {
        public TrackedSource(
            TrackedEntity entity,
            Actor actor,
            VtolEntitySource source,
            Action<Teams> teamHandler,
            Action<Actor.Designation> designationHandler,
            EntityTransform? initialTransform)
        {
            Entity = entity ?? throw new ArgumentNullException(nameof(entity));
            Actor = actor;
            Source = source ?? throw new ArgumentNullException(nameof(source));
            TeamHandler = teamHandler;
            DesignationHandler = designationHandler;
            InitialTransform = initialTransform;
        }

        public TrackedEntity Entity { get; private set; }

        public Actor Actor { get; }

        public VtolEntitySource Source { get; }

        public Action<Teams> TeamHandler { get; }

        public Action<Actor.Designation> DesignationHandler { get; }

        public EntityTransform? InitialTransform { get; private set; }

        public bool InitialSnapshotEmitted { get; private set; }

        public bool SnapshotQueuedThisFrame { get; private set; }

        public bool CanSample => InitialSnapshotEmitted && !SnapshotQueuedThisFrame;

        public void BeginFrame()
        {
            SnapshotQueuedThisFrame = false;
        }

        public bool TryQueueMandatorySnapshot(out EntityTransform transform)
        {
            transform = default;
            if (InitialSnapshotEmitted || SnapshotQueuedThisFrame)
            {
                return false;
            }

            if (!InitialTransform.HasValue)
            {
                InitialTransform = CaptureCurrentTransform();
            }

            if (!InitialTransform.HasValue)
            {
                return false;
            }

            transform = InitialTransform.Value;
            InitialSnapshotEmitted = true;
            SnapshotQueuedThisFrame = true;
            return true;
        }

        public EntityTransform? CaptureCurrentTransform()
        {
            return VtolEntityRegistryAdapter.CaptureCurrentTransform(Source);
        }

        public void ReplaceEntity(TrackedEntity entity)
        {
            Entity = entity ?? throw new ArgumentNullException(nameof(entity));
            InitialTransform = CaptureCurrentTransform();
            InitialSnapshotEmitted = false;
            SnapshotQueuedThisFrame = false;
        }
    }

    private readonly struct PendingUnregistration
    {
        public PendingUnregistration(TrackedSource source, double removeAfterSeconds)
        {
            Source = source;
            RemoveAfterSeconds = removeAfterSeconds;
        }

        public TrackedSource Source { get; }

        public double RemoveAfterSeconds { get; }
    }

}

using Avalonia;
using Avalonia.Rendering.Composition.Server;
using SmoothScroll.Avalonia.Interaction.Experimental;

namespace SmoothScroll.Avalonia.Interaction;

internal partial class ServerInteractionTracker
{
    private IExperimentalScrollMovementParticipant? _movementParticipant;
    private bool _disposed;
    private ParticipantFrameClock? _participantFrameClock;

    internal bool HasExperimentalVerticalScroll => _movementParticipant is not null;

    public void Dispose()
    {
        if (_disposed)
            return;
        CancelExperimentalScroll();
        _movementParticipant?.Detach();
        _movementParticipant = null;
        _pendingClientActions.Clear();
        _client = null;
        if (IsActive)
            Deactivate();
        _disposed = true;
    }

    private void ConfigureExperimentalVerticalScroll(IExperimentalScrollMovementParticipantFactory? factory)
    {
        CancelExperimentalScroll();
        _movementParticipant?.Detach();
        _movementParticipant = factory?.Create();
        SetPosition(ClampPosition(Position), 0, notifyParticipant: false);
        _movementParticipant?.OnPositionChanged(Position.Y);
    }

    private void CancelExperimentalScroll()
    {
        _participantFrameClock?.Stop();
        State.CancelMovement();
        _movementParticipant?.OnCancelled();
    }

    private void CancelExperimentalInertiaForInput()
    {
        var wasSettling = _participantFrameClock?.IsRunning == true;
        _participantFrameClock?.Stop();
        if (State is InertiaState or CustomAnimationState || wasSettling)
        {
            State.CancelMovement();
            _movementParticipant?.OnCancelled();
        }
    }

    internal void DispatchExperimentalToUI(Action callback) =>
        PostToClient(client => { if (!client.IsDisposed) callback(); });

    internal void NotifyExperimentalIdle(ExperimentalScrollMovementSource source)
    {
        _participantFrameClock?.Stop();
        if (_movementParticipant is not { } participant)
            return;
        participant.OnIdle(source);
        _participantFrameClock = new ParticipantFrameClock(this, participant);
        _participantFrameClock.Start();
    }

    private void EnsureExperimentalParticipantFrames()
    {
        if (State is not IdleState || _participantFrameClock?.IsRunning == true || _movementParticipant is not { } participant)
            return;
        _participantFrameClock = new ParticipantFrameClock(this, participant);
        _participantFrameClock.Start();
    }

    internal Vector3D ClampPosition(Vector3D position)
    {
        var clamped = Vector3D.Clamp(position, MinPosition, MaxPosition);
        if (_movementParticipant is not { } participant)
            return clamped;
        var context = new ExperimentalScrollMovementContext(0, 0, Position.Y,
            MinPosition.Y, MaxPosition.Y, 0, ExperimentalScrollMovementSource.Direct);
        var bounds = GetBounds(participant, in context);
        return new Vector3D(clamped.X, Math.Clamp(position.Y, bounds.Minimum, bounds.Maximum), clamped.Z);
    }

    private static ExperimentalScrollBounds GetBounds(IExperimentalScrollMovementParticipant participant,
        in ExperimentalScrollMovementContext context)
    {
        var bounds = participant.GetVerticalBounds(in context);
        if (!double.IsFinite(bounds.Minimum) || !double.IsFinite(bounds.Maximum) || bounds.Minimum > bounds.Maximum)
            throw new InvalidOperationException("Experimental participant bounds must be finite and ordered.");
        return bounds;
    }

    internal double ApplyExperimentalInput(double delta, ExperimentalScrollMovementSource source, int requestId)
    {
        // Input sample timestamps are not transported by the existing request queue. Processing
        // intervals are not sample intervals, especially when multiple deltas share one batch.
        return ApplyExperimentalVerticalMovement(delta, 0, 0, source, requestId);
    }

    internal void ApplyExperimentalHorizontalInput(double delta, int requestId)
    {
        if (delta != 0)
            SetPosition(new Vector3D(Math.Clamp(Position.X + delta, MinPosition.X, MaxPosition.X),
                Position.Y, Position.Z), requestId);
    }

    internal double ApplyExperimentalVerticalMovement(double delta, double velocity, double elapsed,
        ExperimentalScrollMovementSource source, int requestId)
    {
        var participant = _movementParticipant!;
        var context = new ExperimentalScrollMovementContext(delta, velocity, Position.Y,
            MinPosition.Y, MaxPosition.Y, elapsed, source);
        var bounds = GetBounds(participant, in context);
        var initialPosition = Math.Clamp(Position.Y, bounds.Minimum, bounds.Maximum);
        context = context with { Position = initialPosition };
        var preConsumed = ValidateConsumption(participant.PreScroll(in context), delta);
        bounds = GetBounds(participant, in context);
        // Viewport changes correct geometry independently of the offered input.
        var bodyStart = Math.Clamp(initialPosition, bounds.Minimum, bounds.Maximum);
        var available = delta - preConsumed;
        var finalPosition = Math.Clamp(bodyStart + available, bounds.Minimum, bounds.Maximum);
        var selfConsumed = finalPosition - bodyStart;
        available -= selfConsumed;
        context = context with { Delta = available, Position = finalPosition };
        var postConsumed = ValidateConsumption(participant.PostScroll(in context, selfConsumed), available);
        bounds = GetBounds(participant, in context);
        finalPosition = Math.Clamp(finalPosition, bounds.Minimum, bounds.Maximum);
        SetPosition(new Vector3D(Position.X, finalPosition, Position.Z), requestId, notifyParticipant: false);
        var remaining = available - postConsumed;
        var result = new ExperimentalScrollMovementResult(delta, preConsumed, selfConsumed, postConsumed,
            remaining, finalPosition, velocity, elapsed, source);
        participant.OnScrollCompleted(in result);
        return preConsumed + selfConsumed + postConsumed;
    }

    private sealed class ParticipantFrameClock(ServerInteractionTracker tracker,
        IExperimentalScrollMovementParticipant participant) : ServerObject(tracker.Compositor), IServerClockItem
    {
        private TimeSpan _started;
        private bool _running;
        internal bool IsRunning => _running;
        internal void Start()
        {
            _started = Compositor.Clock.Elapsed;
            var continueFrames = participant.OnFrame(0);
            tracker.SetPosition(tracker.ClampPosition(tracker.Position), 0);
            if (!continueFrames)
                return;
            _running = true;
            Compositor.Animations.AddToClock(this);
        }
        internal void Stop()
        {
            if (!_running)
                return;
            _running = false;
            Compositor.Animations.RemoveFromClock(this);
        }
        public void OnTick()
        {
            if (!_running)
                return;
            if (!participant.OnFrame((Compositor.Clock.Elapsed - _started).TotalSeconds))
                Stop();
            tracker.SetPosition(tracker.ClampPosition(tracker.Position), 0);
        }
    }

    private static double ValidateConsumption(double consumed, double available)
    {
        // A consumer commonly returns clamp(position + delta) - position. Subtraction can
        // exceed the offered delta by an ulp, although it consumed exactly that movement.
        // Normalize at subpixel numeric tolerance; reject actual contract violations.
        const double consumptionTolerance = 1e-7; // DIP, far below visible geometry precision.
        var minimum = Math.Min(0, available);
        var maximum = Math.Max(0, available);
        if (!double.IsFinite(consumed) || consumed < minimum - consumptionTolerance || consumed > maximum + consumptionTolerance)
            throw new InvalidOperationException("Experimental participant consumption must be finite, have the offered sign and not exceed the offered delta.");
        return Math.Clamp(consumed, minimum, maximum);
    }
}

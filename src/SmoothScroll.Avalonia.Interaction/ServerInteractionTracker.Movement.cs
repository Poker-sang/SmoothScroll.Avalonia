using Avalonia;
using Avalonia.Rendering.Composition.Server;

namespace SmoothScroll.Avalonia.Interaction;

internal partial class ServerInteractionTracker
{
    private const double ReferenceRange = 2000;
    private const double Tension = 0.5;
    private IScrollMovementParticipant? _movementParticipant;
    private bool _disposed;
    private ParticipantFrameClock? _participantFrameClock;
    private Vector3D _minimumPosition, _maximumPosition;
    private Vector3D _manipulationPosition;

    internal bool HasScrollParticipant => _movementParticipant is not null;

    public void Dispose()
    {
        if (_disposed) return;
        CancelScroll();
        _movementParticipant?.Detach();
        _movementParticipant = null;
        _pendingClientActions.Clear();
        _client = null;
        if (IsActive) Deactivate();
        _disposed = true;
    }

    private void ConfigureVerticalScroll(IScrollMovementParticipantFactory? factory)
    {
        CancelScroll();
        _movementParticipant?.Detach();
        _movementParticipant = factory?.Create();
        SetPosition(ClampPosition(Position), 0, notifyParticipant: false);
        _movementParticipant?.OnPositionChanged(Position.Y);
    }

    private void CancelScroll()
    {
        _participantFrameClock?.Stop();
        State.CancelMovement();
        _movementParticipant?.OnCancelled();
    }

    private void PrepareForScrollInput(bool useInertia)
    {
        var wasSettling = _participantFrameClock?.IsRunning == true;
        _participantFrameClock?.Stop();
        // Preserve ordinary wheel impulse accumulation and the participant's interruption policy.
        var interrupt = !useInertia && HasScrollParticipant && State is InertiaState or CustomAnimationState;
        if (interrupt) State.CancelMovement();
        if (interrupt || wasSettling) _movementParticipant?.OnCancelled();
    }

    internal void DispatchToUI(Action callback) =>
        PostToClient(client => { if (!client.IsDisposed) callback(); });

    internal void NotifyScrollIdle(ScrollMovementSource source)
    {
        _participantFrameClock?.Stop();
        if (_movementParticipant is not { } participant) return;
        participant.OnIdle(source);
        _participantFrameClock = new ParticipantFrameClock(this, participant);
        _participantFrameClock.Start();
    }

    private void EnsureParticipantFrames()
    {
        if (State is not IdleState || _participantFrameClock?.IsRunning == true || _movementParticipant is not { } participant)
            return;
        _participantFrameClock = new ParticipantFrameClock(this, participant);
        _participantFrameClock.Start();
    }

    // Only this method derives the effective range. UI geometry and participant geometry are inputs;
    // movement, absolute positioning, animation targets and geometry correction share its result.
    internal (Vector3D Minimum, Vector3D Maximum) GetPositionBounds(ScrollMovementContext? input = null)
    {
        if (_contentBounds is { } geometry)
            (MinPosition, MaxPosition) = geometry.Calculate(Scale);
        var minimum = MinPosition;
        var maximum = MaxPosition;
        if (_movementParticipant is { } participant)
        {
            var context = input ?? new(0, 0, Position.Y, minimum.Y, maximum.Y, 0, ScrollMovementSource.Direct);
            context = context with { Minimum = minimum.Y, Maximum = maximum.Y };
            var bounds = participant.GetVerticalBounds(in context);
            if (!double.IsFinite(bounds.Minimum) || !double.IsFinite(bounds.Maximum) || bounds.Minimum > bounds.Maximum)
                throw new InvalidOperationException("Scroll participant bounds must be finite and ordered.");
            minimum = new(minimum.X, bounds.Minimum, minimum.Z);
            maximum = new(maximum.X, bounds.Maximum, maximum.Z);
        }
        _minimumPosition = minimum;
        _maximumPosition = maximum;
        return (minimum, maximum);
    }

    internal Vector3D ClampPosition(Vector3D position)
    {
        GetPositionBounds();
        return Vector3D.Clamp(position, _minimumPosition, _maximumPosition);
    }

    internal void SyncManipulationPosition()
    {
        GetPositionBounds();
        _manipulationPosition = GetOriginalPoint(Position, _minimumPosition, _maximumPosition);
    }

    /// <summary>
    /// The common compositor movement transaction. Input deltas use zero velocity/time; inertia
    /// supplies its integrated displacement and velocities before/after decay. Returns the next
    /// velocity after body boundaries or elastic bounce. Only the optional vertical stages consume
    /// externally; ordinary movement uses exactly the same body and position commit.
    /// </summary>
    internal Vector3D ApplyScrollDelta(Vector3D delta, ScrollMovementSource source, int requestId,
        bool allowOverscroll = false, double elapsedSeconds = 0,
        Vector3D velocity = default, Vector3D decayedVelocity = default, bool isManipulation = false)
    {
        var participant = _movementParticipant;
        var context = new ScrollMovementContext(delta.Y, velocity.Y, Position.Y,
            MinPosition.Y, MaxPosition.Y, elapsedSeconds, source);
        GetPositionBounds(context);
        var initialY = participant is null ? Position.Y : Math.Clamp(Position.Y, _minimumPosition.Y, _maximumPosition.Y);
        context = context with { Position = initialY };
        var preConsumed = ValidateConsumption(participant?.PreScroll(in context) ?? 0, delta.Y);
        GetPositionBounds(context);

        var interacting = State is InteractingState;
        var raw = interacting && participant is null ? _manipulationPosition : Position;
        // Preserve the original pointer sampler's Float conversion without rounding wheel input.
        if (isManipulation && participant is null)
        {
            delta = new((float)delta.X, (float)delta.Y, delta.Z);
            requestId = 0;
        }
        var elastic = allowOverscroll && OverscrollElasticity > 0;
        var bodyStartY = participant is null ? raw.Y : Math.Clamp(initialY, _minimumPosition.Y, _maximumPosition.Y);
        var available = delta.Y - preConsumed;
        var x = MoveAxis(raw.X, delta.X, velocity.X, decayedVelocity.X,
            _minimumPosition.X, _maximumPosition.X, elastic && (participant is null || source == ScrollMovementSource.Inertia));
        var y = MoveAxis(bodyStartY, available, velocity.Y, decayedVelocity.Y,
            _minimumPosition.Y, _maximumPosition.Y, elastic && participant is null);
        var z = Math.Clamp(raw.Z + delta.Z, _minimumPosition.Z, _maximumPosition.Z);
        var selfConsumed = y.Position - bodyStartY;
        available -= selfConsumed;
        context = context with { Delta = available, Position = y.Position };
        var postConsumed = ValidateConsumption(participant?.PostScroll(in context, selfConsumed) ?? 0, available);
        var remaining = available - postConsumed;
        GetPositionBounds(context);
        // A viewport change is geometry correction, independent of the offered input.
        var finalY = participant is null ? y.Position : Math.Clamp(y.Position, _minimumPosition.Y, _maximumPosition.Y);
        var position = new Vector3D(x.Position, finalY, z);
        if (interacting)
            _manipulationPosition = participant is null && isManipulation
                ? raw + delta : position;
        SetPosition(position, requestId, notifyParticipant: false);
        if (participant is not null)
        {
            var result = new ScrollMovementResult(delta.Y, preConsumed, selfConsumed, postConsumed,
                remaining, finalY, velocity.Y, elapsedSeconds, source);
            participant.OnScrollCompleted(in result);
        }
        // A participant can keep moving at a stationary body boundary. Preserve the same decay
        // until the entire transaction is exhausted, rather than stopping at the body's edge.
        var nextYVelocity = participant is null ? y.Velocity
            : preConsumed + selfConsumed + postConsumed == 0 ? 0 : decayedVelocity.Y;
        return new(x.Velocity, nextYVelocity, 0);

        (double Position, double Velocity) MoveAxis(double start, double movement, double initialVelocity,
            double nextVelocity, double minimum, double maximum, bool allowsElasticity)
        {
            if (!allowsElasticity)
            {
                var next = Math.Clamp(start + movement, minimum, maximum);
                return (next, next <= minimum || next >= maximum ? 0 : nextVelocity);
            }
            if (source == ScrollMovementSource.Direct)
                return (GetElasticCoordinate(start + movement, minimum, maximum, OverscrollElasticity), 0);
            if (start < minimum || start > maximum)
            {
                var target = Math.Clamp(start, minimum, maximum);
                var frequency = 14 * OverscrollBounceRate;
                var acceleration = -2 * frequency * initialVelocity - frequency * frequency * (start - target);
                nextVelocity = initialVelocity + acceleration * elapsedSeconds;
                return (start + nextVelocity * elapsedSeconds, nextVelocity);
            }
            return (start + movement, nextVelocity);
        }
    }

    private sealed class ParticipantFrameClock(ServerInteractionTracker tracker,
        IScrollMovementParticipant participant) : ServerObject(tracker.Compositor), IServerClockItem
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
            throw new InvalidOperationException("Scroll participant consumption must be finite, have the offered sign and not exceed the offered delta.");
        return Math.Clamp(consumed, minimum, maximum);
    }

    private static double GetElasticCoordinate(double current, double min, double max, double tension)
    {
        (min, max) = GetOrderedBounds(min, max);

        if (double.IsNaN(current))
        {
            return min;
        }

        if (current < min)
        {
            return min - CalculateOffset(min - current, tension);
        }

        if (current > max)
        {
            return max + CalculateOffset(current - max, tension);
        }

        return current;
    }

    private static Vector3D GetOriginalPoint(Vector3D elasticPoint, Vector3D min, Vector3D max, double tension = Tension)
    {
        return new Vector3D(
            GetOriginalCoordinate(elasticPoint.X, min.X, max.X, tension),
            GetOriginalCoordinate(elasticPoint.Y, min.Y, max.Y, tension),
            GetOriginalCoordinate(elasticPoint.Z, min.Z, max.Z, tension));
    }

    private static double GetOriginalCoordinate(double elasticPoint, double min, double max, double tension)
    {
        (min, max) = GetOrderedBounds(min, max);

        if (elasticPoint < min)
        {
            var offset = CalculateInverseOffset(min - elasticPoint, tension);
            return SubtractWithSaturation(min, offset);
        }

        if (elasticPoint > max)
        {
            var offset = CalculateInverseOffset(elasticPoint - max, tension);
            return AddWithSaturation(max, offset);
        }

        return double.IsNaN(elasticPoint) ? min : elasticPoint;
    }

    private static (double Min, double Max) GetOrderedBounds(double min, double max)
    {
        var minIsNaN = double.IsNaN(min);
        var maxIsNaN = double.IsNaN(max);

        if (minIsNaN && maxIsNaN)
        {
            return (0, 0);
        }

        if (minIsNaN)
        {
            min = max;
        }

        if (maxIsNaN)
        {
            max = min;
        }

        return min <= max ? (min, max) : (max, min);
    }

    private static double CalculateOffset(double distance, double tension)
    {
        if (distance <= 0 || tension <= 0 || double.IsNaN(distance) || double.IsNaN(tension))
        {
            return 0;
        }

        if (double.IsPositiveInfinity(distance))
        {
            return ReferenceRange * tension;
        }

        return (distance / (distance + ReferenceRange)) * ReferenceRange * tension;
    }

    private static double CalculateInverseOffset(double resultOffset, double tension)
    {
        if (resultOffset <= 0 || tension <= 0 || double.IsNaN(resultOffset) || double.IsNaN(tension))
        {
            return 0;
        }

        double limit = ReferenceRange * tension;

        if (limit <= 0 || double.IsNaN(limit) || double.IsPositiveInfinity(resultOffset) || resultOffset >= limit)
        {
            return double.MaxValue;
        }

        var denominator = limit / resultOffset - 1.0;
        if (denominator <= 0 || double.IsNaN(denominator))
        {
            return double.MaxValue;
        }

        var offset = ReferenceRange / denominator;
        return double.IsNaN(offset) || double.IsInfinity(offset) ? double.MaxValue : offset;
    }

    private static double AddWithSaturation(double value, double offset)
    {
        var result = value + offset;
        return double.IsNaN(result) || double.IsPositiveInfinity(result) ? double.MaxValue : result;
    }

    private static double SubtractWithSaturation(double value, double offset)
    {
        var result = value - offset;
        return double.IsNaN(result) || double.IsNegativeInfinity(result) ? double.MinValue : result;
    }
}

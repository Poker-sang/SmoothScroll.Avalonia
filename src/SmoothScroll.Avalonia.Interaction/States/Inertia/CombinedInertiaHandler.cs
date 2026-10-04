using Avalonia;
using Avalonia.Rendering.Composition.Server;
using Avalonia.Utilities;

namespace SmoothScroll.Avalonia.Interaction;

internal sealed class CombinedInertiaHandler(
    ServerInteractionTracker interactionTracker,
    Vector3D positionVelocity,
    double scaleVelocity,
    Point scaleOrigin,
    int requestId,
    bool allowOverscroll)
    : ServerObject(interactionTracker.Compositor), IServerClockItem
{
    private const double PositionStopVelocity = 5;
    private const double ScaleStopVelocity = 0.005;
    private const double BoundaryTolerance = 0.1;
    private const double MaxTranslationVelocity = 8000;
    private const double MaxScaleVelocity = 8;

    private TimeSpan _lastTick;
    private int _stopRequested;
    private Point _scaleOrigin = scaleOrigin;
    private bool _allowOverscroll = allowOverscroll;
    private Vector3D? _modifiedRestingPosition;
    private Vector3D? _modifiedPositionDecayRate;

    public Vector3D PositionVelocity { get; private set; } = LimitMagnitude(positionVelocity, MaxTranslationVelocity);

    public double ScaleVelocity { get; private set; } = Math.Clamp(scaleVelocity, -MaxScaleVelocity, MaxScaleVelocity);

    public Vector3D NaturalRestingPosition
    {
        get
        {
            var decay = GetDecayConstant(interactionTracker.PositionInertiaDecayRate);
            return new Vector3D(
                GetNaturalRestingValue(interactionTracker.Position.X, PositionVelocity.X, decay.X),
                GetNaturalRestingValue(interactionTracker.Position.Y, PositionVelocity.Y, decay.Y),
                0);
        }
    }

    public Vector3D ModifiedRestingPosition =>
        _modifiedRestingPosition is { } position ? interactionTracker.ClampPosition(position) : interactionTracker.ClampPosition(NaturalRestingPosition);

    public double NaturalRestingScale
    {
        get
        {
            var decay = GetDecayConstant(interactionTracker.ScaleInertiaDecayRate);
            return decay > 0 ? interactionTracker.Scale * Math.Exp(ScaleVelocity / decay) : interactionTracker.Scale;
        }
    }

    public double ModifiedRestingScale =>
        Math.Clamp(NaturalRestingScale, interactionTracker.MinScale, interactionTracker.MaxScale);

    public void Start()
    {
        if (Volatile.Read(ref _stopRequested) is not 0)
            return;

        _lastTick = Compositor.Clock.Elapsed;
        Compositor.Animations.AddToClock(this);
    }

    public void Stop()
    {
        if (Interlocked.Exchange(ref _stopRequested, 1) is 0)
            Compositor.Animations.RemoveFromClock(this);
    }

    public void AddTranslationImpulse(Vector3D velocity)
    {
        _modifiedRestingPosition = null;
        _modifiedPositionDecayRate = null;
        PositionVelocity = Vector3D.Dot(PositionVelocity, velocity) < 0 ? velocity : PositionVelocity + velocity;
        PositionVelocity = LimitMagnitude(PositionVelocity, MaxTranslationVelocity);
    }

    public void UpdateRestingPosition(Vector3D position)
    {
        var target = interactionTracker.ClampPosition(position);
        var current = interactionTracker.Position;
        var decay = GetDecayConstant(interactionTracker.PositionInertiaDecayRate);

        _modifiedRestingPosition = target;
        PositionVelocity = LimitMagnitude(new Vector3D(
            (target.X - current.X) * decay.X,
            (target.Y - current.Y) * decay.Y,
            0), MaxTranslationVelocity);
        _modifiedPositionDecayRate = new Vector3D(
            GetTargetDecayRate(current.X, target.X, PositionVelocity.X),
            GetTargetDecayRate(current.Y, target.Y, PositionVelocity.Y),
            interactionTracker.PositionInertiaDecayRate.Z);
    }

    public void ClampRestingPositionToBounds()
    {
        if (_modifiedRestingPosition is { } position)
            UpdateRestingPosition(position);
    }

    public void AddScaleImpulse(Point origin, double velocity)
    {
        _scaleOrigin = origin;
        ScaleVelocity = Math.Sign(ScaleVelocity) != Math.Sign(velocity) ? velocity : ScaleVelocity + velocity;
        ScaleVelocity = Math.Clamp(ScaleVelocity, -MaxScaleVelocity, MaxScaleVelocity);
    }

    public void DisableOverscroll()
    {
        _allowOverscroll = false;
        var clampedPosition = interactionTracker.ClampPosition(interactionTracker.Position);
        interactionTracker.SetPosition(clampedPosition, InteractionTrackerValuesChangedArgs.UserRequestId);
    }

    public void ApplyTranslationDelta(Vector delta)
    {
        _modifiedRestingPosition = null;
        _modifiedPositionDecayRate = null;
        PositionVelocity = new Vector3D(
            delta.X is 0 ? PositionVelocity.X : 0,
            delta.Y is 0 ? PositionVelocity.Y : 0,
            0);
        interactionTracker.ApplyScrollDelta(new Vector3D(delta.X, delta.Y, 0),
            ScrollMovementSource.Wheel, InteractionTrackerValuesChangedArgs.UserRequestId);
    }

    public void ApplyScaleDelta(Point origin, double delta)
    {
        ScaleVelocity = 0;
        var scale = Math.Clamp(
            interactionTracker.Scale * delta,
            interactionTracker.MinScale,
            interactionTracker.MaxScale);
        interactionTracker.SetScale(
            scale,
            new Vector3D(origin.X, origin.Y, 0),
            InteractionTrackerValuesChangedArgs.UserRequestId);
    }

    public void OnTick()
    {
        if (Volatile.Read(ref _stopRequested) is not 0)
        {
            Stop();
            return;
        }

        var now = Compositor.Clock.Elapsed;
        var elapsed = Math.Clamp((now - _lastTick).TotalSeconds, 0, 1.0 / 15.0);
        _lastTick = now;
        if (elapsed <= 0)
            return;

        StepScale(elapsed);
        StepPosition(elapsed);
        CompleteModifiedPositionIfNeeded();

        if (!HasCompleted())
            return;

        var finalPosition = interactionTracker.ClampPosition(interactionTracker.Position);
        if (_modifiedRestingPosition is { } target)
            finalPosition = interactionTracker.ClampPosition(target);
        interactionTracker.SetPosition(finalPosition, requestId);
        Stop();
        interactionTracker.ChangeState(new IdleState(interactionTracker, requestId));
        interactionTracker.NotifyScrollIdle(ScrollMovementSource.Inertia);
    }

    private void StepScale(double elapsed)
    {
        if (Math.Abs(ScaleVelocity) <= ScaleStopVelocity)
        {
            ScaleVelocity = 0;
            return;
        }

        var currentScale = interactionTracker.Scale;
        var scaleLogDelta = GetDecayedDisplacement(
            ScaleVelocity,
            interactionTracker.ScaleInertiaDecayRate,
            elapsed);
        var requestedScale = currentScale * Math.Exp(scaleLogDelta);
        var scale = Math.Clamp(requestedScale, interactionTracker.MinScale, interactionTracker.MaxScale);
        interactionTracker.SetScale(scale, new Vector3D(_scaleOrigin.X, _scaleOrigin.Y, 0), requestId);

        if (!MathUtilities.AreClose(scale, requestedScale))
            ScaleVelocity = 0;
        else
            ScaleVelocity *= GetFrameDecay(interactionTracker.ScaleInertiaDecayRate, elapsed);
    }

    internal void StepPosition(double elapsed)
    {
        var decayRate = _modifiedPositionDecayRate ?? interactionTracker.PositionInertiaDecayRate;
        var delta = new Vector3D(
            GetDecayedDisplacement(PositionVelocity.X, decayRate.X, elapsed),
            GetDecayedDisplacement(PositionVelocity.Y, decayRate.Y, elapsed), 0);
        var decayedVelocity = new Vector3D(
            PositionVelocity.X * GetFrameDecay(decayRate.X, elapsed),
            PositionVelocity.Y * GetFrameDecay(decayRate.Y, elapsed), 0);
        PositionVelocity = interactionTracker.ApplyScrollDelta(delta, ScrollMovementSource.Inertia,
            requestId, _allowOverscroll, elapsed, PositionVelocity, decayedVelocity);
    }

    private bool HasCompleted()
    {
        var clamped = interactionTracker.ClampPosition(interactionTracker.Position);
        var atBoundary = Vector3D.Distance(interactionTracker.Position, clamped) <= BoundaryTolerance;
        return PositionVelocity.Length <= PositionStopVelocity
               && Math.Abs(ScaleVelocity) <= ScaleStopVelocity
               && atBoundary;
    }

    private void CompleteModifiedPositionIfNeeded()
    {
        if (_modifiedRestingPosition is not { } target
            || PositionVelocity.Length > PositionStopVelocity)
        {
            return;
        }

        target = interactionTracker.ClampPosition(target);
        _modifiedRestingPosition = target;
        _modifiedPositionDecayRate = null;
        PositionVelocity = default;
        interactionTracker.SetPosition(target, requestId);
    }

    private static double GetTargetDecayRate(double current, double target, double velocity)
    {
        var distance = target - current;
        if (Math.Abs(distance) <= BoundaryTolerance || Math.Abs(velocity) <= double.Epsilon)
            return 0.01;

        var decayConstant = Math.Abs(velocity / distance);
        return Math.Exp(-decayConstant / 60);
    }

    private static Vector3D LimitMagnitude(Vector3D value, double maximum)
    {
        var length = value.Length;
        return length > maximum ? value * (maximum / length) : value;
    }

    private static double GetFrameDecay(double rate, double elapsed) =>
        Math.Pow(Math.Clamp(rate, 0.01, 0.9999), elapsed * 60);

    // Integrate exponential decay over the entire compositor interval so travel is frame-rate independent.
    private static double GetDecayedDisplacement(double velocity, double rate, double elapsed)
    {
        var decayConstant = GetDecayConstant(rate);
        return decayConstant > 0 ? velocity * (1 - GetFrameDecay(rate, elapsed)) / decayConstant : velocity * elapsed;
    }

    private static Vector3D GetDecayConstant(Vector3D rate) =>
        new(
            GetDecayConstant(rate.X),
            GetDecayConstant(rate.Y),
            GetDecayConstant(rate.Z));

    private static double GetDecayConstant(double rate) =>
        -Math.Log(Math.Clamp(rate, 0.01, 0.9999)) * 60;

    private static double GetNaturalRestingValue(double value, double velocity, double decay) =>
        decay > 0 ? value + (velocity / decay) : value;
}

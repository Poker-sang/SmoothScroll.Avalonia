using Avalonia;
using Avalonia.Rendering.Composition.Server;
using Avalonia.Utilities;

namespace SmoothScroll.Avalonia.Interaction;

internal sealed class CombinedInertiaHandler : ServerObject, IServerClockItem
{
    private const double PositionStopVelocity = 5;
    private const double ScaleStopVelocity = 0.005;
    private const double BoundaryTolerance = 0.1;
    private const double MaxTranslationVelocity = 8000;
    private const double MaxScaleVelocity = 8;

    private readonly ServerInteractionTracker _interactionTracker;
    private readonly int _requestId;
    private TimeSpan _lastTick;
    private int _stopRequested;
    private Vector3D _positionVelocity;
    private double _scaleVelocity;
    private Point _scaleOrigin;
    private bool _allowOverscroll;
    private Vector3D? _modifiedRestingPosition;
    private Vector3D? _modifiedPositionDecayRate;

    public CombinedInertiaHandler(
        ServerCompositor compositor,
        ServerInteractionTracker interactionTracker,
        Vector3D positionVelocity,
        double scaleVelocity,
        Point scaleOrigin,
        int requestId,
        bool allowOverscroll) : base(compositor)
    {
        _interactionTracker = interactionTracker;
        _positionVelocity = LimitMagnitude(positionVelocity, MaxTranslationVelocity);
        _scaleVelocity = Math.Clamp(scaleVelocity, -MaxScaleVelocity, MaxScaleVelocity);
        _scaleOrigin = scaleOrigin;
        _requestId = requestId;
        _allowOverscroll = allowOverscroll;
    }

    public Vector3D PositionVelocity => _positionVelocity;

    public double ScaleVelocity => _scaleVelocity;

    public Vector3D NaturalRestingPosition
    {
        get
        {
            var decay = GetDecayConstant(_interactionTracker.PositionInertiaDecayRate);
            return new Vector3D(
                GetNaturalRestingValue(_interactionTracker.Position.X, _positionVelocity.X, decay.X),
                GetNaturalRestingValue(_interactionTracker.Position.Y, _positionVelocity.Y, decay.Y),
                0);
        }
    }

    public Vector3D ModifiedRestingPosition =>
        _modifiedRestingPosition is { } position ? Vector3D.Clamp(position, _interactionTracker.MinPosition, _interactionTracker.MaxPosition) : Vector3D.Clamp(NaturalRestingPosition, _interactionTracker.MinPosition, _interactionTracker.MaxPosition);

    public double NaturalRestingScale
    {
        get
        {
            var decay = GetDecayConstant(_interactionTracker.ScaleInertiaDecayRate);
            return decay > 0 ? _interactionTracker.Scale * Math.Exp(_scaleVelocity / decay) : _interactionTracker.Scale;
        }
    }

    public double ModifiedRestingScale =>
        Math.Clamp(NaturalRestingScale, _interactionTracker.MinScale, _interactionTracker.MaxScale);

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
        _positionVelocity = Vector3D.Dot(_positionVelocity, velocity) < 0 ? velocity : _positionVelocity + velocity;
        _positionVelocity = LimitMagnitude(_positionVelocity, MaxTranslationVelocity);
    }

    public void UpdateRestingPosition(Vector3D position)
    {
        var target = Vector3D.Clamp(position, _interactionTracker.MinPosition, _interactionTracker.MaxPosition);
        var current = _interactionTracker.Position;
        var decay = GetDecayConstant(_interactionTracker.PositionInertiaDecayRate);

        _modifiedRestingPosition = target;
        _positionVelocity = LimitMagnitude(new Vector3D(
            (target.X - current.X) * decay.X,
            (target.Y - current.Y) * decay.Y,
            0), MaxTranslationVelocity);
        _modifiedPositionDecayRate = new Vector3D(
            GetTargetDecayRate(current.X, target.X, _positionVelocity.X),
            GetTargetDecayRate(current.Y, target.Y, _positionVelocity.Y),
            _interactionTracker.PositionInertiaDecayRate.Z);
    }

    public void ClampRestingPositionToBounds()
    {
        if (_modifiedRestingPosition is { } position)
            UpdateRestingPosition(position);
    }

    public void AddScaleImpulse(Point origin, double velocity)
    {
        _scaleOrigin = origin;
        _scaleVelocity = Math.Sign(_scaleVelocity) != Math.Sign(velocity) ? velocity : _scaleVelocity + velocity;
        _scaleVelocity = Math.Clamp(_scaleVelocity, -MaxScaleVelocity, MaxScaleVelocity);
    }

    public void DisableOverscroll()
    {
        _allowOverscroll = false;
        var clampedPosition = Vector3D.Clamp(
            _interactionTracker.Position,
            _interactionTracker.MinPosition,
            _interactionTracker.MaxPosition);
        _interactionTracker.SetPosition(clampedPosition, InteractionTrackerValuesChangedArgs.UserRequestId);
    }

    public void ApplyTranslationDelta(Vector delta)
    {
        _modifiedRestingPosition = null;
        _modifiedPositionDecayRate = null;
        _positionVelocity = new Vector3D(
            delta.X is 0 ? _positionVelocity.X : 0,
            delta.Y is 0 ? _positionVelocity.Y : 0,
            0);
        var position = Vector3D.Clamp(
            _interactionTracker.Position + new Vector3D(delta.X, delta.Y, 0),
            _interactionTracker.MinPosition,
            _interactionTracker.MaxPosition);
        _interactionTracker.SetPosition(position, InteractionTrackerValuesChangedArgs.UserRequestId);
    }

    public void ApplyScaleDelta(Point origin, double delta)
    {
        _scaleVelocity = 0;
        var scale = Math.Clamp(
            _interactionTracker.Scale * delta,
            _interactionTracker.MinScale,
            _interactionTracker.MaxScale);
        _interactionTracker.SetScale(
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

        var finalPosition = Vector3D.Clamp(
            _interactionTracker.Position,
            _interactionTracker.MinPosition,
            _interactionTracker.MaxPosition);
        if (_modifiedRestingPosition is { } target)
            finalPosition = Vector3D.Clamp(target, _interactionTracker.MinPosition, _interactionTracker.MaxPosition);
        _interactionTracker.SetPosition(finalPosition, _requestId);
        Stop();
        _interactionTracker.ChangeState(new IdleState(_interactionTracker, _requestId));
    }

    private void StepScale(double elapsed)
    {
        if (Math.Abs(_scaleVelocity) <= ScaleStopVelocity)
        {
            _scaleVelocity = 0;
            return;
        }

        var currentScale = _interactionTracker.Scale;
        var scaleLogDelta = GetDecayedDisplacement(
            _scaleVelocity,
            _interactionTracker.ScaleInertiaDecayRate,
            elapsed);
        var requestedScale = currentScale * Math.Exp(scaleLogDelta);
        var scale = Math.Clamp(requestedScale, _interactionTracker.MinScale, _interactionTracker.MaxScale);
        _interactionTracker.SetScale(scale, new Vector3D(_scaleOrigin.X, _scaleOrigin.Y, 0), _requestId);

        if (!MathUtilities.AreClose(scale, requestedScale))
            _scaleVelocity = 0;
        else
            _scaleVelocity *= GetFrameDecay(_interactionTracker.ScaleInertiaDecayRate, elapsed);
    }

    private void StepPosition(double elapsed)
    {
        var position = _interactionTracker.Position;
        var decayRate = _modifiedPositionDecayRate ?? _interactionTracker.PositionInertiaDecayRate;

        (var x, var velocityX) = StepAxis(
            position.X,
            _positionVelocity.X,
            _interactionTracker.MinPosition.X,
            _interactionTracker.MaxPosition.X,
            decayRate.X,
            elapsed);
        (var y, var velocityY) = StepAxis(
            position.Y,
            _positionVelocity.Y,
            _interactionTracker.MinPosition.Y,
            _interactionTracker.MaxPosition.Y,
            decayRate.Y,
            elapsed);

        _positionVelocity = new Vector3D(velocityX, velocityY, 0);
        _interactionTracker.SetPosition(new Vector3D(x, y, 0), _requestId);
    }

    private (double Position, double Velocity) StepAxis(
        double position,
        double velocity,
        double minimum,
        double maximum,
        double decayRate,
        double elapsed)
    {
        if (!_allowOverscroll || _interactionTracker.OverscrollElasticity <= 0)
        {
            var next = Math.Clamp(
                position + GetDecayedDisplacement(velocity, decayRate, elapsed),
                minimum,
                maximum);
            if (next <= minimum || next >= maximum)
                velocity = 0;
            else
                velocity *= GetFrameDecay(decayRate, elapsed);
            return (next, velocity);
        }

        if (position < minimum || position > maximum)
        {
            var target = Math.Clamp(position, minimum, maximum);
            var angularFrequency = 14 * _interactionTracker.OverscrollBounceRate;
            var displacement = position - target;
            var acceleration = (-2 * angularFrequency * velocity)
                               - (angularFrequency * angularFrequency * displacement);
            velocity += acceleration * elapsed;
            position += velocity * elapsed;
            return (position, velocity);
        }

        position += GetDecayedDisplacement(velocity, decayRate, elapsed);
        velocity *= GetFrameDecay(decayRate, elapsed);
        return (position, velocity);
    }

    private bool HasCompleted()
    {
        var clamped = Vector3D.Clamp(
            _interactionTracker.Position,
            _interactionTracker.MinPosition,
            _interactionTracker.MaxPosition);
        var atBoundary = Vector3D.Distance(_interactionTracker.Position, clamped) <= BoundaryTolerance;
        return _positionVelocity.Length <= PositionStopVelocity
               && Math.Abs(_scaleVelocity) <= ScaleStopVelocity
               && atBoundary;
    }

    private void CompleteModifiedPositionIfNeeded()
    {
        if (_modifiedRestingPosition is not { } target
            || _positionVelocity.Length > PositionStopVelocity)
        {
            return;
        }

        target = Vector3D.Clamp(target, _interactionTracker.MinPosition, _interactionTracker.MaxPosition);
        _modifiedRestingPosition = target;
        _modifiedPositionDecayRate = null;
        _positionVelocity = default;
        _interactionTracker.SetPosition(target, _requestId);
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

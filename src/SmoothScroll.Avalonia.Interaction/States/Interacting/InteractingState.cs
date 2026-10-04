using Avalonia;
using Avalonia.Input;

namespace SmoothScroll.Avalonia.Interaction;

internal sealed class InteractingState : InteractionTrackerState
{
    internal override string Name => "InteractingState";

    private double _previousScale;
    private Point _previousOrigin;
    private TimeSpan _previousScaleTimestamp;
    private double _scaleVelocity;
    private Point _scaleOrigin;
    private bool _hasPreviousOrigin;
    private readonly bool _allowOverscroll;

    public InteractingState(ServerInteractionTracker interactionTracker, bool allowOverscroll) : base(interactionTracker)
    {
        _allowOverscroll = allowOverscroll;
        _previousScale = interactionTracker.Scale;
        _interactionTracker.SyncManipulationPosition();
        EnterState();
    }

    protected override void EnterState()
    {
        _interactionTracker.NotifyInteractingStateEntered(requestId: 0, isFromBinding: false);
    }

    internal override void BeginUserManipulation(Point position, IPointer pointer)
    {
        // This probably shouldn't happen.
        // We ignore.
        //if (this.Log().IsEnabled(LogLevel.Error))
        //{
        //    this.Log().Error("Unexpected StartUserManipulation while in interacting state");
        //}
    }

    internal override void CompleteUserManipulation()
    {
        var clampedPosition = _interactionTracker.ClampPosition(_interactionTracker.Position);
        if (_interactionTracker.Position != clampedPosition)
        {
            _interactionTracker.ChangeState(new InertiaState(
                _interactionTracker,
                positionVelocity: default,
                scaleVelocity: 0,
                scaleOrigin: default,
                requestId: 0,
                allowOverscroll: _allowOverscroll));
            return;
        }

        _interactionTracker.ChangeState(new IdleState(_interactionTracker, requestId: 0));
        _interactionTracker.NotifyScrollIdle(ScrollMovementSource.Direct);
    }

    internal override void AddScaleVelocity(Point origin, double scaleDelta, bool useInertia)
    {
        if (scaleDelta <= 0 || double.IsNaN(scaleDelta) || double.IsInfinity(scaleDelta))
            return;

        if (useInertia)
        {
            var now = _interactionTracker.Compositor.Clock.Elapsed;
            var elapsed = _previousScaleTimestamp == default ? 0.2 : Math.Max((now - _previousScaleTimestamp).TotalSeconds, 1.0 / 240.0);
            var instantaneousVelocity = Math.Log(scaleDelta) / elapsed;
            _scaleVelocity = Math.Clamp(
                _previousScaleTimestamp == default ? instantaneousVelocity : (_scaleVelocity * 0.65) + (instantaneousVelocity * 0.35),
                -8,
                8);
            _previousScaleTimestamp = now;
        }
        else
        {
            _scaleVelocity = 0;
            _previousScaleTimestamp = default;
        }

        _scaleOrigin = origin;

        var isOriginBaseline = !useInertia && scaleDelta is 1;
        var originDelta = _hasPreviousOrigin && _previousOrigin != default && !isOriginBaseline
            ? origin - _previousOrigin : default;
        if (originDelta != default)
            _interactionTracker.ApplyScrollDelta(new Vector3D(-(float)originDelta.X, -(float)originDelta.Y, 0),
                ScrollMovementSource.Direct, 0, _allowOverscroll, isManipulation: true);

        var clampedScale = Math.Clamp(_previousScale * scaleDelta, _interactionTracker.MinScale, _interactionTracker.MaxScale);
        if (Math.Abs(clampedScale - _previousScale) > double.Epsilon)
        {
            _interactionTracker.SetScale(clampedScale, new Vector3D(origin.X, origin.Y, 0), 0);
            _previousScale = clampedScale;
        }
        else if (originDelta == default)
            _interactionTracker.ApplyScrollDelta(default, ScrollMovementSource.Direct, 0,
                _allowOverscroll, isManipulation: true);

        _previousOrigin = origin;
        _hasPreviousOrigin = true;
    }

    internal override void ApplyManipulationDelta(Vector translationDelta) =>
        _interactionTracker.ApplyScrollDelta(new Vector3D(translationDelta.X, translationDelta.Y, 0),
            ScrollMovementSource.Direct, InteractionTrackerValuesChangedArgs.UserRequestId,
            _allowOverscroll, isManipulation: true);

    internal override void StartInertia(Vector linearVelocity, bool includeScaleVelocity)
    {
        _interactionTracker.ChangeState(new InertiaState(
            _interactionTracker,
            new Vector3D((float)linearVelocity.X, (float)linearVelocity.Y, 0),
            includeScaleVelocity ? GetScaleReleaseVelocity() : 0,
            _scaleOrigin,
            requestId: 0,
            allowOverscroll: _allowOverscroll));
    }

    internal override void ApplyWheelDelta(Vector delta, bool useInertia)
    {
        if (useInertia)
        {
            // Wheel input can arrive before the pointer release reaches the composition thread.
            _interactionTracker.ChangeState(new InertiaState(
                _interactionTracker,
                GetWheelImpulseVelocity(delta),
                scaleVelocity: 0,
                scaleOrigin: default,
                requestId: 0));
            return;
        }

        _interactionTracker.ApplyScrollDelta(new Vector3D(delta.X, delta.Y, 0),
            ScrollMovementSource.Wheel, InteractionTrackerValuesChangedArgs.UserRequestId);
    }

    internal override void TryUpdatePositionWithAdditionalVelocity(Vector3D velocityInPixelsPerSecond, int requestId)
    {
        _interactionTracker.NotifyRequestIgnored(requestId);
    }

    internal override void TryUpdatePosition(Vector3D value, InteractionTrackerClampingOption option, int requestId)
    {
        _interactionTracker.NotifyRequestIgnored(requestId);
    }

    internal override void TryUpdateScale(double scale, Vector3D centerPoint, int requestId)
    {
        _interactionTracker.NotifyRequestIgnored(requestId);
    }

    internal override void ReceiveBoundsUpdate()
    {
        _interactionTracker.SyncManipulationPosition();
    }

    private double GetScaleReleaseVelocity()
    {
        if (_previousScaleTimestamp == default)
            return 0;

        var idleSeconds = (_interactionTracker.Compositor.Clock.Elapsed - _previousScaleTimestamp).TotalSeconds;
        return idleSeconds >= 0.12 ? 0 : _scaleVelocity * (1 - (idleSeconds / 0.12));
    }

}

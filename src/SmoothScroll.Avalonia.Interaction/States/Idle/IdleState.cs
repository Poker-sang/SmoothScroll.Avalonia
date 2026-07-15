using Avalonia;
using Avalonia.Input;
using Avalonia.Rendering.Composition.Animations;

namespace SmoothScroll.Avalonia.Interaction;

internal sealed class IdleState : InteractionTrackerState
{
    private readonly bool _isInitialIdleState;
    private readonly int _requestId;

    internal override string Name => "IdleState";

    public IdleState(ServerInteractionTracker interactionTracker, int requestId, bool isInitialIdleState = false) : base(interactionTracker)
    {
        _requestId = requestId;
        _isInitialIdleState = isInitialIdleState;
        EnterState();
    }

    protected override void EnterState()
    {
        _interactionTracker.NotifyIdleStateEntered(_requestId, isFromBinding: false, _isInitialIdleState);
    }

    internal override void BeginUserManipulation(Point position, IPointer pointer)
    {
        _interactionTracker.ChangeState(new InteractingState(_interactionTracker, AllowsOverscroll(pointer)));
    }

    internal override void CompleteUserManipulation()
    {
    }

    internal override void AddScaleVelocity(Point origin, double delta, bool useInertia)
    {
        if (delta <= 0 || double.IsNaN(delta) || double.IsInfinity(delta))
            return;

        if (useInertia)
        {
            _interactionTracker.ChangeState(new InertiaState(
                _interactionTracker,
                default,
                Math.Log(delta) / 0.2,
                origin,
                requestId: 0));
        }
        else
        {
            var scale = Math.Clamp(
                _interactionTracker.Scale * delta,
                _interactionTracker.MinScale,
                _interactionTracker.MaxScale);
            _interactionTracker.SetScale(
                scale,
                new Vector3D(origin.X, origin.Y, 0),
                InteractionTrackerValuesChangedArgs.UserRequestId);
        }
    }

    internal override void ApplyManipulationDelta(Vector translationDelta)
    {
    }

    internal override void StartInertia(Vector linearVelocity, bool includeScaleVelocity)
    {
        _interactionTracker.ChangeState(new InertiaState(
            _interactionTracker,
            new Vector3D(linearVelocity.X, linearVelocity.Y, 0),
            0,
            default,
            requestId: 0));
    }

    internal override void ApplyWheelDelta(Vector delta, bool useInertia)
    {
        if (useInertia)
        {
            _interactionTracker.ChangeState(new InertiaState(
                _interactionTracker,
                GetWheelImpulseVelocity(delta),
                0,
                default,
                requestId: 0));
        }
        else
        {
            var position = Vector3D.Clamp(
                _interactionTracker.Position + new Vector3D(delta.X, delta.Y, 0),
                _interactionTracker.MinPosition,
                _interactionTracker.MaxPosition);
            _interactionTracker.SetPosition(
                position,
                InteractionTrackerValuesChangedArgs.UserRequestId);
        }
    }

    internal override void TryUpdatePositionWithAdditionalVelocity(Vector3D velocityInPixelsPerSecond, int requestId)
    {
        // State changes to inertia and inertia modifiers are evaluated with requested velocity as initial velocity
        // TODO: inertia modifiers not yet implemented.
        _interactionTracker.ChangeState(new InertiaState(
            _interactionTracker,
            velocityInPixelsPerSecond,
            0,
            default,
            requestId));
    }

    internal override void TryUpdatePosition(Vector3D value, InteractionTrackerClampingOption option, int requestId)
    {
        if (option == InteractionTrackerClampingOption.Auto)
        {
            value = Vector3D.Clamp(value, _interactionTracker.MinPosition, _interactionTracker.MaxPosition);
        }

        _interactionTracker.SetPosition(value, requestId);
    }

    internal override void TryUpdateScale(double scale, Vector3D centerPoint, int requestId) =>
        _interactionTracker.SetScale(scale, centerPoint, requestId);

    internal override void ReceiveBoundsUpdate()
    {
        var position = _interactionTracker.Position;
        var clampedPosition = Vector3D.Clamp(position, _interactionTracker.MinPosition, _interactionTracker.MaxPosition);
        _interactionTracker.SetPosition(clampedPosition, 0);
    }

    internal override void StartAnimation(
        CompositionAnimation animation,
        int requestId,
        Vector3D? centerPoint = null)
    {
        _interactionTracker.ChangeState(new CustomAnimationState(
            _interactionTracker,
            animation,
            requestId,
            centerPoint));
    }
}

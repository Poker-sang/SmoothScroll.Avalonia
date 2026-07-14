using Avalonia;
using Avalonia.Input;
using Avalonia.Rendering.Composition.Animations;

namespace SmoothScroll.Avalonia.Interaction;

internal sealed class CustomAnimationState : InteractionTrackerState
{
    private readonly CustomAnimationHandler _animationHandler;

    internal override string Name => "CustomAnimationState";

    public CustomAnimationState(
        ServerInteractionTracker interactionTracker,
        CompositionAnimation animation,
        int requestId,
        Vector3D? scaleCenterPoint = null) : base(interactionTracker)
    {
        _animationHandler = scaleCenterPoint is null ?
            new PositionAnimationHandler(
                interactionTracker,
                animation,
                requestId,
                interactionTracker.Compositor) :
            new ScaleAnimationHandler(
                interactionTracker,
                animation,
                scaleCenterPoint.Value,
                requestId,
                interactionTracker.Compositor);
        EnterState();
        _animationHandler.Start();
    }

    protected override void EnterState() => _interactionTracker.NotifyCustomAnimationStateEntered();

    internal override void BeginUserManipulation(Point position, IPointer pointer)
    {
        _animationHandler.Stop();
        _interactionTracker.ChangeState(new InteractingState(_interactionTracker, AllowsOverscroll(pointer)));
    }

    internal override void CompleteUserManipulation()
    {
    }

    internal override void AddScaleVelocity(Point origin, double delta, bool useInertia)
    {
        if (delta <= 0 || !double.IsFinite(delta))
            return;

        _animationHandler.Stop();
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
            _interactionTracker.ChangeState(new IdleState(
                _interactionTracker,
                InteractionTrackerValuesChangedArgs.UserRequestId));
        }
    }

    internal override void ApplyManipulationDelta(Vector translationDelta)
    {
    }

    internal override void StartInertia(Vector linearVelocity, bool includeScaleVelocity)
    {
        _animationHandler.Stop();
        _interactionTracker.ChangeState(new InertiaState(
            _interactionTracker,
            new Vector3D(linearVelocity.X, linearVelocity.Y, 0),
            0,
            default,
            requestId: 0));
    }

    internal override void ApplyWheelDelta(Vector delta, bool useInertia)
    {
        _animationHandler.Stop();
        if (useInertia)
        {
            var velocity = delta / 0.25;
            _interactionTracker.ChangeState(new InertiaState(
                _interactionTracker,
                new Vector3D(velocity.X, velocity.Y, 0),
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
            _interactionTracker.ChangeState(new IdleState(
                _interactionTracker,
                InteractionTrackerValuesChangedArgs.UserRequestId));
        }
    }

    internal override void StartAnimation(
        CompositionAnimation animation,
        int requestId,
        Vector3D? scaleCenterPoint = null)
    {
        _animationHandler.Stop();
        _interactionTracker.ChangeState(new CustomAnimationState(
            _interactionTracker,
            animation,
            requestId,
            scaleCenterPoint));
    }

    internal override void TryUpdatePositionWithAdditionalVelocity(
        Vector3D velocityInPixelsPerSecond,
        int requestId)
    {
        _animationHandler.Stop();
        _interactionTracker.ChangeState(new InertiaState(
            _interactionTracker,
            velocityInPixelsPerSecond,
            0,
            default,
            requestId));
    }

    internal override void TryUpdatePosition(
        Vector3D value,
        InteractionTrackerClampingOption option,
        int requestId)
    {
        _animationHandler.Stop();
        if (option is InteractionTrackerClampingOption.Auto)
            value = Vector3D.Clamp(value, _interactionTracker.MinPosition, _interactionTracker.MaxPosition);

        _interactionTracker.SetPosition(value, requestId);
        _interactionTracker.ChangeState(new IdleState(_interactionTracker, requestId));
    }

    internal override void ReceiveBoundsUpdate()
    {
    }
}

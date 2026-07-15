using Avalonia;
using Avalonia.Input;
using Avalonia.Rendering.Composition.Animations;

namespace SmoothScroll.Avalonia.Interaction;

internal sealed class InertiaState : InteractionTrackerState
{
    private readonly CombinedInertiaHandler _handler;
    private readonly int _requestId;

    internal override string Name => "InertiaState";

    public InertiaState(
        ServerInteractionTracker interactionTracker,
        Vector3D positionVelocity,
        double scaleVelocity,
        Point scaleOrigin,
        int requestId,
        bool allowOverscroll = false) : base(interactionTracker)
    {
        _requestId = requestId;
        _handler = new CombinedInertiaHandler(
            interactionTracker.Compositor,
            interactionTracker,
            positionVelocity,
            scaleVelocity,
            scaleOrigin,
            requestId,
            allowOverscroll);
        EnterState();
    }

    protected override void EnterState()
    {
        NotifyStateEntered();
        _handler.Start();
    }

    internal override void BeginUserManipulation(Point position, IPointer pointer)
    {
        _handler.Stop();
        _interactionTracker.ChangeState(new InteractingState(_interactionTracker, AllowsOverscroll(pointer)));
    }

    internal override void CompleteUserManipulation()
    {
    }

    internal override void AddScaleVelocity(Point origin, double delta, bool useInertia)
    {
        if (delta <= 0 || !double.IsFinite(delta))
            return;

        if (useInertia)
        {
            _handler.AddScaleImpulse(origin, Math.Log(delta) / 0.2);
            NotifyStateEntered();
        }
        else
        {
            _handler.ApplyScaleDelta(origin, delta);
        }
    }

    internal override void ApplyManipulationDelta(Vector translationDelta)
    {
    }

    internal override void StartInertia(Vector linearVelocity, bool includeScaleVelocity)
    {
        _handler.AddTranslationImpulse(new Vector3D(linearVelocity.X, linearVelocity.Y, 0));
        NotifyStateEntered();
    }

    internal override void ApplyWheelDelta(Vector delta, bool useInertia)
    {
        _handler.DisableOverscroll();
        if (useInertia)
        {
            _handler.AddTranslationImpulse(GetWheelImpulseVelocity(delta));
            NotifyStateEntered();
        }
        else
        {
            _handler.ApplyTranslationDelta(delta);
        }
    }

    internal override void ReceiveBoundsUpdate()
    {
        _handler.ClampRestingPositionToBounds();
    }

    internal override void UpdateInertiaRestingPosition(Vector3D position, int requestId) =>
        _handler.UpdateRestingPosition(position);

    internal override void TryUpdatePositionWithAdditionalVelocity(Vector3D velocityInPixelsPerSecond, int requestId)
    {
        _handler.AddTranslationImpulse(velocityInPixelsPerSecond);
        NotifyStateEntered();
    }

    internal override void TryUpdatePosition(
        Vector3D value,
        InteractionTrackerClampingOption option,
        int requestId)
    {
        _handler.Stop();
        if (option is InteractionTrackerClampingOption.Auto)
            value = Vector3D.Clamp(value, _interactionTracker.MinPosition, _interactionTracker.MaxPosition);

        _interactionTracker.SetPosition(value, requestId);
        _interactionTracker.ChangeState(new IdleState(_interactionTracker, requestId));
    }

    internal override void TryUpdateScale(double scale, Vector3D centerPoint, int requestId)
    {
        _handler.Stop();
        _interactionTracker.SetScale(scale, centerPoint, requestId);
        _interactionTracker.ChangeState(new IdleState(_interactionTracker, requestId));
    }

    internal override void StartAnimation(
        CompositionAnimation animation,
        int requestId,
        Vector3D? scaleCenterPoint = null)
    {
        _handler.Stop();
        _interactionTracker.ChangeState(new CustomAnimationState(
            _interactionTracker,
            animation,
            requestId,
            scaleCenterPoint));
    }

    private void NotifyStateEntered() =>
        _interactionTracker.NotifyInertiaStateEntered(
            _handler.ModifiedRestingPosition,
            _handler.ModifiedRestingScale,
            _handler.NaturalRestingPosition,
            _handler.NaturalRestingScale,
            _handler.PositionVelocity,
            _requestId,
            (float)_handler.ScaleVelocity,
            isInertiaFromImpulse: true,
            isFromBinding: false);
}

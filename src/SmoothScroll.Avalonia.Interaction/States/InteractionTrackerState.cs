using Avalonia;
using Avalonia.Input;
using Avalonia.Rendering.Composition.Animations;

namespace SmoothScroll.Avalonia.Interaction;

internal abstract class InteractionTrackerState
{
    private const double WheelImpulseDuration = 0.25;

    private protected ServerInteractionTracker _interactionTracker;

    internal abstract string Name { get; }

    protected InteractionTrackerState(ServerInteractionTracker interactionTracker)
    {
        _interactionTracker = interactionTracker;
    }

    protected static bool AllowsOverscroll(IPointer pointer) =>
        pointer.Type is PointerType.Touch or PointerType.Pen;

    protected static Vector3D GetWheelImpulseVelocity(Vector delta) =>
        new(delta.X / WheelImpulseDuration, delta.Y / WheelImpulseDuration, 0);

    protected abstract void EnterState();
    internal abstract void BeginUserManipulation(Point position, IPointer pointer);
    internal abstract void CompleteUserManipulation();
    internal abstract void AddScaleVelocity(Point origin, double delta, bool useInertia);
    internal abstract void ApplyManipulationDelta(Vector translationDelta);
    internal abstract void StartInertia(Vector linearVelocity, bool includeScaleVelocity);
    internal abstract void ApplyWheelDelta(Vector delta, bool useInertia);
    internal abstract void ReceiveBoundsUpdate();
    internal abstract void TryUpdatePositionWithAdditionalVelocity(Vector3D velocityInPixelsPerSecond, int requestId);
    internal abstract void TryUpdatePosition(Vector3D value, InteractionTrackerClampingOption option, int requestId);

    internal virtual void UpdateInertiaRestingPosition(Vector3D position, int requestId) =>
        _interactionTracker.NotifyRequestIgnored(requestId);

    internal virtual void StartAnimation(
        CompositionAnimation animation,
        int requestId,
        Vector3D? scaleCenterPoint = null)
    {
    }
}

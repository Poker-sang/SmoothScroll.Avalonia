using Avalonia;
using Avalonia.Animation;
using Avalonia.Rendering.Composition;
using Avalonia.Rendering.Composition.Animations;
using Avalonia.Rendering.Composition.Expressions;
using Avalonia.Rendering.Composition.Server;

namespace SmoothScroll.Avalonia.Interaction;

internal abstract class CustomAnimationHandler : ServerObject, IServerClockItem
{
    private readonly CompositionAnimation _animation;
    private readonly TimeSpan? _duration;
    private IAnimationInstance? _animationInstance;
    private TimeSpan _startTime;

    protected CustomAnimationHandler(
        ServerInteractionTracker interactionTracker,
        CompositionAnimation animation,
        int requestId,
        ServerCompositor compositor) : base(compositor)
    {
        InteractionTracker = interactionTracker;
        RequestId = requestId;
        _animation = animation;
        _duration = (animation as KeyFrameAnimation)?.Duration;
    }

    protected ServerInteractionTracker InteractionTracker { get; }

    protected int RequestId { get; }

    public void Start()
    {
        var targetProperty = InteractionTracker.GetCompositionProperty(_animation.Target!)!;
        _animationInstance = _animation.CreateInstance(InteractionTracker, null);
        _animationInstance.Initialize(
            Compositor.Clock.Elapsed,
            targetProperty.GetVariant!(InteractionTracker),
            targetProperty);
        _startTime = Compositor.Clock.Elapsed;
        Compositor.Animations.AddToClock(this);
        Activate();
    }

    public void Stop()
    {
        Compositor.Animations.RemoveFromClock(this);
        Deactivate();
    }

    public void OnTick()
    {
        if (_animationInstance is null)
            return;

        var elapsed = Compositor.Clock.Elapsed;
        var completed = _duration is { } duration && elapsed - _startTime >= duration;
        var evaluationTime = completed ? _startTime + _duration!.Value : elapsed;
        var value = _animationInstance.Evaluate(evaluationTime, GetCurrentValue());
        Evaluate(value);

        if (!completed)
            return;

        Stop();
        InteractionTracker.ChangeState(new IdleState(InteractionTracker, RequestId));
    }

    protected abstract ExpressionVariant GetCurrentValue();

    protected abstract void Evaluate(ExpressionVariant animationValue);
}

internal sealed class ScaleAnimationHandler : CustomAnimationHandler
{
    private readonly Vector3D _centerPoint;

    public ScaleAnimationHandler(
        ServerInteractionTracker interactionTracker,
        CompositionAnimation animation,
        Vector3D centerPoint,
        int requestId,
        ServerCompositor compositor) : base(interactionTracker, animation, requestId, compositor)
    {
        _centerPoint = centerPoint;
    }

    protected override ExpressionVariant GetCurrentValue() => InteractionTracker.Scale;

    protected override void Evaluate(ExpressionVariant animationValue)
    {
        var scale = Math.Clamp(
            animationValue.Double,
            InteractionTracker.MinScale,
            InteractionTracker.MaxScale);
        InteractionTracker.SetScale(scale, _centerPoint, RequestId);
    }
}

internal sealed class PositionAnimationHandler : CustomAnimationHandler
{
    public PositionAnimationHandler(
        ServerInteractionTracker interactionTracker,
        CompositionAnimation animation,
        int requestId,
        ServerCompositor compositor) : base(interactionTracker, animation, requestId, compositor)
    {
    }

    protected override ExpressionVariant GetCurrentValue() => InteractionTracker.Position;

    protected override void Evaluate(ExpressionVariant animationValue)
    {
        var position = Vector3D.Clamp(
            animationValue.Vector3D,
            InteractionTracker.MinPosition,
            InteractionTracker.MaxPosition);
        InteractionTracker.SetPosition(position, RequestId);
    }
}

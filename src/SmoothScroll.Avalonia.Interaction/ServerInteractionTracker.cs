using System.Diagnostics;
using Avalonia;
using Avalonia.Rendering.Composition.Transport;
using Avalonia.Threading;
using Avalonia.Utilities;

namespace SmoothScroll.Avalonia.Interaction;

internal partial class ServerInteractionTracker : IDisposable
{
    private int _count;
    private InteractionTrackerState? _state;
    private InteractionTracker? _client;
    private readonly Queue<Action<InteractionTracker>> _pendingClientActions = [];
    private InteractionTrackerContentBounds? _contentBounds;

    internal double OverscrollElasticity { get; private set; } = 0.5;

    internal double OverscrollBounceRate { get; private set; } = 1;


    partial void Initialize()
    {
        _scale = 1;
        _positionInertiaDecayRate = new Vector3D(0.95, 0.95, 0.95);
        _scaleInertiaDecayRate = 0.95;
    }

    partial void OnFieldsDeserialized(InteractionTrackerChangedFields changed)
    {
        const InteractionTrackerChangedFields boundsFields =
            InteractionTrackerChangedFields.MinPosition
            | InteractionTrackerChangedFields.MinPositionAnimated
            | InteractionTrackerChangedFields.MaxPosition
            | InteractionTrackerChangedFields.MaxPositionAnimated;

        if ((changed & boundsFields) is not 0)
            State.ReceiveBoundsUpdate();
    }

    public void AttachClient(InteractionTracker client)
    {
        if (_disposed)
            return;
        _client = client;
        Activate();
        _state ??= new IdleState(this, requestId: 0, isInitialIdleState: true);

        // Server jobs and composition batches use separate queues, so an immediate tracker
        // request can execute before this attachment job. Preserve those notifications in order.
        while (_pendingClientActions.TryDequeue(out var action))
            DispatchToClient(client, action);
    }

    internal void SetPosition(Vector3D newPosition, int requestId, bool notifyParticipant = true)
    {
        if (Position == newPosition)
            return;
        var previousY = Position.Y;
        Position = newPosition;
        NotifyValuesChanged(newPosition, Scale, requestId);
        if (notifyParticipant && previousY != newPosition.Y)
            _movementParticipant?.OnPositionChanged(newPosition.Y);
    }

    internal void SetScale(double newScale, Vector3D centerPoint, int requestId)
    {
        if (MathUtilities.AreClose(Scale, newScale))
            return;

        var scaleRatio = newScale / Scale;
        var currentPosition = Position;
        var deltaX = (centerPoint.X - (-currentPosition.X)) * (1 - scaleRatio);
        var deltaY = (centerPoint.Y - (-currentPosition.Y)) * (1 - scaleRatio);

        var scaledNewPosition = new Vector3D(
            currentPosition.X - deltaX,
            currentPosition.Y - deltaY,
            currentPosition.Z);

        Scale = newScale;
        if (UpdateContentBounds())
            scaledNewPosition = Vector3D.Clamp(scaledNewPosition, MinPosition, MaxPosition);
        Position = scaledNewPosition;

        // Scale, bounds and position must agree before either inertia or rendering observes them.
        if (_contentBounds is not null)
            State.ReceiveBoundsUpdate();

        NotifyValuesChanged(scaledNewPosition, newScale, requestId);
    }

    private bool UpdateContentBounds()
    {
        if (_contentBounds is not { } bounds)
            return false;

        (MinPosition, MaxPosition) = bounds.Calculate(Scale);
        return true;
    }

    private void ConfigureContentBounds(InteractionTrackerContentBounds? bounds)
    {
        _contentBounds = bounds;
        if (!UpdateContentBounds())
            return;

        SetPosition(ClampPosition(Position), requestId: 0);
        State.ReceiveBoundsUpdate();
    }

    internal void ChangeState(InteractionTrackerState newState)
    {
        _ = Interlocked.Increment(ref _count);
        WriteStateTransition(_count, _state?.Name ?? "<none>", newState.Name);
        _state = newState;
    }

    internal void NotifyIdleStateEntered(int requestId, bool isFromBinding, bool isInitialIdleState)
    {
        if (isInitialIdleState)
            return;

        PostToClient(client => client.RaiseIdleStateEntered(requestId, isFromBinding));
    }

    internal void NotifyInteractingStateEntered(int requestId, bool isFromBinding)
        => PostToClient(client => client.RaiseInteractingStateEntered(requestId, isFromBinding));

    internal void NotifyCustomAnimationStateEntered()
        => PostToClient(client => client.RaiseCustomAnimationStateEntered());

    internal void NotifyInertiaStateEntered(
        Vector3D modifiedRestingPosition,
        double modifiedRestingScale,
        Vector3D naturalRestingPosition,
        double naturalRestingScale,
        Vector3D positionVelocityInPixelsPerSecond,
        int requestId,
        float scaleVelocityInPercentPerSecond,
        bool isInertiaFromImpulse,
        bool isFromBinding)
    {
        PostToClient(client => client.RaiseInertiaStateEntered(
            modifiedRestingPosition,
            modifiedRestingScale,
            naturalRestingPosition,
            naturalRestingScale,
            positionVelocityInPixelsPerSecond,
            requestId,
            scaleVelocityInPercentPerSecond,
            isInertiaFromImpulse,
            isFromBinding));
    }

    internal void NotifyRequestIgnored(int requestId)
        => PostToClient(client => client.RaiseRequestIgnored(requestId));

    internal void ConfigurePhysics(double overscrollElasticity, double overscrollBounceRate)
    {
        OverscrollElasticity = Math.Clamp(overscrollElasticity, 0, 1);
        OverscrollBounceRate = Math.Max(overscrollBounceRate, 0.01);
    }

    private InteractionTrackerState State => _state ??= new IdleState(this, requestId: 0, isInitialIdleState: true);

    private void NotifyValuesChanged(Vector3D position, double scale, int requestId)
        => PostToClient(client => client.RaiseValuesChanged(position, scale, requestId));

    private void PostToClient(Action<InteractionTracker> action)
    {
        if (_client is not { } client)
        {
            _pendingClientActions.Enqueue(action);
            return;
        }

        DispatchToClient(client, action);
    }

    private static void DispatchToClient(InteractionTracker client, Action<InteractionTracker> action) =>
        Dispatcher.UIThread.Post(() => action(client), DispatcherPriority.Render);

    [Conditional("INTERACTION_TRACKER_TRACE")]
    private static void WriteStateTransition(int count, string previousState, string newState)
    {
        Debug.WriteLine($"{count}:{previousState} -> {newState}");
    }

    partial void DeserializeRequests(BatchStreamReader reader)
    {
        var requestCount = reader.Read<int>();
        for (var i = 0; i < requestCount; i++)
        {
            var request = reader.ReadObject();
            if (_disposed)
                continue;
            // The opt-in first stage is deliberately vertical and cannot mix with zoom or snap retargeting.
            if (HasExperimentalVerticalScroll && request is TryUpdateScaleRequest or AddScaleVelocityRequest or UpdateInertiaRestingPositionRequest
                || HasExperimentalVerticalScroll && request is StartAnimationRequest { ScaleCenterPoint: not null })
            {
                NotifyRequestIgnored(((InteractionTrackerRequest)request).RequestId);
                continue;
            }
            switch (request)
            {
                case ConfigureExperimentalVerticalScrollRequest configureMovement:
                    ConfigureExperimentalVerticalScroll(configureMovement.Factory);
                    break;
                case ExperimentalCancelScrollRequest:
                    CancelExperimentalScroll();
                    break;
                case ExperimentalCompleteScrollRequest:
                    State.CancelMovement();
                    NotifyExperimentalIdle(Experimental.ExperimentalScrollMovementSource.Direct);
                    break;
                case ExperimentalParticipantUpdateRequest update:
                    if (_movementParticipant is { } participant)
                    {
                        update.Update(participant);
                        SetPosition(ClampPosition(Position), update.RequestId, notifyParticipant: false);
                        participant.OnPositionChanged(Position.Y);
                        EnsureExperimentalParticipantFrames();
                    }
                    break;
                case ExperimentalVerticalDeltaRequest directMovement:
                    if (HasExperimentalVerticalScroll)
                    {
                        CancelExperimentalInertiaForInput();
                        ApplyExperimentalInput(directMovement.Delta, Experimental.ExperimentalScrollMovementSource.Direct, InteractionTrackerValuesChangedArgs.UserRequestId);
                    }
                    else
                    {
                        State.ApplyWheelDelta(new Vector(0, directMovement.Delta), false);
                    }
                    break;
                case TryUpdatePositionRequest tryUpdatePositionRequest:
                    _participantFrameClock?.Stop();
                    _movementParticipant?.OnCancelled();
                    State.TryUpdatePosition(tryUpdatePositionRequest.Position, tryUpdatePositionRequest.ClampingOption, tryUpdatePositionRequest.RequestId);
                    break;
                case TryUpdateScaleRequest tryUpdateScaleRequest:
                    State.TryUpdateScale(
                        tryUpdateScaleRequest.Scale,
                        tryUpdateScaleRequest.CenterPoint,
                        tryUpdateScaleRequest.RequestId);
                    break;
                case BeginUserManipulationRequest beginUserManipulationRequest:
                    if (HasExperimentalVerticalScroll)
                    {
                        _participantFrameClock?.Stop();
                        _movementParticipant!.OnCancelled();
                    }
                    State.BeginUserManipulation(beginUserManipulationRequest.Position, beginUserManipulationRequest.Pointer);
                    break;
                case CompleteManipulationRequest completeManipulationRequest:
                    State.CompleteUserManipulation();
                    break;
                case ApplyManipulationDeltaRequest applyManipulationDeltaRequest:
                    State.ApplyManipulationDelta(applyManipulationDeltaRequest.TranslationDelta);
                    break;
                case StartInertiaRequest startInertiaRequest:
                    _participantFrameClock?.Stop();
                    State.StartInertia(
                        startInertiaRequest.LinearVelocity,
                        !HasExperimentalVerticalScroll && startInertiaRequest.IncludeScaleVelocity);
                    break;
                case AddScaleVelocityRequest addScaleVelocityRequest:
                    State.AddScaleVelocity(
                        addScaleVelocityRequest.Origin,
                        addScaleVelocityRequest.Delta,
                        addScaleVelocityRequest.UseInertia);
                    break;
                case ApplyWheelDeltaRequest applyWheelDeltaRequest:
                    if (HasExperimentalVerticalScroll && !applyWheelDeltaRequest.UseInertia)
                        CancelExperimentalInertiaForInput();
                    else if (HasExperimentalVerticalScroll && _participantFrameClock?.IsRunning == true)
                    {
                        _participantFrameClock.Stop();
                        _movementParticipant!.OnCancelled();
                    }
                    State.ApplyWheelDelta(
                        applyWheelDeltaRequest.Delta,
                        applyWheelDeltaRequest.UseInertia);
                    break;
                case UpdateInertiaRestingPositionRequest updateInertiaRestingPositionRequest:
                    State.UpdateInertiaRestingPosition(
                        updateInertiaRestingPositionRequest.Position,
                        updateInertiaRestingPositionRequest.RequestId);
                    break;
                case StartAnimationRequest startAnimationRequest:
                    _participantFrameClock?.Stop();
                    _movementParticipant?.OnCancelled();
                    State.StartAnimation(
                        startAnimationRequest.Animation,
                        startAnimationRequest.RequestId,
                        startAnimationRequest.ScaleCenterPoint);
                    break;
                case ConfigurePhysicsRequest configurePhysicsRequest:
                    ConfigurePhysics(
                        configurePhysicsRequest.OverscrollElasticity,
                        configurePhysicsRequest.OverscrollBounceRate);
                    break;
                case ConfigureContentBoundsRequest configureContentBoundsRequest:
                    ConfigureContentBounds(configureContentBoundsRequest.Bounds);
                    break;
            }
        }
    }
}

using Avalonia;

namespace SmoothScroll.Avalonia.Controls;

public sealed partial class ScrollView
{
    /// <summary>
    /// Indicates that a navigation request did not create an operation.
    /// </summary>
    public const int NoCorrelationId = -1;

    /// <summary>
    /// Defines the <see cref="State"/> property.
    /// </summary>
    public static readonly DirectProperty<ScrollView, ScrollingInteractionState> StateProperty =
        AvaloniaProperty.RegisterDirect<ScrollView, ScrollingInteractionState>(nameof(State), view => view.State);

    private int _nextCorrelationId;
    private ScrollingInteractionState _state;
    private ActiveScrollOperation? _activeScrollOperation;
    private ActiveZoomOperation? _activeZoomOperation;
    private OperationRequestContext? _offsetChangeContext;
    private OperationRequestContext? _zoomChangeContext;

    /// <summary>
    /// Occurs when a scroll operation is accepted.
    /// </summary>
    public event EventHandler<ScrollingScrollStartingEventArgs>? ScrollStarting;

    /// <summary>
    /// Occurs when a scroll operation completes, is interrupted, or is ignored.
    /// </summary>
    public event EventHandler<ScrollingScrollCompletedEventArgs>? ScrollCompleted;

    /// <summary>
    /// Occurs when a zoom operation is accepted.
    /// </summary>
    public event EventHandler<ScrollingZoomStartingEventArgs>? ZoomStarting;

    /// <summary>
    /// Occurs when a zoom operation completes, is interrupted, or is ignored.
    /// </summary>
    public event EventHandler<ScrollingZoomCompletedEventArgs>? ZoomCompleted;

    /// <summary>
    /// Occurs when <see cref="State"/> changes.
    /// </summary>
    public event EventHandler? StateChanged;

    /// <summary>
    /// Gets the current direct-manipulation, inertia, or animation state.
    /// </summary>
    public ScrollingInteractionState State => _state;

    internal int BeginScrollOperation(
        Vector startingOffset,
        Vector targetOffset,
        bool isAnimated,
        ScrollChangeSource source)
    {
        CompleteScrollOperation(_activeScrollOperation?.CorrelationId ?? NoCorrelationId, ScrollingOperationResult.Interrupted);

        var operation = new ActiveScrollOperation(NextCorrelationId(), source);
        _activeScrollOperation = operation;
        ScrollStarting?.Invoke(
            this,
            new ScrollingScrollStartingEventArgs(
                operation.CorrelationId,
                startingOffset,
                targetOffset,
                isAnimated,
                source));
        return operation.CorrelationId;
    }

    internal int BeginZoomOperation(
        double startingZoomFactor,
        double targetZoomFactor,
        Point? centerPoint,
        bool isAnimated,
        ScrollChangeSource source)
    {
        CompleteZoomOperation(_activeZoomOperation?.CorrelationId ?? NoCorrelationId, ScrollingOperationResult.Interrupted);

        var operation = new ActiveZoomOperation(NextCorrelationId(), source);
        _activeZoomOperation = operation;
        ZoomStarting?.Invoke(
            this,
            new ScrollingZoomStartingEventArgs(
                operation.CorrelationId,
                startingZoomFactor,
                targetZoomFactor,
                centerPoint,
                isAnimated,
                source));
        return operation.CorrelationId;
    }

    internal bool IsScrollOperationActive(int correlationId) =>
        _activeScrollOperation?.CorrelationId == correlationId;

    internal bool IsZoomOperationActive(int correlationId) =>
        _activeZoomOperation?.CorrelationId == correlationId;

    internal void CompleteScrollOperation(int correlationId, ScrollingOperationResult result)
    {
        if (_activeScrollOperation is not { } operation || operation.CorrelationId != correlationId)
            return;

        _activeScrollOperation = null;
        ScrollCompleted?.Invoke(
            this,
            new ScrollingScrollCompletedEventArgs(
                operation.CorrelationId,
                result,
                Offset,
                operation.Source));
    }

    internal void CompleteZoomOperation(int correlationId, ScrollingOperationResult result)
    {
        if (_activeZoomOperation is not { } operation || operation.CorrelationId != correlationId)
            return;

        _activeZoomOperation = null;
        ZoomCompleted?.Invoke(
            this,
            new ScrollingZoomCompletedEventArgs(
                operation.CorrelationId,
                result,
                ZoomFactor,
                operation.Source));
    }

    internal void InterruptOperations()
    {
        CompleteScrollOperation(
            _activeScrollOperation?.CorrelationId ?? NoCorrelationId,
            ScrollingOperationResult.Interrupted);
        CompleteZoomOperation(
            _activeZoomOperation?.CorrelationId ?? NoCorrelationId,
            ScrollingOperationResult.Interrupted);
    }

    internal void UpdateInteractionState(ScrollingInteractionState state)
    {
        if (!SetAndRaise(StateProperty, ref _state, state))
            return;

        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    private int NextCorrelationId()
    {
        if (_nextCorrelationId is int.MaxValue)
            _nextCorrelationId = 0;

        return ++_nextCorrelationId;
    }

    private sealed record ActiveScrollOperation(int CorrelationId, ScrollChangeSource Source);

    private sealed record ActiveZoomOperation(int CorrelationId, ScrollChangeSource Source);

    private sealed class OperationRequestContext(ScrollChangeSource source)
    {
        public ScrollChangeSource Source { get; } = source;

        public int CorrelationId { get; set; } = NoCorrelationId;
    }
}

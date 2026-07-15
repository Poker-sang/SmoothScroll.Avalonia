using Avalonia;

namespace SmoothScroll.Avalonia.Controls;

/// <summary>
/// Describes a zoom operation that is starting.
/// </summary>
public sealed class ScrollingZoomStartingEventArgs(
    int correlationId,
    double startingZoomFactor,
    double targetZoomFactor,
    Point? centerPoint,
    bool isAnimated,
    ScrollChangeSource changeSource) : EventArgs
{
    /// <summary>
    /// Gets the identifier shared with the corresponding completion event.
    /// </summary>
    public int CorrelationId { get; } = correlationId;

    /// <summary>
    /// Gets the zoom factor at which the operation started.
    /// </summary>
    public double StartingZoomFactor { get; } = startingZoomFactor;

    /// <summary>
    /// Gets the requested zoom factor after coercion.
    /// </summary>
    public double TargetZoomFactor { get; } = targetZoomFactor;

    /// <summary>
    /// Gets the viewport-relative zoom center, or <see langword="null"/> when no presenter was available.
    /// </summary>
    public Point? CenterPoint { get; } = centerPoint;

    /// <summary>
    /// Gets whether the operation was requested with animation.
    /// </summary>
    public bool IsAnimated { get; } = isAnimated;

    /// <summary>
    /// Gets the source that started the operation.
    /// </summary>
    public ScrollChangeSource ChangeSource { get; } = changeSource;
}

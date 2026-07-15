using Avalonia;

namespace SmoothScroll.Avalonia.Controls;

/// <summary>
/// Describes a scroll operation that is starting.
/// </summary>
public sealed class ScrollingScrollStartingEventArgs(
    int correlationId,
    Vector startingOffset,
    Vector targetOffset,
    bool isAnimated,
    ScrollChangeSource changeSource) : EventArgs
{
    /// <summary>
    /// Gets the identifier shared with the corresponding completion event.
    /// </summary>
    public int CorrelationId { get; } = correlationId;

    /// <summary>
    /// Gets the offset at which the operation started.
    /// </summary>
    public Vector StartingOffset { get; } = startingOffset;

    /// <summary>
    /// Gets the initially requested offset.
    /// </summary>
    /// <remarks>User interaction may continue beyond this initial value.</remarks>
    public Vector TargetOffset { get; } = targetOffset;

    /// <summary>
    /// Gets whether the operation was requested with animation.
    /// </summary>
    public bool IsAnimated { get; } = isAnimated;

    /// <summary>
    /// Gets the source that started the operation.
    /// </summary>
    public ScrollChangeSource ChangeSource { get; } = changeSource;
}

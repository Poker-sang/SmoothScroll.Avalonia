using Avalonia;

namespace SmoothScroll.Avalonia.Controls;

/// <summary>
/// Describes a scroll operation that has ended.
/// </summary>
public sealed class ScrollingScrollCompletedEventArgs(
    int correlationId,
    ScrollingOperationResult result,
    Vector finalOffset,
    ScrollChangeSource changeSource) : EventArgs
{
    /// <summary>
    /// Gets the identifier supplied by the corresponding starting event and navigation method.
    /// </summary>
    public int CorrelationId { get; } = correlationId;

    /// <summary>
    /// Gets how the operation ended.
    /// </summary>
    public ScrollingOperationResult Result { get; } = result;

    /// <summary>
    /// Gets the offset when the operation ended.
    /// </summary>
    public Vector FinalOffset { get; } = finalOffset;

    /// <summary>
    /// Gets the source that started the operation.
    /// </summary>
    public ScrollChangeSource ChangeSource { get; } = changeSource;
}

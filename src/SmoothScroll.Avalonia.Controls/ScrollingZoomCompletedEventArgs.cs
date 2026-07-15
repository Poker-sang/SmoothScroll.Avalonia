namespace SmoothScroll.Avalonia.Controls;

/// <summary>
/// Describes a zoom operation that has ended.
/// </summary>
public sealed class ScrollingZoomCompletedEventArgs(
    int correlationId,
    ScrollingOperationResult result,
    double finalZoomFactor,
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
    /// Gets the zoom factor when the operation ended.
    /// </summary>
    public double FinalZoomFactor { get; } = finalZoomFactor;

    /// <summary>
    /// Gets the source that started the operation.
    /// </summary>
    public ScrollChangeSource ChangeSource { get; } = changeSource;
}

namespace SmoothScroll.Avalonia.Controls;

/// <summary>
/// Identifies how a scroll or zoom operation ended.
/// </summary>
public enum ScrollingOperationResult
{
    /// <summary>
    /// The operation reached its requested or natural resting value.
    /// </summary>
    Completed,

    /// <summary>
    /// Another request, direct manipulation, or control teardown superseded the operation.
    /// </summary>
    Interrupted,

    /// <summary>
    /// The interaction tracker rejected the operation before applying it.
    /// </summary>
    Ignored
}

using Avalonia.Interactivity;

namespace SmoothScroll.Avalonia.Controls;

/// <summary>
/// Describes a change to a <see cref="ScrollView"/>'s zoom factor.
/// </summary>
public sealed class ZoomChangedEventArgs(
    double zoomFactorDelta,
    ScrollChangeSource source) : RoutedEventArgs
{
    /// <summary>
    /// Gets the difference between the current and previously reported zoom factors.
    /// </summary>
    public double ZoomFactorDelta { get; } = zoomFactorDelta;

    /// <summary>
    /// Gets what caused the zoom change.
    /// </summary>
    public ScrollChangeSource ChangeSource { get; } = source;

    /// <summary>
    /// Gets whether the zoom change originated from direct user input.
    /// </summary>
    public bool IsUserInitiated => ChangeSource is ScrollChangeSource.User;
}

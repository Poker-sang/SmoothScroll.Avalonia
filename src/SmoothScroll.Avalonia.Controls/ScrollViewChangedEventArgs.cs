using Avalonia;
using Avalonia.Interactivity;

namespace SmoothScroll.Avalonia.Controls;

/// <summary>
/// Describes a change to a <see cref="ScrollView"/>.
/// </summary>
public sealed class ScrollViewChangedEventArgs(
    Vector extentDelta,
    Vector offsetDelta,
    Vector viewportDelta,
    ScrollChangeSource source) : RoutedEventArgs
{
    public Vector ExtentDelta { get; } = extentDelta;

    public Vector OffsetDelta { get; } = offsetDelta;

    public Vector ViewportDelta { get; } = viewportDelta;

    public ScrollChangeSource ChangeSource { get; } = source;

    public bool IsUserInitiated => ChangeSource is ScrollChangeSource.User;
}

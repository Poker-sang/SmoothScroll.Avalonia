namespace SmoothScroll.Avalonia.Controls;

/// <summary>
/// Identifies what caused a <see cref="ScrollView.ScrollChanged"/> or
/// <see cref="ScrollView.ZoomChanged"/> notification.
/// </summary>
public enum ScrollChangeSource
{
    /// <summary>
    /// The content, viewport, anchoring, or another layout calculation changed the value.
    /// </summary>
    Layout,

    /// <summary>
    /// An application property write, navigation method, or bring-into-view request changed the value.
    /// </summary>
    Programmatic,

    /// <summary>
    /// Pointer, touch, wheel, scroll-bar, or keyboard input handled directly by the control changed the value.
    /// </summary>
    User
}

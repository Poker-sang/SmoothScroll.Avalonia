namespace SmoothScroll.Avalonia.Controls;

/// <summary>
/// Defines constants that specify scrolling behavior for the <see cref = "ScrollView"/> control.
/// </summary>
public enum ScrollMode
{
    /// <summary>
    /// Scrolling is enabled only when the content overflows the viewport.
    /// </summary>
    Auto,

    /// <summary>
    /// Scrolling is always enabled for the axis.
    /// </summary>
    Enabled,

    /// <summary>
    /// Scrolling is always disabled for the axis.
    /// </summary>
    Disabled
}

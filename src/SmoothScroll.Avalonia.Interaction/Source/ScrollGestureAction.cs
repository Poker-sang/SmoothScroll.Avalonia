namespace SmoothScroll.Avalonia.Interaction;

/// <summary>
/// Identifies how a matched pointer gesture changes scroll content.
/// </summary>
public enum ScrollGestureAction
{
    /// <summary>
    /// Leaves the matched gesture unhandled.
    /// </summary>
    None,

    /// <summary>
    /// Translates both enabled axes.
    /// </summary>
    Pan,

    /// <summary>
    /// Scrolls vertically when that axis has range, or horizontally when only that axis has range.
    /// </summary>
    AutoScroll,

    /// <summary>
    /// Scrolls only the horizontal axis.
    /// </summary>
    HorizontalScroll,

    /// <summary>
    /// Scrolls only the vertical axis.
    /// </summary>
    VerticalScroll,

    /// <summary>
    /// Changes the content zoom factor.
    /// </summary>
    Zoom
}

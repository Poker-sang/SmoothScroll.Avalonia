namespace SmoothScroll.Avalonia.Interaction;

/// <summary>
/// Identifies a physical pointer gesture that can be mapped to a scroll action.
/// </summary>
public enum ScrollInputGesture
{
    /// <summary>
    /// A one-contact touch or pen drag.
    /// </summary>
    TouchDrag,

    /// <summary>
    /// A two-contact touch or pen pinch.
    /// </summary>
    TouchPinch,

    /// <summary>
    /// A left mouse-button drag.
    /// </summary>
    MouseLeftDrag,

    /// <summary>
    /// A right mouse-button drag.
    /// </summary>
    MouseRightDrag,

    /// <summary>
    /// A middle mouse-button drag.
    /// </summary>
    MouseMiddleDrag,

    /// <summary>
    /// The vertical mouse-wheel component.
    /// </summary>
    MouseWheel,

    /// <summary>
    /// The horizontal or tilt-wheel component.
    /// </summary>
    MouseWheelTilt
}

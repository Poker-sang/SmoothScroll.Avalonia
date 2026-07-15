namespace SmoothScroll.Avalonia.Controls;

/// <summary>
/// Identifies the current interaction state of a <see cref="ScrollView"/>.
/// </summary>
public enum ScrollingInteractionState
{
    /// <summary>
    /// No interaction, inertia, or programmatic animation is active.
    /// </summary>
    Idle,

    /// <summary>
    /// Pointer or touch input is directly manipulating the view.
    /// </summary>
    Interaction,

    /// <summary>
    /// Translation, zoom, or boundary-return inertia is active.
    /// </summary>
    Inertia,

    /// <summary>
    /// A programmatic scroll or zoom animation is active.
    /// </summary>
    Animation
}

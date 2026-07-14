using Avalonia.Input;

namespace SmoothScroll.Avalonia.Interaction;

/// <summary>
/// Identifies one physical input gesture and an exact modifier-key combination.
/// </summary>
public readonly record struct ScrollGesture
{
    /// <summary>
    /// Initializes a new gesture key.
    /// </summary>
    /// <param name="inputGesture">The physical input gesture.</param>
    /// <param name="modifiers">The modifier keys that must match exactly.</param>
    public ScrollGesture(
        ScrollInputGesture inputGesture,
        KeyModifiers modifiers = KeyModifiers.None)
    {
        InputGesture = inputGesture;
        Modifiers = modifiers;
    }

    /// <summary>
    /// Gets the physical input gesture.
    /// </summary>
    public ScrollInputGesture InputGesture { get; }

    /// <summary>
    /// Gets the modifier keys that must match exactly.
    /// </summary>
    public KeyModifiers Modifiers { get; }
}

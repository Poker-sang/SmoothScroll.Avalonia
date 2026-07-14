using Avalonia.Collections;
using Avalonia.Input;

namespace SmoothScroll.Avalonia.Interaction;

/// <summary>
/// Maps unique <see cref="ScrollGesture"/> keys to the actions they perform.
/// </summary>
public sealed class ScrollGestureBindings : AvaloniaDictionary<ScrollGesture, ScrollGestureAction>
{
    /// <summary>
    /// Creates a new dictionary containing the default input mappings.
    /// </summary>
    /// <returns>A mutable dictionary containing the default mappings.</returns>
    public static ScrollGestureBindings CreateDefault() => new()
    {
        [new(ScrollInputGesture.TouchDrag)] = ScrollGestureAction.Pan,
        [new(ScrollInputGesture.TouchPinch)] = ScrollGestureAction.Zoom,
        [new(ScrollInputGesture.MouseLeftDrag)] = ScrollGestureAction.Pan,
        [new(ScrollInputGesture.MouseMiddleDrag)] = ScrollGestureAction.Pan,
        [new(ScrollInputGesture.MouseRightDrag)] = ScrollGestureAction.None,
        [new(ScrollInputGesture.MouseWheel)] = ScrollGestureAction.AutoScroll,
        [new(ScrollInputGesture.MouseWheel, KeyModifiers.Shift)] = ScrollGestureAction.HorizontalScroll,
        [new(ScrollInputGesture.MouseWheel, KeyModifiers.Control)] = ScrollGestureAction.Zoom,
        [new(ScrollInputGesture.MouseWheelTilt)] = ScrollGestureAction.HorizontalScroll
    };
}

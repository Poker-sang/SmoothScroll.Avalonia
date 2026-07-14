using Avalonia.Input;

namespace SmoothScroll.Avalonia.Interaction;

internal static class ScrollGestureResolver
{
    public static ScrollGestureAction Resolve(
        IReadOnlyDictionary<ScrollGesture, ScrollGestureAction> bindings,
        ScrollInputGesture gesture,
        KeyModifiers modifiers) =>
        bindings.TryGetValue(new ScrollGesture(gesture, modifiers), out var action) ? action : ScrollGestureAction.None;
}

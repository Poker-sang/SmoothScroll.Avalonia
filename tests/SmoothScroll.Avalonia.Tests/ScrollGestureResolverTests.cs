using Avalonia.Controls;
using Avalonia.Input;
using SmoothScroll.Avalonia.Controls;
using SmoothScroll.Avalonia.Interaction;

namespace SmoothScroll.Avalonia.Tests;

public sealed class ScrollGestureResolverTests
{
    [Theory]
    [InlineData(ScrollInputGesture.TouchDrag, ScrollGestureAction.Pan)]
    [InlineData(ScrollInputGesture.TouchPinch, ScrollGestureAction.Zoom)]
    [InlineData(ScrollInputGesture.MouseLeftDrag, ScrollGestureAction.None)]
    [InlineData(ScrollInputGesture.MouseMiddleDrag, ScrollGestureAction.Pan)]
    [InlineData(ScrollInputGesture.MouseRightDrag, ScrollGestureAction.None)]
    [InlineData(ScrollInputGesture.MouseWheelTilt, ScrollGestureAction.HorizontalScroll)]
    public void DefaultPointerBindingsResolveExpectedAction(
        ScrollInputGesture gesture,
        ScrollGestureAction expectedAction)
    {
        var action = ScrollGestureResolver.Resolve(
            ScrollGestureBindings.CreateDefault(),
            gesture,
            KeyModifiers.None);

        Assert.Equal(expectedAction, action);
    }

    [Theory]
    [InlineData(KeyModifiers.None, ScrollGestureAction.AutoScroll)]
    [InlineData(KeyModifiers.Shift, ScrollGestureAction.HorizontalScroll)]
    [InlineData(KeyModifiers.Control, ScrollGestureAction.Zoom)]
    [InlineData(KeyModifiers.Alt, ScrollGestureAction.None)]
    [InlineData(KeyModifiers.Control | KeyModifiers.Shift, ScrollGestureAction.None)]
    public void WheelBindingsMatchExactModifiers(
        KeyModifiers modifiers,
        ScrollGestureAction expectedAction)
    {
        var action = ScrollGestureResolver.Resolve(
            ScrollGestureBindings.CreateDefault(),
            ScrollInputGesture.MouseWheel,
            modifiers);

        Assert.Equal(expectedAction, action);
    }

    [Fact]
    public void IndexerReplacesAnExistingMappingWithoutDuplicatingItsKey()
    {
        var bindings = ScrollGestureBindings.CreateDefault();
        var key = new ScrollGesture(ScrollInputGesture.MouseLeftDrag);
        var originalCount = bindings.Count;
        bindings[key] = ScrollGestureAction.VerticalScroll;

        var action = ScrollGestureResolver.Resolve(
            bindings,
            ScrollInputGesture.MouseLeftDrag,
            KeyModifiers.None);

        Assert.Equal(ScrollGestureAction.VerticalScroll, action);
        Assert.Equal(originalCount, bindings.Count);
    }
}

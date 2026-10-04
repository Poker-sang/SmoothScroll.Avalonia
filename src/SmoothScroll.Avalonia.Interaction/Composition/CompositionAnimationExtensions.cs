using Avalonia.Rendering.Composition;
using Avalonia.Rendering.Composition.Animations;
using Avalonia.Rendering.Composition.Server;
using Avalonia.Rendering.Composition.Transport;
using Avalonia.Threading;

namespace SmoothScroll.Avalonia.Composition;

/// <summary>Initial evaluation support for invalidation-driven composition expressions.</summary>
public static class CompositionAnimationExtensions
{
    /// <summary>
    /// Creates a native solid color visual with color repaint invalidation restored for Avalonia 12.1.2.
    /// Its animation and rendering behavior otherwise remains the native implementation.
    /// </summary>
    public static CompositionSolidColorVisual CreateAnimatedSolidColorVisual(this Compositor compositor)
    {
        ArgumentNullException.ThrowIfNull(compositor);
        Dispatcher.UIThread.VerifyAccess();
        return new CompositionSolidColorVisual(compositor, new ColorInvalidatingVisual(compositor.Server));
    }

    /// <summary>Starts an animation and evaluates it in the next compositor job, including its initial value.</summary>
    public static void StartAndEvaluateAnimation(this CompositionVisual visual, string property, CompositionAnimation animation)
    {
        ArgumentNullException.ThrowIfNull(visual);
        ArgumentNullException.ThrowIfNull(animation);
        Dispatcher.UIThread.VerifyAccess();
        visual.StartAnimation(property, animation);
        var serverVisual = visual.Server;
        visual.Compositor.PostServerJob(() =>
        {
            serverVisual.Animations?.EvaluateAnimations();
            serverVisual.RecomputeOwnProperties();
        });
    }

    // Avalonia tag 12.1.2: ServerCompositionSolidColorVisual renders Color but supplies no
    // Color invalidation. ServerCompositionVisual's property masks cover base visual properties only.
    private sealed class ColorInvalidatingVisual(ServerCompositor compositor) : ServerCompositionSolidColorVisual(compositor)
    {
        public override void NotifyAnimatedValueChanged(CompositionProperty property)
        {
            base.NotifyAnimatedValueChanged(property);
            if (property == GetCompositionProperty("Color"))
                TriggerCompositionFieldsDirty();
        }

        protected override void DeserializeChangesCore(BatchStreamReader reader, TimeSpan committedAt)
        {
            var previous = Color;
            base.DeserializeChangesCore(reader, committedAt);
            if (Color != previous)
                TriggerCompositionFieldsDirty();
        }
    }
}

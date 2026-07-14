using Avalonia;
using Avalonia.Controls;

namespace SmoothScroll.Avalonia.Controls;

/// <summary>
/// Provides smooth-scroll-aware overloads for Avalonia's <c>BringIntoView</c> control extensions.
/// </summary>
public static class BringIntoViewExtensions
{
    extension(Control control)
    {
        /// <summary>
        /// Requests that the complete control be brought into view.
        /// </summary>
        /// <param name="isAnimated">Whether an ancestor <see cref="ScrollPresenter"/> should animate the scroll.</param>
        public void BringIntoView(bool isAnimated) =>
            RaiseBringIntoViewRequest(control, new Rect(control.Bounds.Size), isAnimated);

        /// <summary>
        /// Requests that a portion of the control be brought into view.
        /// </summary>
        /// <param name="targetRect">The control-relative rectangle to reveal.</param>
        /// <param name="isAnimated">Whether an ancestor <see cref="ScrollPresenter"/> should animate the scroll.</param>
        public void BringIntoView(Rect targetRect, bool isAnimated) =>
            RaiseBringIntoViewRequest(control, targetRect, isAnimated);
    }

    private static void RaiseBringIntoViewRequest(Control control, Rect targetRect, bool isAnimated)
    {
        ArgumentNullException.ThrowIfNull(control);
        if (!control.IsEffectivelyVisible)
            return;

        control.RaiseEvent(new SmoothScrollBringIntoViewRequestEventArgs(isAnimated)
        {
            RoutedEvent = Control.RequestBringIntoViewEvent,
            TargetObject = control,
            TargetRect = targetRect
        });
    }
}

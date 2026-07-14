using Avalonia.Controls;

namespace SmoothScroll.Avalonia.Controls;

internal sealed class SmoothScrollBringIntoViewRequestEventArgs(bool isAnimated) : RequestBringIntoViewEventArgs
{
    public bool IsAnimated { get; } = isAnimated;
}

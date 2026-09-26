using Avalonia;

namespace SmoothScroll.Avalonia.Interaction;

/// <summary>
/// Describes scale-independent content geometry used to constrain a tracker on the composition thread.
/// </summary>
/// <param name="Extent">The unscaled content extent.</param>
/// <param name="Viewport">The viewport size in pixels.</param>
/// <param name="Alignment">The horizontal and vertical alignment ratios, from zero to one.</param>
public readonly record struct InteractionTrackerContentBounds(Size Extent, Size Viewport, Vector Alignment)
{
    /// <summary>
    /// Calculates position bounds at the specified scale, including alignment of underflow content.
    /// </summary>
    public (Vector3D Minimum, Vector3D Maximum) Calculate(double scale)
    {
        var x = CalculateAxisRange(Extent.Width * scale, Viewport.Width, Alignment.X);
        var y = CalculateAxisRange(Extent.Height * scale, Viewport.Height, Alignment.Y);
        return (new Vector3D(x.Minimum, y.Minimum, 0), new Vector3D(x.Maximum, y.Maximum, 0));
    }

    /// <summary>
    /// Calculates the scrollable range or the aligned position when content fits within an axis.
    /// </summary>
    public static (double Minimum, double Maximum) CalculateAxisRange(double scaledExtent, double viewport, double alignment)
    {
        var overflow = scaledExtent - viewport;
        if (overflow >= 0)
            return (0, overflow);

        var alignedPosition = overflow * Math.Clamp(alignment, 0, 1);
        return (alignedPosition, alignedPosition);
    }
}

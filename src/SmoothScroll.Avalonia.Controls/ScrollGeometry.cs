using Avalonia;

namespace SmoothScroll.Avalonia.Controls;

internal static class ScrollGeometry
{
    public static (double Minimum, double Maximum) CalculateAxisRange(
        double scaledExtent,
        double viewport,
        double alignment)
    {
        var overflow = scaledExtent - viewport;
        if (overflow >= 0)
            return (0, overflow);

        var alignedPosition = overflow * Math.Clamp(alignment, 0, 1);
        return (alignedPosition, alignedPosition);
    }

    public static Vector ToTrackerPosition(
        Vector offset,
        Vector minimum,
        Vector maximum)
    {
        var position = offset + minimum;
        return new Vector(
            Math.Clamp(position.X, minimum.X, maximum.X),
            Math.Clamp(position.Y, minimum.Y, maximum.Y));
    }

    public static Vector FromTrackerPosition(
        Vector position,
        Vector minimum,
        Vector maximum)
    {
        var range = maximum - minimum;
        var offset = position - minimum;
        return new Vector(
            Math.Clamp(offset.X, 0, Math.Max(range.X, 0)),
            Math.Clamp(offset.Y, 0, Math.Max(range.Y, 0)));
    }

    public static Vector ClampTrackerPosition(
        Vector position,
        Vector minimum,
        Vector maximum) =>
        new(
            Math.Clamp(position.X, minimum.X, maximum.X),
            Math.Clamp(position.Y, minimum.Y, maximum.Y));
}

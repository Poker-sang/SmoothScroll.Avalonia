using Avalonia;
using SmoothScroll.Avalonia.Controls;

namespace SmoothScroll.Avalonia.Tests;

public sealed class ScrollGeometryTests
{
    [Theory]
    [InlineData(500, 1000, 0, 0)]
    [InlineData(500, 1000, 0.5, -250)]
    [InlineData(500, 1000, 1, -500)]
    public void SmallerContentHasOneAlignedTrackerPosition(
        double extent,
        double viewport,
        double alignment,
        double expectedPosition)
    {
        var range = ScrollGeometry.CalculateAxisRange(extent, viewport, alignment);

        Assert.Equal(expectedPosition, range.Minimum);
        Assert.Equal(expectedPosition, range.Maximum);
    }

    [Fact]
    public void LargerContentUsesZeroBasedTrackerRange()
    {
        var range = ScrollGeometry.CalculateAxisRange(1400, 1000, 0.5);

        Assert.Equal(0, range.Minimum);
        Assert.Equal(400, range.Maximum);
    }

    [Fact]
    public void CenteredTrackerPositionMapsToZeroPublicOffset()
    {
        var minimum = new Vector(-250, -100);
        var maximum = minimum;

        var trackerPosition = ScrollGeometry.ToTrackerPosition(default, minimum, maximum);
        var offset = ScrollGeometry.FromTrackerPosition(trackerPosition, minimum, maximum);

        Assert.Equal(minimum, trackerPosition);
        Assert.Equal(default, offset);
    }

    [Fact]
    public void CenteredContentMovesWithResizedViewport()
    {
        var resizedMinimum = new Vector(-500, -300);

        var position = ScrollGeometry.ClampTrackerPosition(
            new Vector(-250, -100),
            resizedMinimum,
            resizedMinimum);

        Assert.Equal(resizedMinimum, position);
    }

    [Fact]
    public void ResizePreservesPositionInsideScrollableRange()
    {
        var position = ScrollGeometry.ClampTrackerPosition(
            new Vector(320, 180),
            default,
            new Vector(800, 600));

        Assert.Equal(new Vector(320, 180), position);
    }
}

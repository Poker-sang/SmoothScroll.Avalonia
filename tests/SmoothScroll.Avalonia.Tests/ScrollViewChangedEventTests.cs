using Avalonia;
using SmoothScroll.Avalonia.Controls;

namespace SmoothScroll.Avalonia.Tests;

public sealed class ScrollViewChangedEventTests
{
    [Fact]
    public void OffsetAssignmentIsProgrammatic()
    {
        var view = new ScrollView();
        ScrollViewChangedEventArgs? received = null;
        view.ScrollChanged += (_, args) => received = args;

        view.Offset = new Vector(0, 20);

        Assert.NotNull(received);
        Assert.Equal(ScrollChangeSource.Programmatic, received.ChangeSource);
        Assert.False(received.IsUserInitiated);
    }

    [Fact]
    public void ScrollByIsProgrammaticAndClamped()
    {
        var view = new ScrollView();
        ScrollViewChangedEventArgs? received = null;
        view.ScrollChanged += (_, args) => received = args;
        view.UpdateFromPresenter(
            new Size(1200, 800),
            new Size(600, 400),
            default,
            1,
            ScrollChangeSource.Layout);

        view.ScrollBy(new Vector(1000, 1000));

        Assert.NotNull(received);
        Assert.Equal(new Vector(600, 400), view.Offset);
        Assert.Equal(ScrollChangeSource.Programmatic, received.ChangeSource);
        Assert.False(received.IsUserInitiated);
    }

    [Fact]
    public void ZoomChangedIdentifiesProgrammaticUserAndLayoutChanges()
    {
        var view = new ScrollView();
        ZoomChangedEventArgs? received = null;
        view.ZoomChanged += (_, args) => received = args;

        view.ZoomFactor = 2;

        Assert.NotNull(received);
        Assert.Equal(1, received.ZoomFactorDelta);
        Assert.Equal(ScrollChangeSource.Programmatic, received.ChangeSource);
        Assert.False(received.IsUserInitiated);

        view.UpdateFromPresenter(
            new Size(1200, 800),
            new Size(600, 400),
            default,
            2.5,
            ScrollChangeSource.User);

        Assert.NotNull(received);
        Assert.Equal(0.5, received.ZoomFactorDelta);
        Assert.Equal(ScrollChangeSource.User, received.ChangeSource);
        Assert.True(received.IsUserInitiated);

        view.UpdateFromPresenter(
            new Size(1200, 800),
            new Size(600, 400),
            default,
            3,
            ScrollChangeSource.Layout);

        Assert.NotNull(received);
        Assert.Equal(0.5, received.ZoomFactorDelta);
        Assert.Equal(ScrollChangeSource.Layout, received.ChangeSource);
        Assert.False(received.IsUserInitiated);
    }

    [Fact]
    public void ProgrammaticZoomAssignmentInsideUserNotificationHasIndependentSourceAndDelta()
    {
        var view = new ScrollView();
        var changes = new List<ZoomChangedEventArgs>();
        var assignedProgrammatically = false;
        view.ZoomChanged += (_, args) =>
        {
            changes.Add(args);
            if (assignedProgrammatically || args.ChangeSource is not ScrollChangeSource.User)
                return;

            assignedProgrammatically = true;
            view.ZoomFactor = 2.75;
        };

        view.UpdateFromPresenter(
            new Size(1200, 800),
            new Size(600, 400),
            default,
            2.5,
            ScrollChangeSource.User);

        Assert.Equal(2, changes.Count);
        Assert.Equal(ScrollChangeSource.User, changes[0].ChangeSource);
        Assert.Equal(1.5, changes[0].ZoomFactorDelta, 3);
        Assert.Equal(ScrollChangeSource.Programmatic, changes[1].ChangeSource);
        Assert.Equal(0.25, changes[1].ZoomFactorDelta, 3);
    }

    [Fact]
    public void PresenterGeometryChangeIsLayoutInitiated()
    {
        var view = new ScrollView();
        ScrollViewChangedEventArgs? received = null;
        view.ScrollChanged += (_, args) => received = args;

        view.UpdateFromPresenter(
            new Size(1200, 800),
            new Size(600, 400),
            default,
            1,
            ScrollChangeSource.Layout);

        Assert.NotNull(received);
        Assert.Equal(ScrollChangeSource.Layout, received.ChangeSource);
    }
}

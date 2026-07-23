using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Input.Raw;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.VisualTree;
using SmoothScroll.Avalonia.Controls;
using SmoothScroll.Avalonia.Interaction;

namespace SmoothScroll.Avalonia.Tests;

public sealed class ScrollViewHeadlessTests
{
    [AvaloniaFact]
    public void ContentIsCenteredOnFirstFrameAndAfterResize()
    {
        using var host = new ScrollViewHost(new Size(800, 600), new Size(200, 120));

        var initialFrame = host.Render();
        var initialBounds = FindRedBounds(initialFrame);
        AssertCentered(initialBounds, initialFrame.PixelSize);

        host.Window.Width = 1000;
        host.Window.Height = 720;

        var resizedFrame = host.Render();
        var resizedBounds = FindRedBounds(resizedFrame);
        AssertCentered(resizedBounds, resizedFrame.PixelSize);
    }

    [AvaloniaFact]
    public void ZoomingOverflowContentToUnderflowCentersWithoutPointerInput()
    {
        using var host = new ScrollViewHost(
            new Size(800, 600),
            new Size(1000, 1200),
            view =>
            {
                view.IsZoomEnabled = true;
                view.IsScrollInertiaEnabled = false;
            },
            renderInitially: false);
        ScrollingZoomCompletedEventArgs? zoomCompleted = null;
        host.View.ZoomCompleted += (_, args) => zoomCompleted = args;

        var zoomId = host.View.ZoomTo(0.5, isAnimated: false);

        var fittedFrame = host.Render();
        var fittedBounds = FindRedBounds(fittedFrame);

        AssertCentered(fittedBounds, fittedFrame.PixelSize);
        AssertVectorEqual(default, host.View.Offset);
        Assert.Equal(0.5, host.View.ZoomFactor, 3);
        Assert.Equal(zoomId, zoomCompleted?.CorrelationId);
        Assert.Equal(ScrollingOperationResult.Completed, zoomCompleted?.Result);
    }

    [AvaloniaFact]
    public void TemplatePropagatesInteractionConfiguration()
    {
        Assert.Null(new ScrollView().ScrollPresenter);

        var bindings = new ScrollGestureBindings
        {
            [new ScrollGesture(ScrollInputGesture.MouseRightDrag, KeyModifiers.Alt)] = ScrollGestureAction.Zoom
        };
        using var host = new ScrollViewHost(
            new Size(800, 600),
            new Size(1200, 900),
            view =>
            {
                view.GestureBindings = bindings;
                view.IsZoomEnabled = true;
                view.MinZoomFactor = 0.25;
                view.MaxZoomFactor = 6;
                view.IsHorizontalMeasureInfinite = true;
                view.IsVerticalMeasureInfinite = true;
                view.HorizontalScrollMode = ScrollMode.Disabled;
                view.VerticalScrollMode = ScrollMode.Enabled;
                view.IsScrollChainingEnabled = false;
                view.IsScrollInertiaEnabled = false;
                view.HorizontalSnapPointsType = SnapPointsType.Mandatory;
                view.VerticalSnapPointsType = SnapPointsType.MandatorySingle;
                view.HorizontalSnapPointsAlignment = SnapPointsAlignment.Center;
                view.VerticalSnapPointsAlignment = SnapPointsAlignment.Far;
                view.ScrollInputMultiplier = 1.5;
                view.ZoomInputMultiplier = 0.75;
                view.ScrollInertiaDecayRate = 0.9;
                view.ZoomInertiaDecayRate = 0.85;
                view.OverscrollElasticity = 0.3;
                view.OverscrollBounceRate = 1.8;
            });

        var presenter = Assert.IsType<ScrollPresenter>(host.View.Presenter);
        Assert.Same(presenter, host.View.ScrollPresenter);
        Assert.True(presenter.IsLoaded);
        Assert.Same(host.Content, presenter.Child);
        Assert.Same(bindings, presenter.GestureBindings);
        Assert.True(presenter.IsZoomEnabled);
        Assert.Equal(0.25, presenter.MinZoomFactor);
        Assert.Equal(6, presenter.MaxZoomFactor);
        Assert.True(presenter.IsHorizontalMeasureInfinite);
        Assert.True(presenter.IsVerticalMeasureInfinite);
        Assert.False(presenter.CanHorizontallyScroll);
        Assert.True(presenter.CanVerticallyScroll);
        Assert.Equal(ScrollMode.Disabled, presenter.ComputedHorizontalScrollMode);
        Assert.Equal(ScrollMode.Enabled, presenter.ComputedVerticalScrollMode);
        Assert.False(presenter.IsScrollChainingEnabled);
        Assert.False(ScrollViewer.GetIsScrollInertiaEnabled(presenter));
        Assert.Equal(SnapPointsType.Mandatory, presenter.HorizontalSnapPointsType);
        Assert.Equal(SnapPointsType.MandatorySingle, presenter.VerticalSnapPointsType);
        Assert.Equal(SnapPointsAlignment.Center, presenter.HorizontalSnapPointsAlignment);
        Assert.Equal(SnapPointsAlignment.Far, presenter.VerticalSnapPointsAlignment);
        Assert.Equal(1.5, presenter.ScrollInputMultiplier);
        Assert.Equal(0.75, presenter.ZoomInputMultiplier);
        Assert.Equal(0.9, presenter.ScrollInertiaDecayRate);
        Assert.Equal(0.85, presenter.ZoomInertiaDecayRate);
        Assert.Equal(0.3, presenter.OverscrollElasticity);
        Assert.Equal(1.8, presenter.OverscrollBounceRate);
    }

    [AvaloniaFact]
    public void DefaultInertiaRatesMatchLegacyWheelAndZoomHalfLife()
    {
        using var host = new ScrollViewHost(new Size(800, 600), new Size(1200, 900));

        var expectedRate = Math.Pow(0.5, 1.0 / (60 * 0.08));
        var presenter = Assert.IsType<ScrollPresenter>(host.View.Presenter);

        Assert.Equal(expectedRate, host.View.ScrollInertiaDecayRate, 12);
        Assert.Equal(expectedRate, host.View.ZoomInertiaDecayRate, 12);
        Assert.Equal(expectedRate, presenter.ScrollInertiaDecayRate, 12);
        Assert.Equal(expectedRate, presenter.ZoomInertiaDecayRate, 12);
    }

    [AvaloniaFact]
    public void AxisModesAndScrollBarVisibilityAreIndependent()
    {
        using var host = new ScrollViewHost(
            new Size(800, 600),
            new Size(200, 900),
            view =>
            {
                view.HorizontalScrollBarVisibility = ScrollBarVisibilityMode.Auto;
                view.VerticalScrollBarVisibility = ScrollBarVisibilityMode.Auto;
            });
        var presenter = Assert.IsType<ScrollPresenter>(host.View.Presenter);
        var horizontalScrollBar = host.GetScrollBar(Orientation.Horizontal);
        var verticalScrollBar = host.GetScrollBar(Orientation.Vertical);

        Assert.False(presenter.CanHorizontallyScroll);
        Assert.True(presenter.CanVerticallyScroll);
        Assert.Equal(ScrollMode.Disabled, presenter.ComputedHorizontalScrollMode);
        Assert.Equal(ScrollMode.Enabled, presenter.ComputedVerticalScrollMode);
        Assert.Equal(ScrollMode.Disabled, host.View.ComputedHorizontalScrollMode);
        Assert.Equal(ScrollMode.Enabled, host.View.ComputedVerticalScrollMode);
        Assert.Equal(ScrollBarVisibilityMode.Hidden, host.View.ComputedHorizontalScrollBarVisibility);
        Assert.Equal(ScrollBarVisibilityMode.Visible, host.View.ComputedVerticalScrollBarVisibility);
        Assert.False(((IScrollable)host.View).CanHorizontallyScroll);
        Assert.True(((IScrollable)host.View).CanVerticallyScroll);
        Assert.Equal(ScrollBarVisibility.Hidden, horizontalScrollBar.Visibility);
        Assert.Equal(ScrollBarVisibility.Visible, verticalScrollBar.Visibility);
        Assert.True(horizontalScrollBar.AllowAutoHide);
        Assert.True(verticalScrollBar.AllowAutoHide);
        Assert.Equal(host.View.Viewport.Width, horizontalScrollBar.LargeChange);
        Assert.Equal(host.View.Viewport.Height, verticalScrollBar.LargeChange);
        Assert.Equal(16, horizontalScrollBar.SmallChange);
        Assert.Equal(16, verticalScrollBar.SmallChange);
        Assert.Equal(2, Grid.GetRowSpan(presenter));
        Assert.Equal(2, Grid.GetColumnSpan(presenter));

        host.View.HorizontalScrollMode = ScrollMode.Enabled;
        _ = host.Render();

        Assert.True(presenter.CanHorizontallyScroll);
        Assert.Equal(ScrollMode.Enabled, presenter.ComputedHorizontalScrollMode);
        Assert.Equal(ScrollMode.Enabled, host.View.ComputedHorizontalScrollMode);
        Assert.Equal(ScrollBarVisibilityMode.Visible, host.View.ComputedHorizontalScrollBarVisibility);
        Assert.True(((IScrollable)host.View).CanHorizontallyScroll);
        Assert.Equal(ScrollBarVisibility.Visible, horizontalScrollBar.Visibility);

        host.View.HorizontalScrollMode = ScrollMode.Disabled;
        host.View.HorizontalScrollBarVisibility = ScrollBarVisibilityMode.Visible;
        host.View.ScrollTo(new Vector(100, 0));
        _ = host.Render();

        Assert.False(presenter.CanHorizontallyScroll);
        Assert.Equal(ScrollMode.Disabled, presenter.ComputedHorizontalScrollMode);
        Assert.Equal(ScrollMode.Disabled, host.View.ComputedHorizontalScrollMode);
        Assert.Equal(ScrollBarVisibilityMode.Visible, host.View.ComputedHorizontalScrollBarVisibility);
        Assert.False(((IScrollable)host.View).CanHorizontallyScroll);
        Assert.Equal(ScrollBarVisibility.Visible, horizontalScrollBar.Visibility);
        Assert.Equal(0, host.View.Offset.X);

        host.View.VerticalScrollBarVisibility = ScrollBarVisibilityMode.Hidden;
        _ = host.Render();

        Assert.True(presenter.CanVerticallyScroll);
        Assert.Equal(ScrollMode.Enabled, host.View.ComputedVerticalScrollMode);
        Assert.Equal(ScrollBarVisibilityMode.Hidden, host.View.ComputedVerticalScrollBarVisibility);
        Assert.True(((IScrollable)host.View).CanVerticallyScroll);
        Assert.Equal(ScrollBarVisibility.Hidden, verticalScrollBar.Visibility);
    }

    [AvaloniaFact]
    public void OrdinaryScrollViewerUsesAutomaticScrollBarExpansion()
    {
        var content = new Border
        {
            Width = 1200,
            Height = 900,
            Background = Brushes.Red
        };
        var scrollViewer = new ScrollViewer
        {
            Width = 800,
            Height = 600,
            Content = content
        };
        var window = new Window
        {
            Width = 800,
            Height = 600,
            WindowDecorations = WindowDecorations.None,
            Content = scrollViewer
        };

        try
        {
            window.Show();
            _ = window.CaptureRenderedFrame();

            var scrollBars = scrollViewer.GetVisualDescendants().OfType<ScrollBar>().ToArray();
            Assert.Equal(2, scrollBars.Length);
            Assert.All(scrollBars, scrollBar => Assert.True(scrollBar.AllowAutoHide));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void AutoScrollModeAccountsForZoomedContent()
    {
        using var host = new ScrollViewHost(
            new Size(800, 600),
            new Size(200, 120),
            view =>
            {
                view.IsZoomEnabled = true;
                view.HorizontalScrollBarVisibility = ScrollBarVisibilityMode.Auto;
                view.VerticalScrollBarVisibility = ScrollBarVisibilityMode.Auto;
            });
        var presenter = Assert.IsType<ScrollPresenter>(host.View.Presenter);

        Assert.False(presenter.CanHorizontallyScroll);
        Assert.False(presenter.CanVerticallyScroll);

        host.View.ZoomTo(6, false);
        _ = host.Render();

        Assert.True(presenter.CanHorizontallyScroll);
        Assert.True(presenter.CanVerticallyScroll);

        host.View.ZoomTo(1, false);
        _ = host.Render();

        Assert.False(presenter.CanHorizontallyScroll);
        Assert.False(presenter.CanVerticallyScroll);
    }

    [AvaloniaFact]
    public void ExtentRemainsLogicalWhenContentIsZoomed()
    {
        using var host = new ScrollViewHost(
            new Size(800, 600),
            new Size(1600, 1200),
            view =>
            {
                view.IsZoomEnabled = true;
                view.HorizontalScrollMode = ScrollMode.Auto;
                view.VerticalScrollMode = ScrollMode.Auto;
            });

        var logicalExtent = host.View.LogicalExtent;
        Assert.Equal(new Size(1600, 1200), logicalExtent);
        Assert.Equal(logicalExtent, host.View.Extent);
        Assert.Equal(new Vector(800, 600), host.View.ScrollBarMaximum);

        host.View.ZoomTo(2, isAnimated: false);
        _ = host.Render();

        Assert.Equal(new Size(3200, 2400), host.View.Extent);
        Assert.Equal(logicalExtent, host.View.LogicalExtent);
        Assert.Equal(new Vector(2400, 1800), host.View.ScrollBarMaximum);

        host.View.ZoomTo(0.5, isAnimated: false);
        _ = host.Render();

        Assert.Equal(new Size(800, 600), host.View.Extent);
        Assert.Equal(logicalExtent, host.View.LogicalExtent);
        Assert.Equal(default, host.View.ScrollBarMaximum);
    }

    [AvaloniaFact]
    public void NoOpNavigationDoesNotCreateAnOperation()
    {
        using var host = new ScrollViewHost(
            new Size(800, 600),
            new Size(1600, 1200),
            view => view.IsZoomEnabled = true);
        var started = 0;
        var completed = 0;
        host.View.ScrollStarting += (_, _) => started++;
        host.View.ScrollCompleted += (_, _) => completed++;
        host.View.ZoomStarting += (_, _) => started++;
        host.View.ZoomCompleted += (_, _) => completed++;

        Assert.Equal(ScrollView.NoCorrelationId, host.View.ScrollTo(default, isAnimated: false));
        Assert.Equal(ScrollView.NoCorrelationId, host.View.ZoomTo(1, isAnimated: false));
        _ = host.Render();

        Assert.Equal(0, started);
        Assert.Equal(0, completed);
    }

    [AvaloniaFact]
    public void ImmediateOperationsUseMatchingCorrelationIdsAndOrderedEvents()
    {
        using var host = new ScrollViewHost(
            new Size(800, 600),
            new Size(1600, 1200),
            view =>
            {
                view.IsZoomEnabled = true;
                view.IsScrollInertiaEnabled = false;
            });
        var scrollEvents = new List<string>();
        var zoomEvents = new List<string>();
        ScrollingScrollStartingEventArgs? scrollStarting = null;
        ScrollingScrollCompletedEventArgs? scrollCompleted = null;
        ScrollingZoomStartingEventArgs? zoomStarting = null;
        ScrollingZoomCompletedEventArgs? zoomCompleted = null;
        host.View.ScrollStarting += (_, args) =>
        {
            scrollStarting = args;
            scrollEvents.Add("Starting");
        };
        host.View.ScrollChanged += (_, _) => scrollEvents.Add("Changed");
        host.View.ScrollCompleted += (_, args) =>
        {
            scrollCompleted = args;
            scrollEvents.Add("Completed");
        };
        host.View.ZoomStarting += (_, args) =>
        {
            zoomStarting = args;
            zoomEvents.Add("Starting");
        };
        host.View.ZoomChanged += (_, _) => zoomEvents.Add("Changed");
        host.View.ZoomCompleted += (_, args) =>
        {
            zoomCompleted = args;
            zoomEvents.Add("Completed");
        };

        var scrollId = host.View.ScrollTo(new Vector(120, 80), isAnimated: false);
        _ = host.Render();

        Assert.True(scrollId > 0);
        Assert.Equal(scrollId, scrollStarting?.CorrelationId);
        Assert.Equal(scrollId, scrollCompleted?.CorrelationId);
        Assert.Equal(ScrollingOperationResult.Completed, scrollCompleted?.Result);
        Assert.Equal(new Vector(120, 80), scrollCompleted?.FinalOffset);
        Assert.Equal(["Starting", "Changed", "Completed"], scrollEvents);

        var zoomId = host.View.ZoomTo(2, isAnimated: false);
        _ = host.Render();

        Assert.True(zoomId > scrollId);
        Assert.Equal(zoomId, zoomStarting?.CorrelationId);
        Assert.Equal(zoomId, zoomCompleted?.CorrelationId);
        Assert.Equal(ScrollingOperationResult.Completed, zoomCompleted?.Result);
        Assert.Equal(2, zoomCompleted?.FinalZoomFactor);
        Assert.Equal(["Starting", "Changed", "Completed"], zoomEvents);
        Assert.Equal(ScrollingInteractionState.Idle, host.View.State);
    }

    [AvaloniaFact]
    public void WritableOffsetAndZoomFactorCreateProgrammaticOperations()
    {
        using var host = new ScrollViewHost(
            new Size(800, 600),
            new Size(1600, 1200),
            view => view.IsZoomEnabled = true);
        ScrollingScrollStartingEventArgs? scrollStarting = null;
        ScrollingScrollCompletedEventArgs? scrollCompleted = null;
        ScrollingZoomStartingEventArgs? zoomStarting = null;
        ScrollingZoomCompletedEventArgs? zoomCompleted = null;
        host.View.ScrollStarting += (_, args) => scrollStarting = args;
        host.View.ScrollCompleted += (_, args) => scrollCompleted = args;
        host.View.ZoomStarting += (_, args) => zoomStarting = args;
        host.View.ZoomCompleted += (_, args) => zoomCompleted = args;

        host.View.Offset = new Vector(90, 70);
        _ = host.Render();
        host.View.ZoomFactor = 1.5;
        _ = host.Render();

        Assert.Equal(ScrollChangeSource.Programmatic, scrollStarting?.ChangeSource);
        Assert.Equal(scrollStarting?.CorrelationId, scrollCompleted?.CorrelationId);
        Assert.Equal(ScrollingOperationResult.Completed, scrollCompleted?.Result);
        Assert.Equal(ScrollChangeSource.Programmatic, zoomStarting?.ChangeSource);
        Assert.Equal(zoomStarting?.CorrelationId, zoomCompleted?.CorrelationId);
        Assert.Equal(ScrollingOperationResult.Completed, zoomCompleted?.Result);
    }

    [AvaloniaFact]
    public void NewAnimatedOperationInterruptsThePreviousTrackerOperation()
    {
        using var host = new ScrollViewHost(
            new Size(800, 600),
            new Size(1600, 1200),
            view => view.IsZoomEnabled = true);
        var scrollCompletions = new List<ScrollingScrollCompletedEventArgs>();
        var zoomCompletions = new List<ScrollingZoomCompletedEventArgs>();
        var states = new List<ScrollingInteractionState>();
        host.View.ScrollCompleted += (_, args) => scrollCompletions.Add(args);
        host.View.ZoomCompleted += (_, args) =>
        {
            Assert.Equal(ScrollingInteractionState.Idle, host.View.State);
            zoomCompletions.Add(args);
        };
        host.View.StateChanged += (_, _) => states.Add(host.View.State);

        var scrollId = host.View.ScrollTo(new Vector(700, 500), isAnimated: true);
        _ = host.Render();
        var zoomId = host.View.ZoomTo(2, isAnimated: true);

        for (var i = 0; i < 50; i++)
        {
            Thread.Sleep(10);
            _ = host.Render();
        }

        var scrollCompletion = Assert.Single(scrollCompletions);
        var zoomCompletion = Assert.Single(zoomCompletions);
        Assert.Equal(scrollId, scrollCompletion.CorrelationId);
        Assert.Equal(ScrollingOperationResult.Interrupted, scrollCompletion.Result);
        Assert.Equal(zoomId, zoomCompletion.CorrelationId);
        Assert.Equal(ScrollingOperationResult.Completed, zoomCompletion.Result);
        Assert.Contains(ScrollingInteractionState.Animation, states);
        Assert.Contains(ScrollingInteractionState.Idle, states);
    }

    [AvaloniaFact]
    public void NewAnimatedScrollInterruptsThePreviousScrollOperation()
    {
        using var host = new ScrollViewHost(
            new Size(800, 600),
            new Size(1600, 1200));
        var completions = new List<ScrollingScrollCompletedEventArgs>();
        host.View.ScrollCompleted += (_, args) => completions.Add(args);

        var firstId = host.View.ScrollTo(new Vector(700, 500), isAnimated: true);
        _ = host.Render();
        var secondId = host.View.ScrollTo(new Vector(100, 200), isAnimated: true);

        for (var i = 0; i < 50; i++)
        {
            Thread.Sleep(10);
            _ = host.Render();
        }

        Assert.Equal(2, completions.Count);
        Assert.Equal(firstId, completions[0].CorrelationId);
        Assert.Equal(ScrollingOperationResult.Interrupted, completions[0].Result);
        Assert.Equal(secondId, completions[1].CorrelationId);
        Assert.Equal(ScrollingOperationResult.Completed, completions[1].Result);
    }

    [AvaloniaFact]
    public void UserDragCreatesAnOperationAndReportsInteractionState()
    {
        using var host = new ScrollViewHost(
            new Size(800, 600),
            new Size(1600, 1200),
            view =>
            {
                view.IsScrollInertiaEnabled = false;
                view.GestureBindings = CreateLeftMousePanBindings();
            });
        var states = new List<ScrollingInteractionState>();
        ScrollingScrollStartingEventArgs? starting = null;
        ScrollingScrollCompletedEventArgs? completed = null;
        host.View.StateChanged += (_, _) => states.Add(host.View.State);
        host.View.ScrollStarting += (_, args) => starting = args;
        host.View.ScrollCompleted += (_, args) => completed = args;
        var center = new Point(400, 300);

        host.Window.MouseDown(center, MouseButton.Left);
        host.Window.MouseMove(new Point(320, 250), RawInputModifiers.LeftMouseButton);
        host.Window.MouseMove(new Point(280, 220), RawInputModifiers.LeftMouseButton);
        _ = host.Render();
        Assert.Equal(ScrollingInteractionState.Interaction, host.View.State);

        host.Window.MouseUp(new Point(280, 220), MouseButton.Left);
        _ = host.Render();

        Assert.NotNull(starting);
        Assert.Equal(ScrollChangeSource.User, starting.ChangeSource);
        Assert.NotNull(completed);
        Assert.Equal(starting.CorrelationId, completed.CorrelationId);
        Assert.Equal(ScrollingOperationResult.Completed, completed.Result);
        Assert.Contains(ScrollingInteractionState.Interaction, states);
        Assert.Equal(ScrollingInteractionState.Idle, host.View.State);
    }

    [AvaloniaFact]
    public void ActivePinchZoomQueuesThrottledArrangeBeforeContactsAreReleased()
    {
        using var host = new ScrollViewHost(
            new Size(800, 600),
            new Size(1400, 1000),
            view =>
            {
                view.IsZoomEnabled = true;
                view.IsScrollInertiaEnabled = false;
            });
        var presenter = Assert.IsType<ScrollPresenter>(host.View.Presenter);
        using var first = new TouchContact(host.Window, isPrimary: true);
        using var second = new TouchContact(host.Window, isPrimary: false);

        first.Press(new Point(300, 250));
        second.Press(new Point(400, 250));
        first.Move(new Point(250, 250));
        second.Move(new Point(450, 250));
        _ = host.Render();

        Assert.True(host.View.ZoomFactor > 1);
        Assert.True(
            presenter.HasPendingArrange,
            "Zoom changed on the composition thread without entering the throttled layout path.");
    }

    [AvaloniaFact]
    public void WritableZoomFactorIsImmediateClampedAndPreservedBeforeTemplateApplication()
    {
        using var host = new ScrollViewHost(
            new Size(800, 600),
            new Size(200, 120),
            view =>
            {
                view.MinZoomFactor = 0.5;
                view.MaxZoomFactor = 2;
                view.ZoomFactor = 4;
            });
        var presenter = Assert.IsType<ScrollPresenter>(host.View.Presenter);

        Assert.Equal(2, host.View.ZoomFactor);
        Assert.Equal(2, presenter.ZoomFactor);

        host.View.ZoomFactor = 0.1;
        _ = host.Render();

        Assert.Equal(0.5, host.View.ZoomFactor);
        Assert.Equal(0.5, presenter.ZoomFactor);
    }

    [AvaloniaFact]
    public void ZoomMethodsHonorViewportRelativeCenterPoint()
    {
        using var host = new ScrollViewHost(
            new Size(800, 600),
            new Size(1600, 1200),
            view =>
            {
                view.IsZoomEnabled = true;
                view.IsScrollInertiaEnabled = false;
            });

        host.View.ZoomTo(2, new Point(100, 150), isAnimated: false);
        _ = host.Render();

        Assert.Equal(2, host.View.ZoomFactor, 3);
        AssertVectorEqual(new Vector(100, 150), host.View.Offset);

        host.View.ZoomBy(1, new Point(200, 100), isAnimated: false);
        _ = host.Render();

        Assert.Equal(3, host.View.ZoomFactor, 3);
        AssertVectorEqual(new Vector(250, 275), host.View.Offset);
    }

    [AvaloniaFact]
    public void ZoomCenterMustBeFiniteEvenBeforeTemplateApplication()
    {
        var view = new ScrollView();

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            view.ZoomTo(2, new Point(double.NaN, 0), isAnimated: false));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            view.ZoomBy(1, new Point(0, double.PositiveInfinity), isAnimated: false));
    }

    [AvaloniaFact]
    public void AnchorRatioSelectsCandidateByTheMatchingElementPoint()
    {
        var cases = new[]
        {
            (Ratio: 0.0, PrimaryTop: 55.0, PrimaryHeight: 280.0, SecondaryTop: 90.0, SecondaryHeight: 20.0),
            (Ratio: 0.5, PrimaryTop: 60.0, PrimaryHeight: 280.0, SecondaryTop: 170.0, SecondaryHeight: 20.0),
            (Ratio: 1.0, PrimaryTop: 65.0, PrimaryHeight: 280.0, SecondaryTop: 290.0, SecondaryHeight: 20.0)
        };

        foreach (var item in cases)
        {
            using var host = new AnchorHost(
                item.Ratio,
                contentHeight: 1200,
                (item.PrimaryTop, item.PrimaryHeight),
                (item.SecondaryTop, item.SecondaryHeight));
            host.View.Offset = new Vector(0, 50);
            _ = host.Render();

            Assert.Same(host.Candidates[0], host.View.CurrentAnchor);
        }
    }

    [AvaloniaFact]
    public void AnchorRequestedCanOverrideAutomaticSelectionWithAReadOnlySnapshot()
    {
        using var host = new AnchorHost(0, contentHeight: 1200, (60, 40), (250, 40));
        host.View.Offset = new Vector(0, 50);
        _ = host.Render();

        var requestCount = 0;
        ScrollingAnchorRequestedEventArgs? capturedArgs = null;
        host.View.AnchorRequested += (sender, args) =>
        {
            Assert.Same(host.View, sender);
            requestCount++;
            capturedArgs = args;
            args.AnchorElement = host.Candidates[1];
        };

        host.View.VerticalAnchorRatio = 0.25;
        _ = host.Render();

        var request = Assert.IsType<ScrollingAnchorRequestedEventArgs>(capturedArgs);
        Assert.Equal(1, requestCount);
        Assert.Equal(2, request.AnchorCandidates.Count);
        Assert.Contains(host.Candidates[0], request.AnchorCandidates);
        Assert.Contains(host.Candidates[1], request.AnchorCandidates);
        var mutableView = Assert.IsAssignableFrom<IList<Control>>(request.AnchorCandidates);
        Assert.True(mutableView.IsReadOnly);
        Assert.Throws<NotSupportedException>(() => mutableView.Add(new Border()));
        Assert.Same(host.Candidates[1], host.View.CurrentAnchor);

        _ = host.View.CurrentAnchor;
        _ = host.View.CurrentAnchor;
        Assert.Equal(1, requestCount);
    }

    [AvaloniaFact]
    public void InvalidAnchorRequestedOverrideFallsBackToRatioSelection()
    {
        using var host = new AnchorHost(0, contentHeight: 1200, (60, 40), (250, 40));
        host.View.Offset = new Vector(0, 50);
        _ = host.Render();
        var unregisteredElement = new Border();
        ScrollingAnchorRequestedEventArgs? capturedArgs = null;
        host.View.AnchorRequested += (_, args) =>
        {
            capturedArgs = args;
            args.AnchorElement = unregisteredElement;
        };

        host.View.VerticalAnchorRatio = 0.25;
        _ = host.Render();

        var request = Assert.IsType<ScrollingAnchorRequestedEventArgs>(capturedArgs);
        Assert.DoesNotContain(unregisteredElement, request.AnchorCandidates);
        Assert.Same(host.Candidates[0], host.View.CurrentAnchor);
    }

    [AvaloniaFact]
    public void AnchorRatioTracksElementSizeAndNaNDisablesTheAxis()
    {
        using var host = new AnchorHost(0.5, contentHeight: 1200, (150, 100));
        var anchor = host.Candidates[0];
        host.View.ScrollTo(new Vector(0, 50), isAnimated: false);
        _ = host.Render();
        Assert.Equal(50, host.View.Offset.Y, 3);

        anchor.Height = 200;
        host.Content.Height = 1300;
        _ = host.Render();

        Assert.Equal(100, host.View.Offset.Y, 3);

        host.View.VerticalAnchorRatio = double.NaN;
        _ = host.Render();
        anchor.Height = 300;
        host.Content.Height = 1400;
        _ = host.Render();

        Assert.Equal(100, host.View.Offset.Y, 3);
        Assert.True(double.IsNaN(host.View.VerticalAnchorRatio));
    }

    [AvaloniaFact]
    public void FarAnchorKeepsContentAtTheEndWhenExtentGrows()
    {
        using var host = new AnchorHost(1, contentHeight: 600);
        Assert.Equal(300, host.View.Offset.Y, 3);

        var anchorRequestCount = 0;
        host.View.AnchorRequested += (_, _) => anchorRequestCount++;
        var sources = new List<ScrollChangeSource>();
        host.View.ScrollChanged += (_, args) => sources.Add(args.ChangeSource);
        host.Content.Height = 800;
        _ = host.Render();

        Assert.Equal(500, host.View.Offset.Y, 3);
        Assert.Equal(0, anchorRequestCount);
        Assert.NotEmpty(sources);
        Assert.All(sources, source => Assert.Equal(ScrollChangeSource.Layout, source));
    }

    [Fact]
    public void AnchorRatiosAreCoercedIndependently()
    {
        var view = new ScrollView
        {
            HorizontalAnchorRatio = -1,
            VerticalAnchorRatio = 2
        };

        Assert.Equal(0, view.HorizontalAnchorRatio);
        Assert.Equal(1, view.VerticalAnchorRatio);

        view.HorizontalAnchorRatio = double.NaN;

        Assert.True(double.IsNaN(view.HorizontalAnchorRatio));
        Assert.Equal(1, view.VerticalAnchorRatio);
    }

    [AvaloniaFact]
    public void MeasureInfinityAxesAreIndependent()
    {
        var content = new ConstraintRecordingControl();
        var view = new ScrollView
        {
            IsHorizontalMeasureInfinite = true,
            IsVerticalMeasureInfinite = false,
            Content = content
        };
        var window = new Window
        {
            Width = 800,
            Height = 600,
            WindowDecorations = WindowDecorations.None,
            Content = view
        };
        try
        {
            window.Show();
            _ = window.CaptureRenderedFrame();

            Assert.True(double.IsPositiveInfinity(content.LastMeasureConstraint.Width));
            Assert.False(double.IsPositiveInfinity(content.LastMeasureConstraint.Height));

            view.IsHorizontalMeasureInfinite = false;
            view.IsVerticalMeasureInfinite = true;
            _ = window.CaptureRenderedFrame();

            Assert.False(double.IsPositiveInfinity(content.LastMeasureConstraint.Width));
            Assert.True(double.IsPositiveInfinity(content.LastMeasureConstraint.Height));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void ScrollChangedDistinguishesProgrammaticUserAndLayoutChanges()
    {
        using var host = new ScrollViewHost(
            new Size(700, 500),
            new Size(1400, 1000),
            view =>
            {
                view.Focusable = true;
                view.IsScrollInertiaEnabled = false;
            });
        var sources = new List<ScrollChangeSource>();
        host.View.ScrollChanged += (_, args) => sources.Add(args.ChangeSource);

        host.View.ScrollTo(new Vector(40, 60));
        _ = host.Render();
        Assert.True(
            sources.Count > 0,
            $"No ScrollChanged event. {host.GetDiagnostics(new Point(350, 250))}");
        Assert.Equal(ScrollChangeSource.Programmatic, sources[^1]);

        host.Window.MouseWheel(new Point(350, 250), new Vector(0, -1));
        _ = host.Render();
        Assert.Equal(ScrollChangeSource.User, sources[^1]);

        Assert.True(host.View.Focus());
        sources.Clear();
        host.Window.KeyPress(Key.PageDown, RawInputModifiers.None, PhysicalKey.PageDown, null);
        Assert.Contains(ScrollChangeSource.User, sources);

        sources.Clear();
        host.Window.Width = 760;
        _ = host.Render();
        Assert.Contains(ScrollChangeSource.Layout, sources);
    }

    [AvaloniaFact]
    public void ProgrammaticOffsetAssignmentInsideUserNotificationStaysProgrammatic()
    {
        using var host = new ScrollViewHost(
            new Size(700, 500),
            new Size(1400, 1000),
            view =>
            {
                view.Focusable = true;
                view.IsScrollInertiaEnabled = false;
            });
        Assert.True(host.View.Focus());
        var changes = new List<ScrollViewChangedEventArgs>();
        var assignedProgrammatically = false;
        host.View.ScrollChanged += (_, args) =>
        {
            changes.Add(args);
            if (assignedProgrammatically || args.ChangeSource is not ScrollChangeSource.User)
                return;

            assignedProgrammatically = true;
            host.View.Offset = host.View.Offset.WithX(host.View.Offset.X + 10);
        };

        host.Window.KeyPress(Key.PageDown, RawInputModifiers.None, PhysicalKey.PageDown, null);

        Assert.True(assignedProgrammatically);
        Assert.True(
            changes.Count >= 2,
            $"Expected a user change followed by a programmatic change, got {string.Join(", ", changes.Select(x => x.ChangeSource))}.");
        Assert.Equal(ScrollChangeSource.User, changes[0].ChangeSource);
        Assert.Equal(ScrollChangeSource.Programmatic, changes[1].ChangeSource);
        Assert.Equal(10, changes[1].OffsetDelta.X, 3);
    }

    [AvaloniaFact]
    public void DisabledBringIntoViewAnimationUsesImmediateMinimalScroll()
    {
        using var host = new BringIntoViewHost();
        var presenter = Assert.IsType<ScrollPresenter>(host.View.Presenter);
        presenter.IsBringIntoViewAnimationEnabled = false;
        var wasHandled = false;
        host.View.AddHandler(
            Control.RequestBringIntoViewEvent,
            (_, args) => wasHandled = args.Handled,
            RoutingStrategies.Bubble,
            handledEventsToo: true);

        host.Target.BringIntoView();
        _ = host.Render();

        Assert.True(wasHandled);
        AssertVectorEqual(new Vector(400, 380), host.View.Offset);
    }

    [AvaloniaFact]
    public void StandardBringIntoViewUsesAnimation()
    {
        using var host = new BringIntoViewHost();
        var presenter = Assert.IsType<ScrollPresenter>(host.View.Presenter);
        ScrollAnimationStartingEventArgs? animationArgs = null;
        presenter.ScrollAnimationStarting += (_, args) => animationArgs = args;

        host.Target.BringIntoView();

        Assert.NotNull(animationArgs);
        Assert.Equal(default, animationArgs.StartingPosition);
        AssertVectorEqual(new Vector(400, 380), animationArgs.EndPosition);
    }

    [AvaloniaFact]
    public void ExplicitlyImmediateBringIntoViewOverridesDefaultAnimation()
    {
        using var host = new BringIntoViewHost();
        var presenter = Assert.IsType<ScrollPresenter>(host.View.Presenter);
        var animationStarted = false;
        presenter.ScrollAnimationStarting += (_, _) => animationStarted = true;

        host.Target.BringIntoView(isAnimated: false);
        _ = host.Render();

        Assert.False(animationStarted);
        AssertVectorEqual(new Vector(400, 380), host.View.Offset);
    }

    [AvaloniaFact]
    public void AnimatedBringIntoViewExtensionUsesScrollPresenterAnimation()
    {
        using var host = new BringIntoViewHost();
        var presenter = Assert.IsType<ScrollPresenter>(host.View.Presenter);
        ScrollAnimationStartingEventArgs? animationArgs = null;
        presenter.ScrollAnimationStarting += (_, args) => animationArgs = args;

        host.Target.BringIntoView(isAnimated: true);
        Assert.NotNull(animationArgs);
        Assert.Equal(default, animationArgs.StartingPosition);
        AssertVectorEqual(new Vector(400, 380), animationArgs.EndPosition);

        for (var i = 0; i < 70; i++)
        {
            Thread.Sleep(10);
            _ = host.Render();
        }

        AssertVectorEqual(new Vector(400, 380), host.View.Offset);
    }

    [AvaloniaFact]
    public void ThemedScrollViewerAnimatedBringIntoViewUsesScrollPresenterAnimation()
    {
        using var host = new ScrollViewerBringIntoViewHost();
        var presenter = Assert.IsType<ScrollPresenter>(host.View.Presenter);
        ScrollAnimationStartingEventArgs? animationArgs = null;
        presenter.ScrollAnimationStarting += (_, args) => animationArgs = args;

        host.Target.BringIntoView(isAnimated: true);

        Assert.NotNull(animationArgs);
        Assert.Equal(default, animationArgs.StartingPosition);
        AssertVectorEqual(new Vector(400, 380), animationArgs.EndPosition);

        for (var i = 0; i < 70; i++)
        {
            Thread.Sleep(10);
            _ = host.Render();
        }

        AssertVectorEqual(new Vector(400, 380), host.View.Offset);
    }

    [AvaloniaFact]
    public void BringIntoViewUsesZoomedTargetCoordinates()
    {
        using var host = new BringIntoViewHost(view => view.IsZoomEnabled = true);

        host.View.ZoomTo(2, isAnimated: false);
        _ = host.Render();
        host.View.ScrollTo(default);
        _ = host.Render();

        host.Target.BringIntoView(isAnimated: false);
        _ = host.Render();

        AssertVectorEqual(new Vector(1500, 1260), host.View.Offset);
    }

    [AvaloniaFact]
    public void RightClickContextMenuSurvivesCustomRightDragBinding()
    {
        using var host = new ScrollViewHost(
            new Size(700, 500),
            new Size(1400, 1000),
            view =>
            {
                view.IsScrollInertiaEnabled = false;
                view.GestureBindings[new ScrollGesture(ScrollInputGesture.MouseRightDrag)] =
                    ScrollGestureAction.Pan;
            });
        var menu = new ContextMenu
        {
            ItemsSource = new[] { new MenuItem { Header = "Context menu" } }
        };
        host.Content.ContextMenu = menu;
        var center = new Point(350, 250);
        var pressedHandled = true;
        var movedHandled = false;
        host.View.AddHandler(
            InputElement.PointerPressedEvent,
            (_, args) => pressedHandled = args.Handled,
            RoutingStrategies.Bubble,
            handledEventsToo: true);
        host.View.AddHandler(
            InputElement.PointerMovedEvent,
            (_, args) => movedHandled = args.Handled,
            RoutingStrategies.Bubble,
            handledEventsToo: true);

        host.Window.MouseDown(center, MouseButton.Right);
        host.Window.MouseUp(center, MouseButton.Right);
        Assert.False(pressedHandled);

        host.Content.RaiseEvent(new ContextRequestedEventArgs());
        Assert.True(menu.IsOpen);
        menu.Close();
        _ = host.Render();

        host.Window.MouseDown(center, MouseButton.Right);
        host.Window.MouseMove(new Point(300, 210), RawInputModifiers.RightMouseButton);
        host.Window.MouseMove(new Point(250, 170), RawInputModifiers.RightMouseButton);
        host.Window.MouseUp(new Point(250, 170), MouseButton.Right);
        _ = host.Render();

        Assert.True(movedHandled);
        Assert.True(host.View.Offset.X > 0, $"MoveHandled={movedHandled}, {host.GetDiagnostics(center)}");
        Assert.True(host.View.Offset.Y > 0, host.GetDiagnostics(center));
    }

    [AvaloniaFact]
    public void ExactAltWheelBindingScrollsOnlyHorizontally()
    {
        using var host = new ScrollViewHost(
            new Size(700, 500),
            new Size(1400, 1000),
            view =>
            {
                view.IsScrollInertiaEnabled = false;
                view.GestureBindings = new ScrollGestureBindings
                {
                    [new ScrollGesture(ScrollInputGesture.MouseWheel, KeyModifiers.Alt)] =
                        ScrollGestureAction.HorizontalScroll
                };
            });
        var center = new Point(350, 250);
        var wheelEventCount = 0;
        var observedModifiers = KeyModifiers.None;
        var wheelHandled = false;
        host.Content.PointerWheelChanged += (_, args) =>
        {
            wheelEventCount++;
            observedModifiers = args.KeyModifiers;
        };
        host.View.AddHandler(
            InputElement.PointerWheelChangedEvent,
            (_, args) => wheelHandled = args.Handled,
            RoutingStrategies.Bubble,
            handledEventsToo: true);

        host.Window.MouseWheel(center, new Vector(0, -1));
        Assert.Equal(default, host.View.Offset);

        host.Window.MouseWheel(center, new Vector(0, -1), RawInputModifiers.Alt);
        _ = host.Render();

        Assert.True(
            host.View.Offset.X > 0,
            $"Alt wheel did not scroll. WheelEvents={wheelEventCount}, Modifiers={observedModifiers}, Handled={wheelHandled}, {host.GetDiagnostics(center)}");
        Assert.Equal(128, host.View.Offset.X, 3);
        Assert.Equal(0, host.View.Offset.Y, 3);
    }

    [AvaloniaFact]
    public void InputMultipliersControlImmediateWheelAndZoomDeltas()
    {
        using var host = new ScrollViewHost(
            new Size(700, 500),
            new Size(1400, 1000),
            view =>
            {
                view.IsZoomEnabled = true;
                view.IsScrollInertiaEnabled = false;
                view.ScrollInputMultiplier = 0.5;
                view.ZoomInputMultiplier = 0.5;
            });
        var center = new Point(350, 250);

        host.Window.MouseWheel(center, new Vector(0, -1));
        _ = host.Render();
        Assert.Equal(64, host.View.Offset.Y, 3);

        host.Window.MouseWheel(center, new Vector(0, 1), RawInputModifiers.Control);
        _ = host.Render();
        Assert.Equal(Math.Sqrt(1.2), host.View.ZoomFactor, 3);
    }

    [AvaloniaFact]
    public void MouseManipulationClampsAtBoundsWithoutOverscroll()
    {
        using var host = new ScrollViewHost(
            new Size(800, 600),
            new Size(200, 120),
            view =>
            {
                view.IsZoomEnabled = true;
                view.IsScrollInertiaEnabled = true;
                view.HorizontalScrollMode = ScrollMode.Enabled;
                view.VerticalScrollMode = ScrollMode.Enabled;
            });
        var center = new Point(400, 300);
        var initialBounds = FindRedBounds(host.Render());

        host.Window.MouseDown(center, MouseButton.Left);
        Thread.Sleep(60);
        host.Window.MouseMove(new Point(420, 300), RawInputModifiers.LeftMouseButton);
        Thread.Sleep(60);
        host.Window.MouseMove(new Point(700, 300), RawInputModifiers.LeftMouseButton);
        var overscrolledBounds = FindRedBounds(host.Render());

        Assert.Equal(initialBounds, overscrolledBounds);

        Thread.Sleep(80);
        host.Window.MouseUp(new Point(700, 300), MouseButton.Left);
        _ = host.Render();
        for (var i = 0; i < 120; i++)
        {
            Thread.Sleep(10);
            _ = host.Render();
        }

        var settledBounds = FindRedBounds(host.Render());
        Assert.Equal(initialBounds, settledBounds);
    }

    [AvaloniaFact]
    public void MouseWheelClampsAtBoundsWithoutOverscroll()
    {
        using var host = new ScrollViewHost(
            new Size(800, 600),
            new Size(200, 120),
            view =>
            {
                view.IsScrollInertiaEnabled = true;
                view.OverscrollElasticity = 1;
                view.HorizontalScrollMode = ScrollMode.Enabled;
                view.VerticalScrollMode = ScrollMode.Enabled;
            });
        var center = new Point(400, 300);
        var initialBounds = FindRedBounds(host.Render());

        host.Window.MouseWheel(center, new Vector(0, 1));
        Thread.Sleep(40);
        var wheelBounds = FindRedBounds(host.Render());

        Assert.Equal(initialBounds, wheelBounds);
    }

    [AvaloniaFact]
    public void TouchPinchStartsOnSecondContactAndPreservesMidpoint()
    {
        using var host = new ScrollViewHost(
            new Size(700, 500),
            new Size(1200, 900),
            view =>
            {
                view.IsZoomEnabled = true;
                view.IsScrollInertiaEnabled = false;
            });
        using var first = new TouchContact(host.Window, isPrimary: true);
        using var second = new TouchContact(host.Window, isPrimary: false);

        first.Press(new Point(300, 250));
        second.Press(new Point(400, 250));
        first.Move(new Point(250, 250));
        second.Move(new Point(450, 250));
        _ = host.Render();

        Assert.Equal(2, host.View.ZoomFactor, 2);
        Assert.Equal(350, host.View.Offset.X, 3);
    }

    [AvaloniaFact]
    public void TouchDragStartsWhenChildHandlesPointerPressed()
    {
        using var host = new ScrollViewHost(
            new Size(600, 300),
            new Size(600, 900),
            view =>
            {
                view.HorizontalScrollMode = ScrollMode.Disabled;
                view.VerticalScrollMode = ScrollMode.Enabled;
                view.IsScrollInertiaEnabled = false;
            });
        var button = new Button { Width = 600, Height = 900 };
        host.View.Content = button;
        _ = host.Render();
        using var touch = new TouchContact(host.Window, isPrimary: true);

        touch.Press(new Point(300, 200));
        touch.Move(new Point(300, 170));
        touch.Move(new Point(300, 80));
        _ = host.Render();

        Assert.True(
            host.View.Offset.Y > 0,
            $"Touch drag was intercepted by {host.Window.InputHitTest(new Point(300, 200))?.GetType().Name}.");
    }

    [AvaloniaFact]
    public void TouchTapOnHandledChildStillClicksWithoutScrolling()
    {
        using var host = new ScrollViewHost(
            new Size(600, 300),
            new Size(600, 900),
            view =>
            {
                view.HorizontalScrollMode = ScrollMode.Disabled;
                view.VerticalScrollMode = ScrollMode.Enabled;
                view.IsScrollInertiaEnabled = false;
            });
        var clickCount = 0;
        var button = new Button { Width = 600, Height = 900 };
        button.Click += (_, _) => clickCount++;
        host.View.Content = button;
        _ = host.Render();
        using var touch = new TouchContact(host.Window, isPrimary: true);

        touch.Press(new Point(300, 200));
        touch.Release(new Point(300, 200));
        _ = host.Render();

        Assert.Equal(1, clickCount);
        Assert.Equal(0, host.View.Offset.Y, 3);
    }

    [AvaloniaFact]
    public void TouchDragStartsOnExpanderHeader()
    {
        using var host = new ScrollViewHost(
            new Size(600, 300),
            new Size(600, 900),
            view =>
            {
                view.HorizontalScrollMode = ScrollMode.Disabled;
                view.VerticalScrollMode = ScrollMode.Enabled;
                view.IsScrollInertiaEnabled = false;
            });
        var expander = new Expander
        {
            Width = 600,
            Height = 900,
            Header = "Settings",
            IsExpanded = true,
            Content = new Border { Height = 800 }
        };
        host.View.Content = expander;
        _ = host.Render();
        using var touch = new TouchContact(host.Window, isPrimary: true);

        touch.Press(new Point(300, 20));
        touch.Move(new Point(300, -10));
        touch.Move(new Point(300, -100));
        _ = host.Render();

        Assert.True(host.View.Offset.Y > 0);
    }

    [AvaloniaFact]
    public void TouchManipulationStaysWithScrollableDescendantBeforeBoundary()
    {
        var innerView = new ScrollView
        {
            Width = 600,
            Height = 300,
            HorizontalScrollMode = ScrollMode.Disabled,
            VerticalScrollMode = ScrollMode.Enabled,
            HorizontalScrollBarVisibility = ScrollBarVisibilityMode.Hidden,
            VerticalScrollBarVisibility = ScrollBarVisibilityMode.Hidden,
            IsScrollInertiaEnabled = false,
            Content = new Button { Width = 600, Height = 900 }
        };
        var outerContent = new StackPanel { Width = 600 };
        outerContent.Children.Add(innerView);
        outerContent.Children.Add(new Border { Height = 500 });
        var outerView = new ScrollView
        {
            Width = 600,
            Height = 500,
            HorizontalScrollMode = ScrollMode.Disabled,
            VerticalScrollMode = ScrollMode.Enabled,
            HorizontalScrollBarVisibility = ScrollBarVisibilityMode.Hidden,
            VerticalScrollBarVisibility = ScrollBarVisibilityMode.Hidden,
            IsScrollInertiaEnabled = false,
            Content = outerContent
        };
        var window = new Window
        {
            Width = 600,
            Height = 500,
            WindowDecorations = WindowDecorations.None,
            Content = outerView
        };

        try
        {
            window.Show();
            for (var i = 0; i < 4; i++)
                _ = window.CaptureRenderedFrame();
            using var touch = new TouchContact(window, isPrimary: true);

            touch.Press(new Point(300, 200));
            touch.Move(new Point(300, 170));
            touch.Move(new Point(300, 80));
            _ = window.CaptureRenderedFrame();

            Assert.True(
                innerView.Offset.Y > 0 && Math.Abs(outerView.Offset.Y) < 0.001,
                $"Expected the inner view to consume the touch. Inner={innerView.Offset}, Outer={outerView.Offset}, "
                + $"Hit={window.InputHitTest(new Point(300, 200))?.GetType().Name}.");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void TouchPinchStaysWithZoomableDescendant()
    {
        var innerView = new ScrollView
        {
            Width = 600,
            Height = 300,
            IsZoomEnabled = true,
            IsScrollInertiaEnabled = false,
            HorizontalScrollBarVisibility = ScrollBarVisibilityMode.Hidden,
            VerticalScrollBarVisibility = ScrollBarVisibilityMode.Hidden,
            Content = new Border { Width = 1200, Height = 900 }
        };
        var outerContent = new StackPanel { Width = 600 };
        outerContent.Children.Add(innerView);
        outerContent.Children.Add(new Border { Height = 500 });
        var outerView = new ScrollView
        {
            Width = 600,
            Height = 500,
            IsZoomEnabled = true,
            IsScrollInertiaEnabled = false,
            HorizontalScrollBarVisibility = ScrollBarVisibilityMode.Hidden,
            VerticalScrollBarVisibility = ScrollBarVisibilityMode.Hidden,
            Content = outerContent
        };
        var window = new Window
        {
            Width = 600,
            Height = 500,
            WindowDecorations = WindowDecorations.None,
            Content = outerView
        };

        try
        {
            window.Show();
            for (var i = 0; i < 4; i++)
                _ = window.CaptureRenderedFrame();
            using var first = new TouchContact(window, isPrimary: true);
            using var second = new TouchContact(window, isPrimary: false);

            first.Press(new Point(250, 150));
            second.Press(new Point(350, 150));
            first.Move(new Point(200, 150));
            second.Move(new Point(400, 150));
            _ = window.CaptureRenderedFrame();

            Assert.True(innerView.ZoomFactor > 1);
            Assert.Equal(1, outerView.ZoomFactor, 3);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void TouchManipulationTransfersToAncestorAtBoundary()
    {
        var innerContent = new Border
        {
            Width = 600,
            Height = 900,
            Background = Brushes.Red
        };
        var innerView = new ScrollView
        {
            Width = 600,
            Height = 300,
            HorizontalScrollMode = ScrollMode.Disabled,
            VerticalScrollMode = ScrollMode.Enabled,
            HorizontalScrollBarVisibility = ScrollBarVisibilityMode.Hidden,
            VerticalScrollBarVisibility = ScrollBarVisibilityMode.Hidden,
            IsScrollInertiaEnabled = false,
            Content = innerContent
        };
        var outerContent = new StackPanel { Width = 600 };
        outerContent.Children.Add(new Border { Height = 50 });
        outerContent.Children.Add(innerView);
        outerContent.Children.Add(new Border { Height = 500 });
        var outerView = new ScrollView
        {
            Width = 600,
            Height = 500,
            HorizontalScrollMode = ScrollMode.Disabled,
            VerticalScrollMode = ScrollMode.Enabled,
            HorizontalScrollBarVisibility = ScrollBarVisibilityMode.Hidden,
            VerticalScrollBarVisibility = ScrollBarVisibilityMode.Hidden,
            IsScrollInertiaEnabled = false,
            Content = outerContent
        };
        var window = new Window
        {
            Width = 600,
            Height = 500,
            WindowDecorations = WindowDecorations.None,
            Content = outerView
        };
        try
        {
            window.Show();
            for (var i = 0; i < 4; i++)
                _ = window.CaptureRenderedFrame();

            var innerMaximum = innerView.ScrollBarMaximum.Y;
            innerView.ScrollTo(new Vector(0, innerMaximum), isAnimated: false);
            for (var i = 0; i < 8 && Math.Abs(innerView.Offset.Y - innerMaximum) > 0.05; i++)
                _ = window.CaptureRenderedFrame();

            Assert.InRange(Math.Abs(innerView.Offset.Y - innerMaximum), 0, 0.05);

            using var touch = new TouchContact(window, isPrimary: true);
            touch.Press(new Point(300, 200));
            touch.Move(new Point(300, 180));
            touch.Move(new Point(300, 80));
            _ = window.CaptureRenderedFrame();

            Assert.True(
                outerView.Offset.Y > 0,
                $"Touch chaining did not reach the ancestor. Inner={innerView.Offset}, Outer={outerView.Offset}.");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void TouchManipulationAllowsElasticOverscroll()
    {
        using var host = new ScrollViewHost(
            new Size(800, 600),
            new Size(200, 120),
            view =>
            {
                view.HorizontalScrollMode = ScrollMode.Enabled;
                view.VerticalScrollMode = ScrollMode.Enabled;
                view.IsScrollInertiaEnabled = false;
                view.OverscrollElasticity = 1;
            });
        var center = new Point(400, 300);
        var initialBounds = FindRedBounds(host.Render());
        using var touch = new TouchContact(host.Window, isPrimary: true);

        touch.Press(center);
        touch.Move(new Point(420, 300));
        touch.Move(new Point(700, 300));
        var overscrolledBounds = FindRedBounds(host.Render());

        Assert.True(
            initialBounds != overscrolledBounds,
            $"Touch did not overscroll. Initial={initialBounds}, Current={overscrolledBounds}, {host.GetDiagnostics(center)}");

        touch.Release(new Point(700, 300));
        for (var i = 0; i < 120; i++)
        {
            Thread.Sleep(10);
            _ = host.Render();
        }

        Assert.Equal(initialBounds, FindRedBounds(host.Render()));
    }

    [AvaloniaFact]
    public void PausedInBoundsManipulationCompletesWithoutInertia()
    {
        using var host = new ScrollViewHost(
            new Size(800, 600),
            new Size(1200, 900),
            view =>
            {
                view.IsScrollInertiaEnabled = true;
                view.GestureBindings = CreateLeftMousePanBindings();
            });
        var center = new Point(400, 300);

        host.Window.MouseDown(center, MouseButton.Left);
        Thread.Sleep(60);
        host.Window.MouseMove(new Point(380, 300), RawInputModifiers.LeftMouseButton);
        Thread.Sleep(60);
        host.Window.MouseMove(new Point(300, 300), RawInputModifiers.LeftMouseButton);
        _ = host.Render();
        var offsetBeforeRelease = host.View.Offset;
        Assert.InRange(offsetBeforeRelease.X, 1, host.View.Extent.Width - host.View.Viewport.Width);
        Assert.InRange(offsetBeforeRelease.Y, 0, host.View.Extent.Height - host.View.Viewport.Height);

        Thread.Sleep(80);
        host.Window.MouseUp(new Point(300, 300), MouseButton.Left);
        for (var i = 0; i < 20; i++)
        {
            Thread.Sleep(10);
            _ = host.Render();
        }

        Assert.Equal(offsetBeforeRelease.X, host.View.Offset.X, 3);
        Assert.Equal(offsetBeforeRelease.Y, host.View.Offset.Y, 3);
    }

    [AvaloniaFact]
    public void WheelDuringInteractionContinuesWithInertia()
    {
        using var host = new ScrollViewHost(
            new Size(700, 500),
            new Size(1400, 1000),
            view => view.IsScrollInertiaEnabled = true);
        var center = new Point(350, 250);
        var moved = new Point(350, 200);

        host.Window.MouseDown(center, MouseButton.Left);
        host.Window.MouseMove(moved, RawInputModifiers.LeftMouseButton);
        _ = host.Render();
        var offsetBeforeWheel = host.View.Offset.Y;

        host.Window.MouseWheel(moved, new Vector(0, -1), RawInputModifiers.LeftMouseButton);
        Thread.Sleep(25);
        _ = host.Render();
        var offsetAfterFirstFrame = host.View.Offset.Y;

        Thread.Sleep(40);
        _ = host.Render();
        var offsetAfterSecondFrame = host.View.Offset.Y;
        host.Window.MouseUp(moved, MouseButton.Left);

        Assert.True(offsetAfterFirstFrame > offsetBeforeWheel);
        Assert.True(offsetAfterSecondFrame > offsetAfterFirstFrame);
    }

    [AvaloniaFact]
    public void TranslationAndZoomInertiaContinueTogether()
    {
        using var host = new ScrollViewHost(
            new Size(700, 500),
            new Size(1400, 1000),
            view =>
            {
                view.IsZoomEnabled = true;
                view.IsScrollInertiaEnabled = true;
            });
        var center = new Point(350, 250);
        ScrollingScrollStartingEventArgs? scrollStarting = null;
        ScrollingZoomStartingEventArgs? zoomStarting = null;
        ScrollingScrollCompletedEventArgs? scrollCompleted = null;
        ScrollingZoomCompletedEventArgs? zoomCompleted = null;
        var states = new List<ScrollingInteractionState>();
        host.View.ScrollStarting += (_, args) => scrollStarting ??= args;
        host.View.ZoomStarting += (_, args) => zoomStarting ??= args;
        host.View.ScrollCompleted += (_, args) => scrollCompleted = args;
        host.View.ZoomCompleted += (_, args) => zoomCompleted = args;
        host.View.StateChanged += (_, _) => states.Add(host.View.State);

        host.Window.MouseWheel(center, new Vector(0, 1), RawInputModifiers.Control);
        Thread.Sleep(25);
        _ = host.Render();
        var zoomAfterScaleImpulse = host.View.ZoomFactor;
        var offsetAfterScaleImpulse = host.View.Offset.Y;
        Assert.Equal(ScrollingInteractionState.Inertia, host.View.State);
        Assert.NotNull(zoomStarting);

        host.Window.MouseWheel(center, new Vector(0, -1));
        Thread.Sleep(40);
        _ = host.Render();

        Assert.True(host.View.ZoomFactor > zoomAfterScaleImpulse);
        Assert.True(host.View.Offset.Y > offsetAfterScaleImpulse);
        Assert.NotNull(scrollStarting);
        Assert.NotEqual(scrollStarting.CorrelationId, zoomStarting.CorrelationId);

        for (var i = 0; i < 120 && host.View.State is not ScrollingInteractionState.Idle; i++)
        {
            Thread.Sleep(10);
            _ = host.Render();
        }

        Assert.Equal(ScrollingInteractionState.Idle, host.View.State);
        Assert.Equal(scrollStarting.CorrelationId, scrollCompleted?.CorrelationId);
        Assert.Equal(ScrollingOperationResult.Completed, scrollCompleted?.Result);
        Assert.Equal(zoomStarting.CorrelationId, zoomCompleted?.CorrelationId);
        Assert.Equal(ScrollingOperationResult.Completed, zoomCompleted?.Result);
        Assert.Contains(ScrollingInteractionState.Inertia, states);
        Assert.Contains(ScrollingInteractionState.Idle, states);
    }

    private static PixelRect FindRedBounds(WriteableBitmap bitmap)
    {
        var size = bitmap.PixelSize;
        var stride = size.Width * 4;
        var byteCount = stride * size.Height;
        var address = Marshal.AllocHGlobal(byteCount);
        try
        {
            bitmap.CopyPixels(new PixelRect(size), address, byteCount, stride);
            var pixels = new byte[byteCount];
            Marshal.Copy(address, pixels, 0, byteCount);
            var minimumX = size.Width;
            var minimumY = size.Height;
            var maximumX = -1;
            var maximumY = -1;

            for (var y = 0; y < size.Height; y++)
            {
                for (var x = 0; x < size.Width; x++)
                {
                    var index = (y * stride) + (x * 4);
                    var firstChannel = pixels[index];
                    var green = pixels[index + 1];
                    var thirdChannel = pixels[index + 2];
                    var isRed = firstChannel > 200 && green < 60 && thirdChannel < 60
                                || thirdChannel > 200 && green < 60 && firstChannel < 60;
                    if (!isRed)
                        continue;

                    minimumX = Math.Min(minimumX, x);
                    minimumY = Math.Min(minimumY, y);
                    maximumX = Math.Max(maximumX, x);
                    maximumY = Math.Max(maximumY, y);
                }
            }

            Assert.True(
                maximumX >= minimumX && maximumY >= minimumY,
                $"The ScrollView content was not rendered. Frame={size}, Format={bitmap.Format}.");
            return new PixelRect(
                minimumX,
                minimumY,
                maximumX - minimumX + 1,
                maximumY - minimumY + 1);
        }
        finally
        {
            Marshal.FreeHGlobal(address);
        }
    }

    private static void AssertCentered(PixelRect bounds, PixelSize frameSize)
    {
        Assert.InRange(Math.Abs(bounds.Center.X - (frameSize.Width / 2.0)), 0, 2);
        Assert.InRange(Math.Abs(bounds.Center.Y - (frameSize.Height / 2.0)), 0, 2);
    }

    private static void AssertVectorEqual(Vector expected, Vector actual)
    {
        Assert.Equal(expected.X, actual.X, 3);
        Assert.Equal(expected.Y, actual.Y, 3);
    }

    private static ScrollGestureBindings CreateLeftMousePanBindings()
    {
        var bindings = ScrollGestureBindings.CreateDefault();
        bindings[new ScrollGesture(ScrollInputGesture.MouseLeftDrag)] = ScrollGestureAction.Pan;
        return bindings;
    }

    private sealed class ScrollViewHost : IDisposable
    {
        public ScrollViewHost(
            Size windowSize,
            Size contentSize,
            Action<ScrollView>? configure = null,
            bool renderInitially = true)
        {
            Content = new Border
            {
                Width = contentSize.Width,
                Height = contentSize.Height,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Background = Brushes.Red
            };
            View = new ScrollView
            {
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
                HorizontalScrollBarVisibility = ScrollBarVisibilityMode.Hidden,
                VerticalScrollBarVisibility = ScrollBarVisibilityMode.Hidden,
                Content = Content
            };
            configure?.Invoke(View);
            Window = new Window
            {
                Width = windowSize.Width,
                Height = windowSize.Height,
                WindowDecorations = WindowDecorations.None,
                Background = Brushes.Black,
                Content = View
            };
            Window.Show();
            if (renderInitially)
                _ = Render();
        }

        public Border Content { get; }

        public ScrollView View { get; }

        public Window Window { get; }

        public WriteableBitmap Render()
        {
            WriteableBitmap? frame = null;
            for (var i = 0; i < 4; i++)
                frame = Window.CaptureRenderedFrame();

            return frame
                   ?? throw new InvalidOperationException("The headless renderer did not produce a frame.");
        }

        public string GetDiagnostics(Point point)
        {
            var presenter = View.Presenter as ScrollPresenter;
            var hit = Window.InputHitTest(point);
            var ancestors = hit is Visual visual
                ? string.Join(">", visual.GetVisualAncestors().Select(value => value.GetType().Name))
                : string.Empty;
            return $"View={View.Bounds}, Viewport={View.Viewport}, Extent={View.Extent}, Content={Content.Bounds}, "
                   + $"PresenterLoaded={presenter?.IsLoaded}, ContentAttached={Content.IsAttachedToVisualTree()}, "
                    + $"Hit={hit?.GetType().Name}, Ancestors={ancestors}.";
        }

        public ScrollBar GetScrollBar(Orientation orientation) =>
            View.GetVisualDescendants().OfType<ScrollBar>().Single(value => value.Orientation == orientation);

        public void Dispose() => Window.Close();
    }

    private sealed class BringIntoViewHost : IDisposable
    {
        public BringIntoViewHost(Action<ScrollView>? configure = null)
        {
            Target = new Border
            {
                Width = 100,
                Height = 80,
                Background = Brushes.Red
            };
            var content = new Canvas
            {
                Width = 1600,
                Height = 1200
            };
            content.Children.Add(Target);
            Canvas.SetLeft(Target, 1000);
            Canvas.SetTop(Target, 800);
            View = new ScrollView
            {
                Width = 700,
                Height = 500,
                HorizontalContentAlignment = HorizontalAlignment.Left,
                VerticalContentAlignment = VerticalAlignment.Top,
                HorizontalScrollBarVisibility = ScrollBarVisibilityMode.Hidden,
                VerticalScrollBarVisibility = ScrollBarVisibilityMode.Hidden,
                Content = content
            };
            configure?.Invoke(View);
            Window = new Window
            {
                Width = 700,
                Height = 500,
                WindowDecorations = WindowDecorations.None,
                Content = View
            };
            Window.Show();
            _ = Render();
        }

        public Border Target { get; }

        public ScrollView View { get; }

        public Window Window { get; }

        public WriteableBitmap Render()
        {
            WriteableBitmap? frame = null;
            for (var i = 0; i < 4; i++)
                frame = Window.CaptureRenderedFrame();

            return frame
                   ?? throw new InvalidOperationException("The headless renderer did not produce a frame.");
        }

        public void Dispose() => Window.Close();
    }

    private sealed class ScrollViewerBringIntoViewHost : IDisposable
    {
        public ScrollViewerBringIntoViewHost()
        {
            Target = new Border
            {
                Width = 100,
                Height = 80,
                Background = Brushes.Red
            };
            var content = new Canvas
            {
                Width = 1600,
                Height = 1200
            };
            content.Children.Add(Target);
            Canvas.SetLeft(Target, 1000);
            Canvas.SetTop(Target, 800);
            View = new ScrollViewer
            {
                Width = 700,
                Height = 500,
                HorizontalContentAlignment = HorizontalAlignment.Left,
                VerticalContentAlignment = VerticalAlignment.Top,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Hidden,
                VerticalScrollBarVisibility = ScrollBarVisibility.Hidden,
                Content = content
            };
            Window = new Window
            {
                Width = 700,
                Height = 500,
                WindowDecorations = WindowDecorations.None,
                Content = View
            };
            Window.Show();
            _ = Render();
        }

        public Border Target { get; }

        public ScrollViewer View { get; }

        public Window Window { get; }

        public WriteableBitmap Render()
        {
            WriteableBitmap? frame = null;
            for (var i = 0; i < 4; i++)
                frame = Window.CaptureRenderedFrame();

            return frame
                   ?? throw new InvalidOperationException("The headless renderer did not produce a frame.");
        }

        public void Dispose() => Window.Close();
    }

    private sealed class AnchorHost : IDisposable
    {
        public AnchorHost(
            double verticalAnchorRatio,
            double contentHeight,
            params (double Top, double Height)[] candidates)
        {
            Content = new Canvas
            {
                Width = 400,
                Height = contentHeight,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top
            };
            Candidates = candidates.Select(candidate =>
            {
                var control = new Border
                {
                    Width = 400,
                    Height = candidate.Height,
                    Background = Brushes.Red
                };
                Canvas.SetTop(control, candidate.Top);
                Content.Children.Add(control);
                return control;
            }).ToArray();
            View = new ScrollView
            {
                HorizontalContentAlignment = HorizontalAlignment.Left,
                VerticalContentAlignment = VerticalAlignment.Top,
                HorizontalAnchorRatio = double.NaN,
                VerticalAnchorRatio = verticalAnchorRatio,
                HorizontalScrollBarVisibility = ScrollBarVisibilityMode.Hidden,
                VerticalScrollBarVisibility = ScrollBarVisibilityMode.Hidden,
                Content = Content
            };
            Window = new Window
            {
                Width = 400,
                Height = 300,
                WindowDecorations = WindowDecorations.None,
                Content = View
            };
            Window.Show();
            _ = Render();

            foreach (var candidate in Candidates)
                View.RegisterAnchorCandidate(candidate);

            _ = Render();
        }

        public Canvas Content { get; }

        public IReadOnlyList<Border> Candidates { get; }

        public ScrollView View { get; }

        public Window Window { get; }

        public WriteableBitmap Render()
        {
            WriteableBitmap? frame = null;
            for (var i = 0; i < 4; i++)
                frame = Window.CaptureRenderedFrame();

            return frame
                   ?? throw new InvalidOperationException("The headless renderer did not produce a frame.");
        }

        public void Dispose() => Window.Close();
    }

    private sealed class TouchContact : IDisposable
    {
        private readonly Window _window;
        private readonly Pointer _pointer;
        private ulong _timestamp;

        public TouchContact(Window window, bool isPrimary)
        {
            _window = window;
            _pointer = new Pointer(Pointer.GetNextFreeId(), PointerType.Touch, isPrimary);
        }

        public void Press(Point position)
        {
            var target = GetTarget(position);
            _pointer.Capture(target);
            target.RaiseEvent(new PointerPressedEventArgs(
                target,
                _pointer,
                _window,
                position,
                _timestamp++,
                new PointerPointProperties(RawInputModifiers.LeftMouseButton, PointerUpdateKind.LeftButtonPressed),
                KeyModifiers.None));
        }

        public void Move(Point position)
        {
            var target = GetTarget(position);
            target.RaiseEvent(new PointerEventArgs(
                InputElement.PointerMovedEvent,
                target,
                _pointer,
                _window,
                position,
                _timestamp++,
                new PointerPointProperties(RawInputModifiers.LeftMouseButton, PointerUpdateKind.Other),
                KeyModifiers.None));
        }

        public void Release(Point position)
        {
            var target = GetTarget(position);
            target.RaiseEvent(new PointerReleasedEventArgs(
                target,
                _pointer,
                _window,
                position,
                _timestamp++,
                new PointerPointProperties(RawInputModifiers.None, PointerUpdateKind.LeftButtonReleased),
                KeyModifiers.None,
                MouseButton.Left));
            _pointer.Capture(null);
        }

        public void Dispose() => _pointer.Capture(null);

        private IInputElement GetTarget(Point position) =>
            _pointer.Captured ?? _window.InputHitTest(position)
            ?? throw new InvalidOperationException($"No touch target at {position}.");
    }

    private sealed class ConstraintRecordingControl : Control
    {
        public Size LastMeasureConstraint { get; private set; }

        protected override Size MeasureOverride(Size availableSize)
        {
            LastMeasureConstraint = availableSize;
            return new Size(240, 160);
        }
    }
}

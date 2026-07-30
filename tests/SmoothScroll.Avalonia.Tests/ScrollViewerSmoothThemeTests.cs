using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using SmoothScroll.Avalonia.Controls;
using SmoothScroll.Avalonia.Interaction;

namespace SmoothScroll.Avalonia.Tests;

public sealed class ScrollViewerSmoothThemeTests
{
    [AvaloniaFact]
    public void OffsetIsPreservedWhenReattachedToVisualTree()
    {
        var marker = new Border
        {
            Width = 40,
            Height = 40,
            Background = Brushes.Red
        };
        Canvas.SetLeft(marker, 300);
        Canvas.SetTop(marker, 240);
        var content = new Canvas
        {
            Width = 1000,
            Height = 900,
            Children = { marker }
        };
        var view = new ScrollViewer
        {
            Width = 500,
            Height = 300,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Hidden,
            VerticalScrollBarVisibility = ScrollBarVisibility.Hidden,
            Content = content
        };
        var navigationHost = new ContentControl { Content = view };
        var window = new Window
        {
            Width = 500,
            Height = 300,
            WindowDecorations = WindowDecorations.None,
            Content = navigationHost
        };

        try
        {
            window.Show();
            Render(window);
            var presenter = Assert.IsType<ScrollViewerPresenter>(view.Presenter);
            var expectedOffset = new Vector(240, 180);
            presenter.ScrollTo(expectedOffset, isAnimated: false);
            Render(window);

            Assert.Equal(expectedOffset, view.Offset);
            Assert.Equal(expectedOffset, presenter.Offset);
            Assert.Same(marker, window.InputHitTest(new Point(70, 70)));

            navigationHost.Content = new Border();
            Render(window);
            navigationHost.Content = view;
            Render(window);

            Assert.Equal(expectedOffset, view.Offset);
            Assert.Equal(expectedOffset, presenter.Offset);
            Assert.Same(marker, window.InputHitTest(new Point(70, 70)));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void ScrollBarVisibilitySelectorsUpdatePresenterSemantics()
    {
        var view = new ScrollViewer
        {
            Width = 800,
            Height = 600,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Hidden,
            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Content = new Border { Width = 1200, Height = 900 }
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
            Render(window);
            var presenter = Assert.IsType<ScrollViewerPresenter>(view.Presenter);

            Assert.True(presenter.IsHorizontalMeasureInfinite);
            Assert.Equal(ScrollMode.Enabled, presenter.HorizontalScrollMode);
            Assert.False(presenter.IsVerticalMeasureInfinite);
            Assert.Equal(ScrollMode.Disabled, presenter.VerticalScrollMode);

            view.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
            view.VerticalScrollBarVisibility = ScrollBarVisibility.Hidden;
            Render(window);

            Assert.False(presenter.IsHorizontalMeasureInfinite);
            Assert.Equal(ScrollMode.Disabled, presenter.HorizontalScrollMode);
            Assert.True(presenter.IsVerticalMeasureInfinite);
            Assert.Equal(ScrollMode.Enabled, presenter.VerticalScrollMode);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void HorizontalSnapPointContentAnimatesProgrammaticAndVerticalWheelScrolling()
    {
        using var host = new HorizontalSnapPointHost();
        var targetOffset = host.GetCardOffset(1);

        Assert.True(host.Presenter.IsHorizontalMeasureInfinite);
        Assert.False(host.Presenter.IsVerticalMeasureInfinite);
        Assert.Equal(ScrollMode.Enabled, host.Presenter.HorizontalScrollMode);
        Assert.Equal(ScrollMode.Disabled, host.Presenter.VerticalScrollMode);
        Assert.True(host.Presenter.CanHorizontallyScroll);
        Assert.True(host.Presenter.Extent.Width > host.Presenter.Viewport.Width);

        host.Presenter.ScrollTo(new Vector(targetOffset, 0), isAnimated: true);
        _ = host.CaptureOffsets(70);
        Assert.Equal(targetOffset, host.View.Offset.X, 3);

        host.Presenter.ScrollTo(default, isAnimated: false);
        Render(host.Window);
        host.Wheel(-1);
        var offsets = host.CaptureOffsets(100);

        Assert.Contains(offsets, value => value > 1 && value < targetOffset - 1);
        Assert.Equal(targetOffset, host.View.Offset.X, 3);
        Assert.Equal(0, host.View.Offset.Y, 3);
    }

    [AvaloniaFact]
    public void RepeatedWheelInputAdvancesAndReversesWhileSnapAnimationIsActive()
    {
        using var host = new HorizontalSnapPointHost();

        host.Wheel(-1);
        _ = host.CaptureOffsets(8);
        var offsetAfterFirstImpulse = host.View.Offset.X;
        host.Wheel(-1);
        _ = host.CaptureOffsets(8);
        var offsetAfterSecondImpulse = host.View.Offset.X;
        host.Wheel(-1);
        var forwardOffsets = host.CaptureOffsets(120);
        var forwardTarget = host.GetCardOffset(3);

        Assert.True(offsetAfterFirstImpulse > 0);
        Assert.True(offsetAfterSecondImpulse > offsetAfterFirstImpulse);
        Assert.Contains(forwardOffsets, value => value > offsetAfterSecondImpulse && value < forwardTarget - 1);
        Assert.Equal(forwardTarget, host.View.Offset.X, 3);

        host.Wheel(1);
        var reverseOffsets = host.CaptureOffsets(120);
        var reverseTarget = host.GetCardOffset(2);

        Assert.Contains(reverseOffsets, value => value < forwardTarget - 1 && value > reverseTarget + 1);
        Assert.Equal(reverseTarget, host.View.Offset.X, 3);
    }

    [AvaloniaFact]
    public void AutoWheelPrefersVerticalAxisWhenBothAxesAreEnabled()
    {
        var view = new ScrollViewer
        {
            Width = 700,
            Height = 500,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Hidden,
            VerticalScrollBarVisibility = ScrollBarVisibility.Hidden,
            Content = new Border { Width = 1400, Height = 1000 }
        };
        var window = new Window
        {
            Width = 700,
            Height = 500,
            WindowDecorations = WindowDecorations.None,
            Content = view
        };

        try
        {
            window.Show();
            Render(window);
            window.MouseWheel(new Point(350, 250), new Vector(0, -1));
            for (var i = 0; i < 20; i++)
            {
                Thread.Sleep(10);
                Render(window);
            }

            Assert.Equal(0, view.Offset.X, 3);
            Assert.True(view.Offset.Y > 0);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void AutoWheelUsesHorizontalAxisWhenOnlyItHasScrollRange()
    {
        var view = new ScrollViewer
        {
            Width = 700,
            Height = 500,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Hidden,
            VerticalScrollBarVisibility = ScrollBarVisibility.Hidden,
            Content = new Border { Width = 1400, Height = 300 }
        };
        var window = new Window
        {
            Width = 700,
            Height = 500,
            WindowDecorations = WindowDecorations.None,
            Content = view
        };

        try
        {
            window.Show();
            Render(window);
            var presenter = Assert.IsType<ScrollViewerPresenter>(view.Presenter);
            Assert.Equal(ScrollMode.Enabled, presenter.HorizontalScrollMode);
            Assert.Equal(ScrollMode.Enabled, presenter.VerticalScrollMode);

            window.MouseWheel(new Point(350, 250), new Vector(0, -1));
            for (var i = 0; i < 20; i++)
            {
                Thread.Sleep(10);
                Render(window);
            }

            Assert.True(view.Offset.X > 0);
            Assert.Equal(0, view.Offset.Y, 3);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void AutoScrollViewerMeasuresHorizontalStackPanelWithInfiniteWidth()
    {
        var items = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
        for (var i = 0; i < 3; i++)
            items.Children.Add(new Border { Width = 240, Height = 80 });

        var view = new ScrollViewer
        {
            Width = 500,
            Height = 200,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Content = items
        };
        var window = new Window
        {
            Width = 500,
            Height = 200,
            WindowDecorations = WindowDecorations.None,
            Content = view
        };

        try
        {
            window.Show();
            Render(window);
            var presenter = Assert.IsType<ScrollViewerPresenter>(view.Presenter);

            Assert.True(presenter.IsHorizontalMeasureInfinite);
            Assert.True(items.DesiredSize.Width > presenter.Viewport.Width);
            Assert.True(presenter.Extent.Width > presenter.Viewport.Width);

            window.MouseWheel(new Point(250, 100), new Vector(0, -1));
            for (var i = 0; i < 20; i++)
            {
                Thread.Sleep(10);
                Render(window);
            }

            Assert.True(view.Offset.X > 0);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void UnderflowContentUsesScrollViewerAlignmentWithoutCreatingScrollRange()
    {
        var content = new Border { Width = 160, Height = 100 };
        var view = new ScrollViewer
        {
            Width = 800,
            Height = 600,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Hidden,
            VerticalScrollBarVisibility = ScrollBarVisibility.Hidden,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
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
            Render(window);
            var presenter = Assert.IsType<ScrollViewerPresenter>(view.Presenter);

            Assert.Equal(default, view.Offset);
            Assert.Equal(content.Width, presenter.Extent.Width, 3);
            Assert.Equal(content.Height, presenter.Extent.Height, 3);
            Assert.Equal((presenter.Viewport.Width - content.Width) / 2, content.Bounds.X, 3);
            Assert.Equal((presenter.Viewport.Height - content.Height) / 2, content.Bounds.Y, 3);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void PresenterOutsideTheThemeSelectorUsesScrollViewerFallbackBindings()
    {
        var nestedPresenter = new ScrollViewerPresenter
        {
            Content = new Border { Width = 1200, Height = 100 }
        };
        var view = new ScrollViewer
        {
            Width = 500,
            Height = 200,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Hidden,
            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Content = new Panel { Children = { nestedPresenter } }
        };
        var window = new Window
        {
            Width = 500,
            Height = 200,
            WindowDecorations = WindowDecorations.None,
            Content = view
        };

        try
        {
            window.Show();
            Render(window);

            Assert.True(nestedPresenter.IsHorizontalMeasureInfinite);
            Assert.False(nestedPresenter.IsVerticalMeasureInfinite);
            Assert.Equal(ScrollMode.Enabled, nestedPresenter.HorizontalScrollMode);
            Assert.Equal(ScrollMode.Disabled, nestedPresenter.VerticalScrollMode);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void LogicalScrollableContentUsesNativeStateDuringSmoothScrolling()
    {
        var content = new LogicalScrollableContent(new Size(900, 1600));
        var view = new ScrollViewer
        {
            Width = 500,
            Height = 300,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Hidden,
            VerticalScrollBarVisibility = ScrollBarVisibility.Hidden,
            Content = content
        };
        var window = new Window
        {
            Width = 500,
            Height = 300,
            WindowDecorations = WindowDecorations.None,
            Content = view
        };

        try
        {
            window.Show();
            Render(window);
            var presenter = Assert.IsType<ScrollViewerPresenter>(view.Presenter);

            Assert.True(double.IsFinite(content.LastMeasureConstraint.Width));
            Assert.True(double.IsFinite(content.LastMeasureConstraint.Height));
            Assert.Equal(content.Extent, presenter.Extent);
            Assert.Equal(content.Viewport, presenter.Viewport);
            Assert.True(content.CanHorizontallyScroll);
            Assert.True(content.CanVerticallyScroll);

            content.Offset = new Vector(0, 180);
            Render(window);
            Assert.Equal(content.Offset, view.Offset);

            content.Offset = default;
            Render(window);
            window.MouseWheel(new Point(250, 150), new Vector(0, -1));
            var offsets = new List<double>();
            for (var i = 0; i < 80; i++)
            {
                Thread.Sleep(10);
                Render(window);
                offsets.Add(content.Offset.Y);
                Assert.Equal(view.Offset.Y, content.Offset.Y, 3);
            }

            Assert.Contains(offsets, offset => offset > 1);
            Assert.Contains(offsets, offset => offset > 1 && offset < content.Offset.Y - 1);
        }
        finally
        {
            window.Close();
        }
    }

    private static void Render(Window window)
    {
        for (var i = 0; i < 4; i++)
            _ = window.CaptureRenderedFrame();
    }

    private sealed class HorizontalSnapPointHost : IDisposable
    {
        public HorizontalSnapPointHost()
        {
            Items = new StackPanel
            {
                Margin = new Thickness(24, 0),
                Orientation = Orientation.Horizontal,
                Spacing = 24,
                AreHorizontalSnapPointsRegular = true
            };
            for (var i = 0; i < 5; i++)
                Items.Children.Add(new Border { Width = 520, Height = 336 });

            View = new ScrollViewer
            {
                Width = 1200,
                Height = 600,
                VerticalContentAlignment = VerticalAlignment.Top,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Hidden,
                VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
                HorizontalSnapPointsType = SnapPointsType.Mandatory,
                HorizontalSnapPointsAlignment = SnapPointsAlignment.Center,
                Content = Items
            };
            Window = new Window
            {
                Width = 1200,
                Height = 600,
                WindowDecorations = WindowDecorations.None,
                Content = View
            };
            Window.Show();
            Render(Window);
            Presenter = Assert.IsType<ScrollViewerPresenter>(View.Presenter);
        }

        public StackPanel Items { get; }

        public ScrollViewerPresenter Presenter { get; }

        public ScrollViewer View { get; }

        public Window Window { get; }

        public IReadOnlyList<double> CaptureOffsets(int count)
        {
            var result = new List<double>(count);
            for (var i = 0; i < count; i++)
            {
                Thread.Sleep(10);
                Render(Window);
                result.Add(View.Offset.X);
            }

            return result;
        }

        public double GetCardOffset(int index) => Math.Clamp(
            Items.Children[index].Bounds.Center.X - (Presenter.Viewport.Width * 0.5),
            0,
            Presenter.Extent.Width - Presenter.Viewport.Width);

        public void Wheel(double delta) =>
            Window.MouseWheel(new Point(600, 300), new Vector(0, delta));

        public void Dispose() => Window.Close();
    }

    private sealed class LogicalScrollableContent(Size extent) : Control, ILogicalScrollable
    {
        private Vector _offset;
        private Size _viewport;

        public bool CanHorizontallyScroll { get; set; }

        public bool CanVerticallyScroll { get; set; }

        public bool IsLogicalScrollEnabled => true;

        public Size ScrollSize => new(10, 50);

        public Size PageScrollSize => new(100, 100);

        public Size Extent { get; } = extent;

        public Vector Offset
        {
            get => _offset;
            set
            {
                var maximum = new Vector(
                    Math.Max(Extent.Width - Viewport.Width, 0),
                    Math.Max(Extent.Height - Viewport.Height, 0));
                var coerced = new Vector(
                    Math.Clamp(value.X, 0, maximum.X),
                    Math.Clamp(value.Y, 0, maximum.Y));
                if (_offset == coerced)
                    return;

                _offset = coerced;
                RaiseScrollInvalidated(EventArgs.Empty);
            }
        }

        public Size Viewport => _viewport;

        public Size LastMeasureConstraint { get; private set; }

        public event EventHandler? ScrollInvalidated;

        public bool BringIntoView(Control target, Rect targetRect) => false;

        public Control? GetControlInDirection(NavigationDirection direction, Control? from) => null;

        public void RaiseScrollInvalidated(EventArgs e) => ScrollInvalidated?.Invoke(this, e);

        protected override Size MeasureOverride(Size availableSize)
        {
            LastMeasureConstraint = availableSize;
            var viewport = new Size(
                double.IsFinite(availableSize.Width) ? availableSize.Width : Extent.Width,
                double.IsFinite(availableSize.Height) ? availableSize.Height : Extent.Height);
            if (_viewport != viewport)
            {
                _viewport = viewport;
                RaiseScrollInvalidated(EventArgs.Empty);
            }

            return viewport;
        }
    }
}

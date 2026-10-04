using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Input.Raw;
using Avalonia.Rendering.Composition;
using Avalonia.Threading;
using SmoothScroll.Avalonia.Controls;
using SmoothScroll.Avalonia.Interaction;
using SmoothScroll.Avalonia.Interaction.Experimental;

namespace SmoothScroll.Avalonia.Tests;

public sealed class ExperimentalScrollIntegrationTests
{
    [AvaloniaTheory]
    [InlineData(true)]
    [InlineData(false)]
    public void TouchRelease_PreservesExperimentalInertia_WhileExternalCaptureLossCancels(bool normalRelease)
    {
        var viewer = new ScrollViewer
        {
            Content = new Border { Height = 8000 }, IsScrollInertiaEnabled = true,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Hidden,
        };
        var window = new Window { Width = 240, Height = 160, Content = viewer };
        var pointer = new Pointer(Pointer.GetNextFreeId(), PointerType.Touch, true);
        try
        {
            window.Show(); Render(window);
            var presenter = Assert.IsType<ScrollViewerPresenter>(viewer.Presenter);
            var participant = new Participant { Maximum = 8000 };
            using var registration = presenter.AttachExperimentalVerticalScroll(tracker =>
            {
                tracker.ConfigureExperimentalVerticalScroll(new Factory(participant));
                return new CallbackDisposable(() => tracker.ConfigureExperimentalVerticalScroll(null));
            });
            Render(window);
            var target = window.InputHitTest(new Point(40, 120))!;
            pointer.Capture(target);
            target.RaiseEvent(new PointerPressedEventArgs(target, pointer, window, new Point(40, 120), 100,
                new PointerPointProperties(RawInputModifiers.LeftMouseButton, PointerUpdateKind.LeftButtonPressed), KeyModifiers.None));
            foreach (var (time, y) in new[] { (116UL, 100d), (132UL, 70d), (148UL, 40d) })
            {
                target = pointer.Captured!;
                target.RaiseEvent(new PointerEventArgs(InputElement.PointerMovedEvent, target, pointer, window, new Point(40, y), time,
                    new PointerPointProperties(RawInputModifiers.LeftMouseButton, PointerUpdateKind.Other), KeyModifiers.None));
            }
            Render(window);
            var before = viewer.Offset.Y;
            Assert.True(before > 0);
            var cancellations = participant.CancelCount;
            if (normalRelease)
            {
                target = pointer.Captured!;
                target.RaiseEvent(new PointerReleasedEventArgs(target, pointer, window, new Point(40, 40), 164,
                    new PointerPointProperties(RawInputModifiers.None, PointerUpdateKind.LeftButtonReleased), KeyModifiers.None, MouseButton.Left));
                Render(window);
                Assert.True(viewer.Offset.Y > before);
                Assert.Equal(cancellations, participant.CancelCount);
            }
            else
            {
                pointer.Capture(window); Render(window);
                var stopped = viewer.Offset.Y;
                Render(window);
                Assert.Equal(stopped, viewer.Offset.Y);
                Assert.True(participant.CancelCount > cancellations);
            }
        }
        finally { pointer.Capture(null); window.Close(); }
    }

    [AvaloniaTheory]
    [InlineData(-1)]
    [InlineData(1)]
    public void NativeNestedWheel_QueriesAdditionalConsumptionAtBoundary_AndClearsQueryOnDetach(double wheelDelta)
    {
        var inner = new ScrollViewer
        {
            Height = 80, Content = new Border { Height = 400 }, IsScrollInertiaEnabled = false,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Hidden,
        };
        var outer = new ScrollViewer
        {
            IsScrollInertiaEnabled = false,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Hidden,
            Content = new StackPanel { Children = { new Border { Height = 200 }, inner, new Border { Height = 600 } } },
        };
        var window = new Window { Width = 240, Height = 160, Content = outer };
        try
        {
            window.Show(); Render(window);
            var presenter = Assert.IsType<ScrollViewerPresenter>(inner.Presenter);
            var participant = new Participant { Maximum = inner.ScrollBarMaximum.Y, ConsumeAll = true };
            var canConsume = true;
            var queries = new List<double>();
            InteractionTracker? attached = null;
            using var registration = presenter.AttachExperimentalVerticalScroll(tracker =>
            {
                attached = tracker;
                tracker.ConfigureExperimentalVerticalScroll(new Factory(participant), delta =>
                {
                    Dispatcher.UIThread.VerifyAccess();
                    queries.Add(delta);
                    return canConsume;
                });
                return new CallbackDisposable(() => tracker.ConfigureExperimentalVerticalScroll(null));
            });
            outer.Offset = new Vector(0, 200);
            inner.Offset = new Vector(0, wheelDelta < 0 ? participant.Maximum : 0);
            Render(window);

            window.MouseWheel(new Point(30, 30), new Vector(0, wheelDelta)); Render(window);
            Assert.Equal(200, outer.Offset.Y);
            Assert.Equal(-Math.Sign(wheelDelta), Math.Sign(Assert.Single(queries)));
            Assert.Equal(-Math.Sign(wheelDelta), Math.Sign(Assert.Single(participant.Results).PreConsumed));

            canConsume = false;
            window.MouseWheel(new Point(30, 30), new Vector(0, wheelDelta)); Render(window);
            Assert.Equal(-Math.Sign(wheelDelta), Math.Sign(outer.Offset.Y - 200));
            Assert.Single(participant.Results);

            // Reset while the inner control remains visible, then remove the participant.
            outer.Offset = new Vector(0, 200); Render(window);
            var queryCount = queries.Count;
            attached!.ConfigureExperimentalVerticalScroll(null);
            Render(window);
            window.MouseWheel(new Point(30, 30), new Vector(0, wheelDelta)); Render(window);
            Assert.Equal(-Math.Sign(wheelDelta), Math.Sign(outer.Offset.Y - 200));
            Assert.Equal(queryCount, queries.Count);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void OrderedUpdatesRetainParticipantAndDeliverSnapshotsWithoutStationaryBodyNotifications()
    {
        using var host = new Host();
        var delivered = new List<double>();
        var participant = new Participant { ConsumeAll = true };
        participant.Publish = value => host.Tracker.ExperimentalDispatchToUI(() =>
        {
            Dispatcher.UIThread.VerifyAccess();
            delivered.Add(value);
        });
        var factory = new Factory(participant);
        host.Tracker.ConfigureExperimentalVerticalScroll(factory);
        host.Tracker.ExperimentalApplyVerticalDelta(10);
        host.Tracker.ExperimentalRunOnParticipant(value => ((Participant)value).Snapshot = 20);
        host.Tracker.ExperimentalApplyVerticalDelta(15);
        host.Tracker.ConfigureContentBounds(new InteractionTrackerContentBounds(new Size(200, 200), new Size(200, 200), default));
        host.Render();

        Assert.Equal(1, factory.CreateCount);
        Assert.Equal(new[] { 10.0, 35 }, delivered);
        Assert.Equal(0, host.Tracker.Position.Y);
        Assert.Equal(2, participant.Results.Count);
        Assert.Equal(0, participant.CancelCount);
    }

    [AvaloniaFact]
    public void DynamicBoundsStayAuthoritativeAcrossStaleGeometryAndCorrectWithoutChargingMovement()
    {
        using var host = new Host();
        var participant = new Participant { Maximum = 200 };
        host.Tracker.ConfigureExperimentalVerticalScroll(new Factory(participant));
        host.Tracker.TryUpdatePosition(new Vector3D(0, 150, 0));
        host.Tracker.ConfigureContentBounds(new InteractionTrackerContentBounds(new Size(100, 200), new Size(100, 100), default));
        host.Render();
        Assert.Equal(150, host.Tracker.Position.Y);

        host.Tracker.ExperimentalRunOnParticipant(value => ((Participant)value).ShrinkAfterPre = true);
        host.Tracker.ExperimentalApplyVerticalDelta(30);
        host.Render();
        var result = Assert.Single(participant.Results);
        Assert.Equal(130, result.Position);
        Assert.Equal(0, result.SelfConsumed);
        Assert.Equal(30, result.Remaining);
        Assert.Equal(30, result.PreConsumed + result.SelfConsumed + result.PostConsumed + result.Remaining);

        host.Tracker.ExperimentalRunOnParticipant(value =>
        {
            var current = (Participant)value;
            current.Maximum = 200;
            current.ShrinkAfterPre = false;
            current.ShrinkAfterPost = true;
        });
        host.Tracker.TryUpdatePosition(new Vector3D(0, 90, 0));
        host.Tracker.ExperimentalApplyVerticalDelta(50);
        host.Render();
        result = participant.Results[1];
        Assert.Equal(100, result.Position);
        Assert.Equal(50, result.SelfConsumed);
        Assert.Equal(0, result.Remaining);
    }

    [AvaloniaFact]
    public void ExplicitReleaseStartsIdleClockAndAbsoluteInputStopsItWithoutConsumption()
    {
        using var host = new Host();
        var participant = new Participant { ContinueFrames = true };
        host.Tracker.ConfigureExperimentalVerticalScroll(new Factory(participant));
        host.Tracker.ExperimentalApplyVerticalDelta(10);
        host.Tracker.ExperimentalCompleteVerticalScroll();
        host.Render();
        Thread.Sleep(25);
        host.Render();
        Assert.Equal(1, participant.IdleCount);
        Assert.Equal(0, participant.Frames[0]);
        Assert.Contains(participant.Frames, value => value > 0);
        host.Tracker.TryUpdatePosition(new Vector3D(0, 20, 0));
        host.Render();
        var framesAfterAbsolute = participant.Frames.Count;
        Thread.Sleep(25);
        host.Render();
        Assert.Equal(framesAfterAbsolute, participant.Frames.Count);
        Assert.Equal(1, participant.IdleCount);
        Assert.Single(participant.Results);
        Assert.Equal(20, host.Tracker.Position.Y);
        Assert.Equal(1, participant.CancelCount);
    }

    [AvaloniaFact]
    public void IdleConfigurationRequestsFramesWithoutIdleAndRunningUpdatesPreserveElapsed()
    {
        using var host = new Host();
        var participant = new Participant();
        host.Tracker.ConfigureExperimentalVerticalScroll(new Factory(participant));
        host.Tracker.ExperimentalRunOnParticipant(value => ((Participant)value).ContinueFrames = true);
        host.Render();
        Thread.Sleep(25);
        host.Render();
        Assert.Equal(0, participant.IdleCount);
        Assert.Single(participant.Frames, value => value == 0);
        var previousElapsed = participant.Frames[^1];
        host.Tracker.ExperimentalRunOnParticipant(value => ((Participant)value).Snapshot = 42);
        host.Render();
        Assert.Single(participant.Frames, value => value == 0);
        Assert.True(participant.Frames[^1] >= previousElapsed);

        host.Tracker.ApplyWheelDelta(new Vector(0, 30), true);
        host.Render();
        var framesAfterNewInput = participant.Frames.Count;
        Thread.Sleep(25);
        host.Render();
        Assert.Equal(framesAfterNewInput, participant.Frames.Count);
        Assert.Equal(1, participant.CancelCount);
    }

    [AvaloniaFact]
    public void ProgrammaticAnimationUsesEffectiveBoundsAndPublishesPositionWithoutScrollConsumption()
    {
        using var host = new Host();
        var participant = new Participant { Maximum = 200 };
        host.Tracker.ConfigureExperimentalVerticalScroll(new Factory(participant));
        var animation = host.Tracker.Compositor.CreateVector3DKeyFrameAnimation();
        animation.InsertKeyFrame(1, new Vector3D(0, 180, 0));
        animation.Duration = TimeSpan.FromMilliseconds(25);
        host.Tracker.TryUpdatePositionWithAnimation(animation);
        host.Render();
        Thread.Sleep(40);
        host.Render();
        Assert.Equal(180, host.Tracker.Position.Y);
        Assert.Equal(180, participant.PositionUpdates[^1]);
        Assert.Empty(participant.Results);
    }

    [AvaloniaFact]
    public void PositionSnapshotsCoverAbsoluteAndIdleGeometryButTransactionsPublishOnlyCompletedResult()
    {
        using var host = new Host();
        var participant = new Participant { Maximum = 200 };
        host.Tracker.ConfigureExperimentalVerticalScroll(new Factory(participant));
        host.Tracker.ExperimentalApplyVerticalDelta(20);
        host.Render();
        Assert.Equal(new[] { 0.0 }, participant.PositionUpdates);
        Assert.Single(participant.Results);
        host.Tracker.TryUpdatePosition(new Vector3D(0, 150, 0));
        host.Render();
        Assert.Equal(150, participant.PositionUpdates[^1]);
        Assert.Single(participant.Results);
        host.Tracker.ExperimentalRunOnParticipant(value =>
        {
            var current = (Participant)value;
            current.Snapshot = -1;
            current.ShrinkAfterFrame = true;
            current.ContinueFrames = true;
        });
        host.Render();
        Assert.Equal(50, host.Tracker.Position.Y);
        Assert.Equal(50, participant.PositionUpdates[^1]);
        Assert.Single(participant.Results);
        host.Tracker.ExperimentalRunOnParticipant(value => ((Participant)value).Snapshot = -1);
        host.Render();
        Assert.Equal(50, participant.Snapshot);
        Assert.Single(participant.Results);
    }

    [AvaloniaFact]
    public void VerticalConsumerPreservesOrdinaryHorizontalInput()
    {
        using var host = new Host();
        host.Tracker.MaxPosition = new Vector3D(100, 100, 0);
        var participant = new Participant { ConsumeAll = true };
        host.Tracker.ConfigureExperimentalVerticalScroll(new Factory(participant));
        host.Tracker.ApplyWheelDelta(new Vector(25, 40), false);
        host.Render();
        Assert.Equal(25, host.Tracker.Position.X);
        Assert.Equal(0, host.Tracker.Position.Y);
        Assert.Equal(40, Assert.Single(participant.Results).PreConsumed);
    }

    [AvaloniaFact]
    public void SealedNativePresenterAttachmentDetachesAndReattachesAcrossVisualLifetime()
    {
        var view = new ScrollViewer { Content = new Border { Height = 1000 } };
        var navigationHost = new ContentControl { Content = view };
        var window = new Window { Width = 240, Height = 160, Content = navigationHost };
        try
        {
            window.Show();
            Render(window);
            var presenter = Assert.IsType<ScrollViewerPresenter>(view.Presenter);
            Assert.True(presenter.IsPhysicalScrollActive);
            var attached = 0;
            var detached = 0;
            using var registration = presenter.AttachExperimentalVerticalScroll(tracker =>
            {
                attached++;
                tracker.ConfigureExperimentalVerticalScroll(new Factory(new Participant()));
                return new CallbackDisposable(() =>
                {
                    tracker.ConfigureExperimentalVerticalScroll(null);
                    detached++;
                });
            });
            Assert.Equal(1, attached);
            navigationHost.Content = null;
            Render(window);
            Assert.Equal(1, detached);
            navigationHost.Content = view;
            Render(window);
            Assert.Equal(2, attached);
            registration.Dispose();
            Assert.Equal(2, detached);
        }
        finally { window.Close(); }
    }

    private sealed class Factory(Participant participant) : IExperimentalScrollMovementParticipantFactory
    {
        public int CreateCount { get; private set; }
        public IExperimentalScrollMovementParticipant Create()
        {
            CreateCount++;
            return participant;
        }
    }

    private sealed class CallbackDisposable(Action callback) : IDisposable
    {
        private Action? _callback = callback;
        public void Dispose() => Interlocked.Exchange(ref _callback, null)?.Invoke();
    }

    private sealed class Participant : IExperimentalScrollMovementParticipant
    {
        internal double Maximum = 100;
        internal bool ShrinkAfterPre;
        internal bool ShrinkAfterPost;
        internal bool ConsumeAll;
        internal bool ContinueFrames;
        internal bool ShrinkAfterFrame;
        internal double Snapshot;
        internal Action<double>? Publish;
        internal int IdleCount;
        internal int CancelCount;
        internal readonly List<double> Frames = [];
        internal readonly List<ExperimentalScrollMovementResult> Results = [];
        internal readonly List<double> PositionUpdates = [];
        public double PreScroll(in ExperimentalScrollMovementContext context)
        {
            if (ShrinkAfterPre) Maximum = 130;
            Snapshot += context.Delta;
            return ConsumeAll ? context.Delta : 0;
        }
        public double PostScroll(in ExperimentalScrollMovementContext context, double selfConsumed)
        {
            if (ShrinkAfterPost) Maximum = 100;
            return 0;
        }
        public ExperimentalScrollBounds GetVerticalBounds(in ExperimentalScrollMovementContext context) => new(0, Maximum);
        public void OnScrollCompleted(in ExperimentalScrollMovementResult result)
        {
            Results.Add(result);
            Publish?.Invoke(Snapshot);
        }
        public void OnIdle(ExperimentalScrollMovementSource source) => IdleCount++;
        public void OnPositionChanged(double position)
        {
            PositionUpdates.Add(position);
            if (ShrinkAfterFrame) Snapshot = position;
        }
        public bool OnFrame(double elapsedSeconds)
        {
            if (ShrinkAfterFrame) Maximum = 50;
            Frames.Add(elapsedSeconds);
            return ContinueFrames;
        }
        public void OnCancelled() => CancelCount++;
    }

    private sealed class Host : IDisposable
    {
        private readonly Window _window = new() { Width = 240, Height = 160, Content = new Border() };
        public InteractionTracker Tracker { get; }
        internal Host()
        {
            _window.Show();
            Tracker = ElementComposition.GetElementVisual(_window)!.Compositor.CreateInteractionTracker(null);
            Tracker.MaxPosition = new Vector3D(0, 100, 0);
        }
        public void Render() => ExperimentalScrollIntegrationTests.Render(_window);
        public void Dispose()
        {
            Tracker.Dispose();
            Render();
            _window.Close();
        }
    }

    private static void Render(Window window)
    {
        for (var i = 0; i < 3; i++)
            window.CaptureRenderedFrame()?.Dispose();
    }
}

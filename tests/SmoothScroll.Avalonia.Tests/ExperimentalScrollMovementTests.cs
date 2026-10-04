using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Rendering.Composition;
using SmoothScroll.Avalonia.Interaction;
using SmoothScroll.Avalonia.Interaction.Experimental;

namespace SmoothScroll.Avalonia.Tests;

public sealed class ExperimentalScrollMovementTests
{
    [AvaloniaTheory]
    [InlineData(90, 30, 10, 10, 5, 5, 100)]
    [InlineData(30, -50, -10, -30, -5, -5, 0)]
    public void ConsumptionRunsBeforeClampAndConservesMovement(double position, double requested,
        double pre, double self, double post, double remaining, double final)
    {
        using var host = new Host();
        var consumer = new Participant(pre, post);
        host.Tracker.TryUpdatePosition(new Vector3D(0, position, 0));
        host.Tracker.ConfigureExperimentalVerticalScroll(new Factory(consumer));
        host.Tracker.ExperimentalApplyVerticalDelta(requested);
        host.Render();

        var result = Assert.Single(consumer.Results);
        Assert.Equal(new[] { "create", "pre", "post", "complete" }, consumer.Calls);
        Assert.Equal(pre, result.PreConsumed);
        Assert.Equal(self, result.SelfConsumed);
        Assert.Equal(post, result.PostConsumed);
        Assert.Equal(remaining, result.Remaining);
        Assert.Equal(final, result.Position);
        Assert.Equal(requested, result.PreConsumed + result.SelfConsumed + result.PostConsumed + result.Remaining);
        Assert.Equal(final, host.Tracker.Position.Y);
    }

    [AvaloniaFact]
    public void AccumulatedFractionalConsumptionDoesNotLoseTransactionsToRounding()
    {
        using var host = new Host();
        var consumer = new Participant(0, 0) { AccumulatePre = true };
        host.Tracker.ConfigureExperimentalVerticalScroll(new Factory(consumer));
        const double delta = 0.07740103858055514;
        const int count = 1000;
        for (var i = 0; i < count; i++)
            host.Tracker.ExperimentalApplyVerticalDelta(delta);
        host.Render();

        Assert.Equal(count, consumer.Results.Count);
        Assert.Equal(count * delta, consumer.Results.Sum(result => result.Requested), 9);
        Assert.Equal(count * delta, consumer.Results.Sum(result => result.PreConsumed), 9);
        Assert.Equal(count * delta, consumer.PrePosition, 9);
        Assert.Equal(0, host.Tracker.Position.Y, 9);
        Assert.All(consumer.Results, result =>
        {
            Assert.InRange(result.PreConsumed, 0, result.Requested);
            Assert.Equal(result.Requested, result.PreConsumed + result.SelfConsumed + result.PostConsumed + result.Remaining, 12);
        });
    }

    [AvaloniaFact]
    public void SameBatchDirectAndWheelInputDoNotInventSampleVelocity()
    {
        using var host = new Host();
        var consumer = new Participant(0, 0);
        host.Tracker.ConfigureExperimentalVerticalScroll(new Factory(consumer));
        host.Tracker.ExperimentalApplyVerticalDelta(10);
        host.Tracker.ExperimentalApplyVerticalDelta(15);
        host.Tracker.ApplyWheelDelta(new Vector(0, 20), false);
        host.Tracker.ApplyWheelDelta(new Vector(0, 25), false);
        host.Render();

        Assert.Equal(new[] { 10.0, 15, 20, 25 }, consumer.Results.Select(result => result.Requested));
        Assert.Equal(new[] { ExperimentalScrollMovementSource.Direct, ExperimentalScrollMovementSource.Direct,
            ExperimentalScrollMovementSource.Wheel, ExperimentalScrollMovementSource.Wheel },
            consumer.Results.Select(result => result.Source));
        Assert.All(consumer.Results, result =>
        {
            Assert.Equal(0, result.Velocity);
            Assert.Equal(0, result.ElapsedSeconds);
        });
        Assert.Equal(70, host.Tracker.Position.Y);
    }

    [AvaloniaFact]
    public void AbsoluteUpdatesBypassConsumptionAndDetachIsOrderedWithInput()
    {
        using var host = new Host();
        var first = new Participant(5, 0);
        var second = new Participant(0, 0);
        host.Tracker.ConfigureExperimentalVerticalScroll(new Factory(first));
        host.Tracker.ExperimentalApplyVerticalDelta(20);
        host.Tracker.TryUpdatePosition(new Vector3D(0, 70, 0));
        host.Tracker.ConfigureExperimentalVerticalScroll(new Factory(second));
        host.Tracker.ExperimentalApplyVerticalDelta(10);
        host.Tracker.ConfigureExperimentalVerticalScroll(null);
        host.Tracker.ExperimentalApplyVerticalDelta(10);
        host.Render();

        Assert.Single(first.Results);
        Assert.Single(second.Results);
        Assert.Equal(15, first.Results[0].Position);
        Assert.Equal(80, second.Results[0].Position);
        Assert.Equal(new[] { "create", "pre", "post", "complete", "cancel", "cancel", "detach" }, first.Calls);
        Assert.Equal(new[] { "create", "pre", "post", "complete", "cancel", "detach" }, second.Calls);
        Assert.Equal(90, host.Tracker.Position.Y);
    }

    [AvaloniaFact]
    public void InertiaContinuesWhileExternalConsumerMovesAtStationaryBodyBoundary()
    {
        using var host = new Host();
        var consumer = new Participant(0, 0) { ConsumeAllPre = true };
        host.Tracker.ConfigureExperimentalVerticalScroll(new Factory(consumer));
        host.Tracker.ExperimentalStartVerticalInertia(-600);
        host.Render();
        for (var i = 0; i < 3; i++)
        {
            Thread.Sleep(20);
            host.Render();
        }
        host.Tracker.ExperimentalCancelScroll();
        host.Render();
        var countAfterCancel = consumer.Results.Count;
        Thread.Sleep(30);
        host.Render();

        Assert.True(countAfterCancel >= 3);
        Assert.All(consumer.Results, result =>
        {
            Assert.Equal(0, result.Position);
            Assert.Equal(0, result.SelfConsumed);
            Assert.True(result.Requested < 0);
            Assert.Equal(result.Requested, result.PreConsumed);
            Assert.Equal(ExperimentalScrollMovementSource.Inertia, result.Source);
            Assert.True(result.Velocity < 0);
            Assert.True(result.ElapsedSeconds > 0);
        });
        Assert.True(Math.Abs(consumer.Results[0].Velocity) > Math.Abs(consumer.Results[^1].Velocity));
        Assert.Equal(countAfterCancel, consumer.Results.Count);
        Assert.Equal(0, host.Tracker.Position.Y);
        Assert.Contains("cancel", consumer.Calls);
    }

    [AvaloniaFact]
    public void NoOpConsumerMatchesBaselineDirectWheelAndAbsolutePositions()
    {
        using var host = new Host();
        var baseline = host.Compositor.CreateInteractionTracker(null);
        baseline.MaxPosition = new Vector3D(0, 100, 0);
        var consumer = new Participant(0, 0);
        host.Tracker.ConfigureExperimentalVerticalScroll(new Factory(consumer));
        foreach (var delta in new[] { 30.0, -10, 200, -300, 50 })
        {
            host.Tracker.ApplyWheelDelta(new Vector(0, delta), false);
            baseline.ApplyWheelDelta(new Vector(0, delta), false);
            host.Render();
            Assert.Equal(baseline.Position, host.Tracker.Position);
        }
        Assert.All(consumer.Results, result => Assert.Equal(ExperimentalScrollMovementSource.Wheel, result.Source));
    }

    [AvaloniaFact]
    public void DisposeDuringInertiaDetachesBeforeOutputDisposalInSameBatch()
    {
        using var host = new Host();
        using var output = CompositionVector3Output.Create(host.Compositor);
        var consumer = new Participant(0, 0) { ConsumeAllPre = true, Writer = output.Writer };
        host.Tracker.ConfigureExperimentalVerticalScroll(new Factory(consumer));
        host.Tracker.ExperimentalStartVerticalInertia(-600);
        host.Render();
        Thread.Sleep(20);
        host.Render();
        Assert.NotEmpty(consumer.Results);

        // Even opposite disposal-set insertion order must run queued detach before output disposal.
        output.Dispose();
        host.Tracker.Dispose();
        host.Tracker.Dispose();
        host.Render();
        var count = consumer.Results.Count;
        Thread.Sleep(30);
        host.Render();
        Assert.Equal(count, consumer.Results.Count);
        Assert.Equal("detach", consumer.Calls[^1]);
        Assert.Single(consumer.Calls, call => call == "detach");
        Assert.True(host.Tracker.IsDisposed);
        Assert.Throws<ObjectDisposedException>(() => host.Tracker.ExperimentalApplyVerticalDelta(1));
    }

    private sealed class Factory(Participant participant) : IExperimentalScrollMovementParticipantFactory
    {
        public IExperimentalScrollMovementParticipant Create()
        {
            participant.Calls.Add("create");
            return participant;
        }
    }

    private sealed class Participant(double pre, double post) : IExperimentalScrollMovementParticipant
    {
        public readonly List<string> Calls = [];
        public readonly List<ExperimentalScrollMovementResult> Results = [];
        public bool ConsumeAllPre { get; init; }
        public bool AccumulatePre { get; init; }
        public double PrePosition { get; private set; }
        public CompositionVector3OutputWriter? Writer { get; init; }
        public double PreScroll(in ExperimentalScrollMovementContext context)
        {
            Calls.Add("pre");
            if (AccumulatePre)
            {
                var consumed = Math.Clamp(PrePosition + context.Delta, 0, 10000) - PrePosition;
                PrePosition += consumed;
                return consumed;
            }
            return ConsumeAllPre ? context.Delta : pre;
        }
        public double PostScroll(in ExperimentalScrollMovementContext context, double selfConsumed)
        {
            Calls.Add("post");
            return post;
        }
        public void OnScrollCompleted(in ExperimentalScrollMovementResult result)
        {
            Calls.Add("complete");
            Results.Add(result);
            Writer?.SetValue(new Vector3D(result.Requested, result.Position, 0));
        }
        public void OnCancelled()
        {
            Writer?.SetValue(default);
            Calls.Add("cancel");
        }
        public void Detach()
        {
            Writer?.SetValue(default);
            Calls.Add("detach");
        }
    }

    private sealed class Host : IDisposable
    {
        private readonly Window _window = new() { Width = 240, Height = 160, Content = new Border() };
        public Compositor Compositor { get; }
        public InteractionTracker Tracker { get; }
        public Host()
        {
            _window.Show();
            Compositor = ElementComposition.GetElementVisual(_window)!.Compositor;
            Tracker = Compositor.CreateInteractionTracker(null);
            Tracker.MaxPosition = new Vector3D(0, 100, 0);
        }
        public void Render()
        {
            for (var i = 0; i < 3; i++)
            {
                using var frame = _window.CaptureRenderedFrame();
            }
        }
        public void Dispose()
        {
            Tracker.Dispose();
            Render();
            _window.Close();
        }
    }
}

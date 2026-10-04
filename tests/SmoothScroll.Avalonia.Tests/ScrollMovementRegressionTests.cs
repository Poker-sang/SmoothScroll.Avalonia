using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Rendering.Composition;
using SmoothScroll.Avalonia.Interaction;

namespace SmoothScroll.Avalonia.Tests;

public sealed class ScrollMovementRegressionTests
{
    [AvaloniaTheory]
    [InlineData("Idle", false)]
    [InlineData("Interacting", false)]
    [InlineData("Inertia", false)]
    [InlineData("CustomAnimation", false)]
    [InlineData("Idle", true)]
    [InlineData("Interacting", true)]
    [InlineData("Inertia", true)]
    [InlineData("CustomAnimation", true)]
    public void DiagonalWheelCommitsOnePositionFromEveryState(string state, bool attachParticipant)
    {
        using var host = new Host();
        var participant = new Participant();
        if (attachParticipant)
            host.Tracker.ConfigureVerticalScroll(new Factory(participant));
        host.Tracker.TryUpdatePosition(new Vector3D(10, 20, 0));
        host.Render();
        host.Owner.Values.Clear();
        switch (state)
        {
            case "Interacting":
                host.Tracker.BeginUserManipulation(default, new Pointer(Pointer.GetNextFreeId(), PointerType.Touch, true));
                break;
            case "Inertia":
                host.Tracker.StartInertia(new Point(100, 100), false);
                break;
            case "CustomAnimation":
                var animation = host.Compositor.CreateVector3DKeyFrameAnimation();
                animation.Duration = TimeSpan.FromSeconds(10);
                animation.InsertKeyFrame(1, new Vector3D(80, 90, 0));
                host.Tracker.TryUpdatePositionWithAnimation(animation);
                break;
        }
        host.Tracker.ApplyWheelDelta(new Vector(5, 7), false);
        host.Render();

        Assert.Equal(new Vector3D(15, 27, 0), host.Tracker.Position);
        Assert.Equal(new Vector3D(15, 27, 0), Assert.Single(host.Owner.Values).Position);
        if (attachParticipant)
        {
            var result = Assert.Single(participant.Results);
            Assert.Equal(7, result.Requested);
            Assert.Equal(7, result.SelfConsumed);
            Assert.Equal(0, result.Remaining);
        }
    }

    [AvaloniaTheory]
    [InlineData(200, 150)]
    [InlineData(50, 45)]
    public void InertiaRestingPositionUsesLiveRangeInsteadOfStaleLayoutBounds(double maximum, double position)
    {
        using var host = new Host();
        host.Tracker.ConfigureVerticalScroll(new Factory(new Participant { Maximum = maximum }));
        host.Tracker.TryUpdatePosition(new Vector3D(0, position, 0));
        host.Render();
        host.Tracker.StartInertia(new Point(0, 600), false);
        host.Render();

        var inertia = Assert.Single(host.Owner.Inertias);
        Assert.Equal(maximum, inertia.ModifiedRestingPosition!.Value.Y);
        Assert.True(inertia.NaturalRestingPosition.Y > maximum);
        Assert.InRange(host.Tracker.Position.Y, position, maximum);
    }

    // Fixed samples from the pre-refactor body physics, including Float pointer samples,
    // elastic displacement and reversal while the raw drag position remains past the edge.
    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PointerTrajectoryPreservesFractionalSamplesAndBoundaryReversal(bool elastic)
    {
        using var host = new Host();
        host.Tracker.TryUpdatePosition(new Vector3D(20, 30, 0));
        host.Render();
        var samples = new TaskCompletionSource<Vector3D[]>(TaskCreationOptions.RunContinuationsAsynchronously);
        host.Tracker.RunOnServerThread(server =>
        {
            var state = new InteractingState(server, elastic);
            server.ChangeState(state);
            var positions = new List<Vector3D>();
            foreach (var delta in new[] { new Vector(.2, -.1), new Vector(200, 180), new Vector(-30, -40),
                         new Vector(-220, -230), new Vector(35, 100), new Vector(-.2, .1) })
            {
                state.ApplyManipulationDelta(delta);
                positions.Add(server.Position);
            }
            samples.SetResult(positions.ToArray());
        });
        await host.Compositor.RequestCommitAsync();
        var actual = await samples.Task;
        var expected = elastic
            ? new[] { new Vector3D(20.200000002980232, 29.899999998509884, 0),
                new Vector3D(156.69276483483222, 152.08777667121072, 0),
                new Vector3D(143.15376519129825, 133.76974733009334, 0),
                new Vector3D(-14.681249382729098, -29.173341100648823, 0),
                new Vector3D(5.200000002980232, 39.899999998509884, 0), new Vector3D(5, 40, 0) }
            : new[] { new Vector3D(20.200000002980232, 29.899999998509884, 0), new Vector3D(100, 100, 0),
                new Vector3D(100, 100, 0), default, new Vector3D(5.200000002980232, 39.899999998509884, 0), new Vector3D(5, 40, 0) };
        for (var i = 0; i < expected.Length; i++)
            AssertVector(expected[i], actual[i]);
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task InertiaTrajectoryPreservesDecayClampingAndBounce(bool elastic)
    {
        using var host = new Host();
        var samples = new TaskCompletionSource<(Vector3D Position, Vector3D Velocity)[]>(TaskCreationOptions.RunContinuationsAsynchronously);
        host.Tracker.RunOnServerThread(server =>
        {
            server.SetPosition(new Vector3D(95, -10, 0), 0);
            var handler = new CombinedInertiaHandler(server, new Vector3D(300, -90, 0),
                0, default, 0, elastic);
            var positions = new List<(Vector3D, Vector3D)>();
            foreach (var interval in new[] { 1d / 60, 1d / 120, 1d / 30, 1d / 60, 1d / 60, 1d / 60 })
            {
                handler.StepPosition(interval);
                positions.Add((server.Position, handler.PositionVelocity));
            }
            samples.SetResult(positions.ToArray());
        });
        await host.Compositor.RequestCommitAsync();
        var actual = await samples.Task;
        if (elastic)
        {
            AssertVector(new Vector3D(99.87393143655592, -10.255555555555555, 0), actual[0].Position);
            AssertVector(new Vector3D(285, -15.333333333333329, 0), actual[0].Velocity);
            AssertVector(new Vector3D(101.88259127942587, -5.197443159507702, 0), actual[^1].Position);
            AssertVector(new Vector3D(-12.335595465105484, 49.35777969441416, 0), actual[^1].Velocity);
        }
        else
        {
            AssertVector(new Vector3D(99.87393143655592, 0, 0), actual[0].Position);
            AssertVector(new Vector3D(285, 0, 0), actual[0].Velocity);
            Assert.All(actual.Skip(1), sample =>
            {
                AssertVector(new Vector3D(100, 0, 0), sample.Position);
                AssertVector(default, sample.Velocity);
            });
        }
    }

    private static void AssertVector(Vector3D expected, Vector3D actual)
    {
        Assert.Equal(expected.X, actual.X, 9);
        Assert.Equal(expected.Y, actual.Y, 9);
        Assert.Equal(expected.Z, actual.Z, 9);
    }

    private sealed class Factory(Participant participant) : IScrollMovementParticipantFactory
    {
        public IScrollMovementParticipant Create() => participant;
    }

    private sealed class Participant : IScrollMovementParticipant
    {
        internal List<ScrollMovementResult> Results { get; } = [];
        internal double Maximum { get; init; } = 100;
        public double PreScroll(in ScrollMovementContext context) => 0;
        public double PostScroll(in ScrollMovementContext context, double selfConsumed) => 0;
        public ScrollBounds GetVerticalBounds(in ScrollMovementContext context) => new(0, Maximum);
        public void OnScrollCompleted(in ScrollMovementResult result) => Results.Add(result);
    }

    private sealed class Owner : IInteractionTrackerOwner
    {
        internal List<InteractionTrackerValuesChangedArgs> Values { get; } = [];
        internal List<InteractionTrackerInertiaStateEnteredArgs> Inertias { get; } = [];
        public void ValuesChanged(InteractionTracker sender, InteractionTrackerValuesChangedArgs args) => Values.Add(args);
        public void IdleStateEntered(InteractionTracker sender, InteractionTrackerIdleStateEnteredArgs args) { }
        public void InteractingStateEntered(InteractionTracker sender, InteractionTrackerInteractingStateEnteredArgs args) { }
        public void InertiaStateEntered(InteractionTracker sender, InteractionTrackerInertiaStateEnteredArgs args) => Inertias.Add(args);
        public void CustomAnimationStateEntered(InteractionTracker sender, InteractionTrackerCustomAnimationStateEnteredArgs args) { }
        public void RequestIgnored(InteractionTracker sender, InteractionTrackerRequestIgnoredArgs args) { }
    }

    private sealed class Host : IDisposable
    {
        private readonly Window _window = new() { Width = 240, Height = 160, Content = new Border() };
        internal Owner Owner { get; } = new();
        internal Compositor Compositor { get; }
        internal InteractionTracker Tracker { get; }
        internal Host()
        {
            _window.Show();
            Compositor = ElementComposition.GetElementVisual(_window)!.Compositor;
            Tracker = Compositor.CreateInteractionTracker(Owner);
            Tracker.MaxPosition = new Vector3D(100, 100, 0);
            Render();
        }
        internal void Render()
        {
            for (var i = 0; i < 3; i++)
                using (_window.CaptureRenderedFrame()) { }
        }
        public void Dispose()
        {
            Tracker.Dispose();
            Render();
            _window.Close();
        }
    }
}

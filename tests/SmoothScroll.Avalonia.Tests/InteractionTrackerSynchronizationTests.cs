using Avalonia;
using Avalonia.Controls;
using Avalonia.Rendering.Composition;
using SmoothScroll.Avalonia.Interaction;

namespace SmoothScroll.Avalonia.Tests;

public sealed class InteractionTrackerSynchronizationTests
{
    [AvaloniaFact]
    public async Task ContentBoundsFollowScaleWithoutWaitingForTheUiThread()
    {
        var window = new Window { Width = 800, Height = 600, Content = new Border() };
        try
        {
            window.Show();
            var compositor = ElementComposition.GetElementVisual(window)!.Compositor;
            var tracker = compositor.CreateInteractionTracker(owner: null);
            tracker.ConfigureContentBounds(new(new Size(400, 300), new Size(800, 600), new Vector(0.5, 0.5)));
            await compositor.RequestCommitAsync();

            var samplesSource = new TaskCompletionSource<(Vector3D Position, Vector3D Minimum, Vector3D Maximum)[]>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            tracker.RunOnServerThread(server =>
            {
                var samples = new List<(Vector3D, Vector3D, Vector3D)>();
                // No UI notifications can be processed between these scale changes.
                foreach (var scale in new[] { 1.5, 2, 3, 1, 0.5 })
                {
                    server.SetScale(scale, new Vector3D(550, 400, 0), requestId: 0);
                    samples.Add((server.Position, server.MinPosition, server.MaxPosition));
                }

                samplesSource.SetResult(samples.ToArray());
            });
            await compositor.RequestCommitAsync();
            var samples = await samplesSource.Task;

            Assert.Equal(new Vector3D(-100, -75, 0), samples[0].Position);
            Assert.Equal(samples[0].Position, samples[0].Minimum);
            Assert.Equal(samples[0].Position, samples[0].Maximum);
            Assert.Equal(default, samples[1].Position);
            Assert.Equal(default, samples[1].Maximum);
            Assert.Equal(new Vector3D(275, 200, 0), samples[2].Position);
            Assert.Equal(default, samples[2].Minimum);
            Assert.Equal(new Vector3D(400, 300, 0), samples[2].Maximum);
            Assert.Equal(new Vector3D(-200, -150, 0), samples[3].Position);
            Assert.Equal(new Vector3D(-300, -225, 0), samples[4].Position);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task LayoutBoundsUseServerScaleEvenWhenTheUiMirrorIsBehind()
    {
        var window = new Window { Width = 800, Height = 600, Content = new Border() };
        try
        {
            window.Show();
            var compositor = ElementComposition.GetElementVisual(window)!.Compositor;
            var tracker = compositor.CreateInteractionTracker(owner: null);
            tracker.ConfigureContentBounds(new(new Size(400, 300), new Size(800, 600), new Vector(0.5, 0.5)));
            await compositor.RequestCommitAsync();
            tracker.RunOnServerThread(server => server.SetScale(1.5, new Vector3D(400, 300, 0), requestId: 0));
            await compositor.RequestCommitAsync();

            tracker.RaiseValuesChanged(new Vector3D(-200, -150, 0), 1, requestId: 0);
            tracker.ConfigureContentBounds(new(new Size(400, 300), new Size(1000, 800), new Vector(0.5, 0.5)));
            await compositor.RequestCommitAsync();

            var sampleSource = new TaskCompletionSource<(Vector3D Position, double Scale)>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            tracker.RunOnServerThread(server => sampleSource.SetResult((server.Position, server.Scale)));
            await compositor.RequestCommitAsync();
            var sample = await sampleSource.Task;

            Assert.Equal(1.5, sample.Scale);
            Assert.Equal(new Vector3D(-200, -175, 0), sample.Position);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task ServerValuesChangedDoesNotSerializeValuesBackToServer()
    {
        var window = new Window
        {
            Width = 200,
            Height = 120,
            Content = new Border()
        };

        try
        {
            window.Show();
            var visual = ElementComposition.GetElementVisual(window)
                         ?? throw new InvalidOperationException("The window does not have a composition visual.");
            var compositor = visual.Compositor;
            var tracker = compositor.CreateInteractionTracker(owner: null);

            await compositor.RequestCommitAsync();

            tracker.RaiseValuesChanged(new Vector3D(100, 200, 0), 2.5, requestId: 0);

            var serverValuesSource = new TaskCompletionSource<(Vector3D Position, double Scale)>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            tracker.RunOnServerThread(server => serverValuesSource.TrySetResult((server.Position, server.Scale)));
            await compositor.RequestCommitAsync();
            var serverValues = await serverValuesSource.Task;

            Assert.Equal(default, serverValues.Position);
            Assert.Equal(1, serverValues.Scale);
            Assert.Equal(new Vector3D(100, 200, 0), tracker.Position);
            Assert.Equal(2.5, tracker.Scale);
        }
        finally
        {
            window.Close();
        }
    }
}

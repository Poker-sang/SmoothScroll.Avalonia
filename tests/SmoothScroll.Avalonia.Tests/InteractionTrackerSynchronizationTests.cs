using Avalonia;
using Avalonia.Controls;
using Avalonia.Rendering.Composition;
using SmoothScroll.Avalonia.Interaction;

namespace SmoothScroll.Avalonia.Tests;

public sealed class InteractionTrackerSynchronizationTests
{
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

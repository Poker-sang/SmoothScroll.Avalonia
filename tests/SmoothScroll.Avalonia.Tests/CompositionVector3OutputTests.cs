using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Rendering.Composition;
using SmoothScroll.Avalonia.Interaction;
using SmoothScroll.Avalonia.Interaction.Experimental;

namespace SmoothScroll.Avalonia.Tests;

public sealed class CompositionVector3OutputTests
{
    [AvaloniaFact]
    public async Task LiveServerOutputInvalidatesVisualExpressionWithoutUpdatingLayout()
    {
        var marker = new Border { Width = 20, Height = 20, Background = Brushes.Lime };
        var canvas = new Canvas { Children = { marker } };
        var window = new Window
        {
            Width = 120,
            Height = 80,
            WindowDecorations = WindowDecorations.None,
            Background = Brushes.Black,
            Content = canvas
        };
        try
        {
            window.Show();
            using var initialFrame = window.CaptureRenderedFrame();
            var visual = ElementComposition.GetElementVisual(marker)!;
            var compositor = visual.Compositor;
            var tracker = compositor.CreateInteractionTracker(owner: null);
            using var output = CompositionVector3Output.Create(compositor);
            var expression = compositor.CreateExpressionAnimation("output.Value");
            expression.SetReferenceParameter("output", output);
            visual.StartAnimation("Offset", expression);

            await SetOutput(new Vector3D(20, 10, 0));
            using (var frame = Render())
            {
                Assert.True(IsLit(frame, 25, 15));
                Assert.False(IsLit(frame, 75, 15));
            }

            await SetOutput(new Vector3D(70, 10, 0));
            using (var frame = Render())
            {
                Assert.False(IsLit(frame, 25, 15));
                Assert.True(IsLit(frame, 75, 15));
            }
            Assert.Equal(new Rect(0, 0, 20, 20), marker.Bounds);
            visual.StopAnimation("Offset");

            async Task SetOutput(Vector3D value)
            {
                var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                tracker.RunOnServerThread(_ =>
                {
                    output.Writer.SetValue(value);
                    Assert.Equal(value, output.Writer.Value);
                    completion.SetResult();
                });
                await compositor.RequestCommitAsync();
                await completion.Task.WaitAsync(TimeSpan.FromSeconds(10));
            }

            WriteableBitmap Render()
            {
                for (var i = 0; i < 3; i++)
                    window.CaptureRenderedFrame()?.Dispose();
                return window.CaptureRenderedFrame()
                       ?? throw new InvalidOperationException("No rendered headless frame.");
            }
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task WriterRejectsAccessOutsideCompositorAndAfterDisposal()
    {
        var window = new Window { Width = 120, Height = 80, Content = new Border() };
        try
        {
            window.Show();
            var compositor = ElementComposition.GetElementVisual(window)!.Compositor;
            var tracker = compositor.CreateInteractionTracker(owner: null);
            using var output = CompositionVector3Output.Create(compositor);
            Assert.Throws<InvalidOperationException>(() => output.Writer.SetValue(new Vector3D(1, 2, 3)));
            Assert.Throws<InvalidOperationException>(() => output.Writer.Value);
            output.Dispose();
            await compositor.RequestCommitAsync();

            var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            tracker.RunOnServerThread(_ =>
            {
                Assert.Throws<ObjectDisposedException>(() => output.Writer.SetValue(new Vector3D(1, 2, 3)));
                Assert.Throws<ObjectDisposedException>(() => output.Writer.Value);
                completion.SetResult();
            });
            await compositor.RequestCommitAsync();
            await completion.Task.WaitAsync(TimeSpan.FromSeconds(10));
        }
        finally
        {
            window.Close();
        }
    }

    private static bool IsLit(WriteableBitmap bitmap, int x, int y)
    {
        using var framebuffer = bitmap.Lock();
        var pixel = framebuffer.Address + y * framebuffer.RowBytes + x * 4;
        return Marshal.ReadByte(pixel) + Marshal.ReadByte(pixel, 1) + Marshal.ReadByte(pixel, 2) > 100;
    }
}

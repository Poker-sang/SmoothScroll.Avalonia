using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Rendering.Composition;
using SmoothScroll.Avalonia.Interaction;
using SmoothScroll.Avalonia.Composition;

namespace SmoothScroll.Avalonia.Tests;

public sealed class CompositionColorOutputTests
{
    [AvaloniaFact]
    public async Task ColorOutputInvalidatesSolidVisualExpressionAndEnforcesWriterLifetime()
    {
        var border = new Canvas { Width = 80, Height = 60, Background = Brushes.Black };
        var window = new Window { Width = 80, Height = 60, WindowDecorations = WindowDecorations.None, Content = border };
        try
        {
            window.Show();
            using var initial = window.CaptureRenderedFrame();
            var compositor = ElementComposition.GetElementVisual(border)!.Compositor;
            using var tracker = compositor.CreateInteractionTracker(null);
            using var output = CompositionColorOutput.Create(compositor);
            Assert.Throws<InvalidOperationException>(() => output.Writer.SetValue(Colors.Red));
            var visual = compositor.CreateAnimatedSolidColorVisual();
            visual.Size = new Vector(80, 60);
            visual.Visible = true;
            visual.Color = Colors.Lime;
            ElementComposition.SetElementChildVisual(border, visual);
            for (var i = 0; i < 3; i++) window.CaptureRenderedFrame()?.Dispose();
            var expression = compositor.CreateExpressionAnimation("output.Value");
            expression.SetReferenceParameter("output", output);
            visual.StartAndEvaluateAnimation("Color", expression);

            foreach (var color in new[] { Colors.Red, Colors.Blue })
            {
                var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                tracker.RunOnServerThread(_ =>
                {
                    output.Writer.SetValue(color);
                    Assert.Equal(color, output.Writer.Value);
                    completion.SetResult();
                });
                await compositor.RequestCommitAsync();
                await completion.Task.WaitAsync(TimeSpan.FromSeconds(10));
                for (var i = 0; i < 3; i++)
                    window.CaptureRenderedFrame()?.Dispose();
                using var frame = window.CaptureRenderedFrame()!;
                using var pixels = frame.Lock();
                var sample = border.TranslatePoint(new Point(10, 10), window)!.Value;
                var pixel = pixels.Address + (int)(sample.Y * window.RenderScaling) * pixels.RowBytes
                    + (int)(sample.X * window.RenderScaling) * 4;
                var rgba = pixels.Format == PixelFormat.Rgba8888;
                Assert.Equal(rgba ? color.R : color.B, Marshal.ReadByte(pixel));
                Assert.Equal(color.G, Marshal.ReadByte(pixel, 1));
                Assert.Equal(rgba ? color.B : color.R, Marshal.ReadByte(pixel, 2));
            }
            visual.StopAnimation("Color");
            ElementComposition.SetElementChildVisual(border, null);
            output.Dispose();
            await compositor.RequestCommitAsync();
            var disposed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            tracker.RunOnServerThread(_ =>
            {
                Assert.Throws<ObjectDisposedException>(() => output.Writer.Value);
                disposed.SetResult();
            });
            await compositor.RequestCommitAsync();
            await disposed.Task.WaitAsync(TimeSpan.FromSeconds(10));
        }
        finally { window.Close(); }
    }

}

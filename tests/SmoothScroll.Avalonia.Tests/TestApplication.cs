using Avalonia;
using Avalonia.Headless;
using Avalonia.Themes.Fluent;
using SmoothScroll.Avalonia.Controls;

namespace SmoothScroll.Avalonia.Tests;

public sealed class TestApplication : Application
{
    public TestApplication()
    {
        Styles.Add(new FluentTheme());
        Styles.Add(new ScrollViewDefaultTheme());
        Styles.Add(new ScrollViewerSmoothTheme());
    }

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<TestApplication>()
            .UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions
            {
                UseHeadlessDrawing = false
            });
}

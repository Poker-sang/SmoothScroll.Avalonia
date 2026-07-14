using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using SmoothScroll.Avalonia.Controls;

namespace SmoothScroll.Avalonia.Sample.Views.Pages;

public partial class SnapPointsPage : ContentPage
{
    public SnapPointsPage()
    {
        InitializeComponent();
    }

    private void PreviousButton_OnClick(object? sender, RoutedEventArgs e) => ScrollToAdjacentCard(-1);

    private void NextButton_OnClick(object? sender, RoutedEventArgs e) => ScrollToAdjacentCard(1);

    private void ScrollToAdjacentCard(int direction)
    {
        if (SnapItems.Children.Count is 0 || SnapScrollViewer.Presenter is not ScrollPresenter presenter)
            return;

        const double tolerance = 1;
        var viewportCenter = presenter.Offset.X + presenter.Viewport.Width * 0.5;
        var targetIndex = direction > 0 ? SnapItems.Children.Count - 1 : 0;

        if (direction > 0)
        {
            for (var i = 0; i < SnapItems.Children.Count; i++)
            {
                if (SnapItems.Children[i].Bounds.Center.X > viewportCenter + tolerance)
                {
                    targetIndex = i;
                    break;
                }
            }
        }
        else
        {
            for (var i = SnapItems.Children.Count - 1; i >= 0; i--)
            {
                if (SnapItems.Children[i].Bounds.Center.X < viewportCenter - tolerance)
                {
                    targetIndex = i;
                    break;
                }
            }
        }

        var offset = SnapItems.Children[targetIndex].Bounds.Center.X - presenter.Viewport.Width * 0.5;
        presenter.ScrollTo(new Vector(offset, 0), isAnimated: true);
    }
}

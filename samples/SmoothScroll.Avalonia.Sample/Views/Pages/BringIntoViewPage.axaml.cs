using Avalonia.Controls;
using Avalonia.Interactivity;
using SmoothScroll.Avalonia.Controls;

namespace SmoothScroll.Avalonia.Sample.Views.Pages;

public partial class BringIntoViewPage : ContentPage
{
    public BringIntoViewPage()
    {
        InitializeComponent();
    }

    private void AnimatedButton_OnClick(object? sender, RoutedEventArgs e) => BringSelectedTargetIntoView(isAnimated: true);

    private void ImmediateButton_OnClick(object? sender, RoutedEventArgs e) => BringSelectedTargetIntoView(isAnimated: false);

    private void BringSelectedTargetIntoView(bool isAnimated)
    {
        var target = TargetSelector.SelectedIndex switch
        {
            0 => UpperLeftTarget,
            1 => UpperRightTarget,
            2 => LowerLeftTarget,
            _ => LowerRightTarget
        };

        target.BringIntoView(isAnimated);
    }
}

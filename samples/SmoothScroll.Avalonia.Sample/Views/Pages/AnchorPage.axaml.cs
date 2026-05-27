using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using CommunityToolkit.Mvvm.ComponentModel;

namespace SmoothScroll.Avalonia.Sample.Views.Pages;

public partial class AnchorPage : ContentPage
{
    public List<A> AList { get; set; } = Enumerable.Range(0, 100).Select(i => new A { V = 40, Index = i }).ToList();

    public AnchorPage()
    {
        DataContext = this;
        InitializeComponent();
    }

    private void ItemsControl_OnContainerPrepared(object? sender, ContainerPreparedEventArgs e)
    {
        ScrollViewer.RegisterAnchorCandidate(e.Container);
    }

    private void ItemsControl_OnContainerClearing(object? sender, ContainerClearingEventArgs e)
    {
        ScrollViewer.UnregisterAnchorCandidate(e.Container);
    }

    private void ScrollViewer_OnPropertyChanged(object? sender, ScrollChangedEventArgs e)
    {
        Count++;
        CountBox.Text = Count.ToString();
        AnchorBox.Text = (ScrollViewer.CurrentAnchor?.DataContext as A)?.Index.ToString();
    }

    private async void Button_OnClick(object? sender, RoutedEventArgs e)
    {
        foreach (var a in AList)
        {
            a.V += 40;
            await Task.Delay(5);
        }
    }

    public int Count { get; set; }
}

public partial class A : ObservableObject
{
    public int Index { get; set; }

    [ObservableProperty]
    public partial int V { get; set; }
}

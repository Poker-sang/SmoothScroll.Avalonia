using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Threading;
using SmoothScroll.Avalonia.Controls;
using SmoothScroll.Avalonia.Interaction;

namespace SmoothScroll.Avalonia.Sample.Views.Pages;

public partial class PlaygroundPage : ContentPage
{
    private const double ScrollByStep = 100;

    public static IReadOnlyList<HorizontalAlignment> HorizontalAlignments { get; } =
        Enum.GetValues<HorizontalAlignment>();

    public static IReadOnlyList<VerticalAlignment> VerticalAlignments { get; } =
        Enum.GetValues<VerticalAlignment>();

    public static IReadOnlyList<ScrollBarVisibilityMode> ScrollBarVisibilities { get; } =
        Enum.GetValues<ScrollBarVisibilityMode>();

    public static IReadOnlyList<ScrollMode> ScrollModes { get; } =
        Enum.GetValues<ScrollMode>();

    public static IReadOnlyList<SnapPointsType> SnapPointTypes { get; } =
        Enum.GetValues<SnapPointsType>();

    public static IReadOnlyList<SnapPointsAlignment> SnapPointAlignments { get; } =
        Enum.GetValues<SnapPointsAlignment>();

    public static IReadOnlyList<ScrollInputGesture> GestureOptions { get; } =
        Enum.GetValues<ScrollInputGesture>();

    public static IReadOnlyList<KeyModifiers> ModifierOptions { get; } =
    [
        KeyModifiers.None,
        KeyModifiers.Shift,
        KeyModifiers.Control,
        KeyModifiers.Alt,
        KeyModifiers.Shift | KeyModifiers.Control,
        KeyModifiers.Shift | KeyModifiers.Alt,
        KeyModifiers.Control | KeyModifiers.Alt,
        KeyModifiers.Shift | KeyModifiers.Control | KeyModifiers.Alt
    ];

    public static IReadOnlyList<ScrollGestureAction> ActionOptions { get; } =
        Enum.GetValues<ScrollGestureAction>();

    public ObservableCollection<GestureBindingEditor> GestureEditors { get; } = [];

    private bool _synchronizingSliders = true;
    private bool _synchronizingGestureEditors;
    private bool _synchronizingAnchorControls = true;
    private int _anchorRequestCount;
    private int _lastOperationCorrelationId = ScrollView.NoCorrelationId;

    public PlaygroundPage()
    {
        InitializeComponent();
        PlaygroundScrollView.AnchorRequested += PlaygroundScrollView_OnAnchorRequested;
        PlaygroundScrollView.ScrollStarting += PlaygroundScrollView_OnScrollStarting;
        PlaygroundScrollView.ScrollCompleted += PlaygroundScrollView_OnScrollCompleted;
        PlaygroundScrollView.ScrollChanged += PlaygroundScrollView_OnScrollChanged;
        PlaygroundScrollView.ZoomStarting += PlaygroundScrollView_OnZoomStarting;
        PlaygroundScrollView.ZoomCompleted += PlaygroundScrollView_OnZoomCompleted;
        PlaygroundScrollView.Loaded += PlaygroundScrollView_OnLoaded;
        LoadGestureEditors(PlaygroundScrollView.GestureBindings);
        SynchronizeSliders();
        SynchronizeAnchorControls();
    }

    private void PlaygroundScrollView_OnLoaded(object? sender, RoutedEventArgs e)
    {
        PlaygroundScrollView.RegisterAnchorCandidate(PlaygroundContent);
        UpdatePresenterLoadedState();
        UpdateCurrentAnchorState();
    }

    private void PlaygroundScrollView_OnAnchorRequested(object? sender, ScrollingAnchorRequestedEventArgs e)
    {
        AnchorRequestCountText.Text = (++_anchorRequestCount).ToString();
        Dispatcher.UIThread.Post(UpdateCurrentAnchorState, DispatcherPriority.Loaded);
    }

    private void Presenter_OnLoaded(object? sender, RoutedEventArgs e)
    {
        if (sender is ScrollPresenter presenter)
            presenter.Loaded -= Presenter_OnLoaded;

        UpdatePresenterLoadedState();
    }

    private void UpdatePresenterLoadedState()
    {
        var presenter = PlaygroundScrollView.ScrollPresenter;
        if (presenter is null)
        {
            PresenterLoadedText.Text = "Missing";
        }
        else if (presenter.IsLoaded)
        {
            PresenterLoadedText.Text = bool.TrueString;
        }
        else
        {
            PresenterLoadedText.Text = "Pending";
            presenter.Loaded -= Presenter_OnLoaded;
            presenter.Loaded += Presenter_OnLoaded;
        }
    }

    private void UpdateCurrentAnchorState()
    {
        var anchor = PlaygroundScrollView.CurrentAnchor;
        CurrentAnchorText.Text = anchor?.Name ?? anchor?.GetType().Name ?? "None";
    }

    private void PlaygroundScrollView_OnScrollChanged(object? sender, ScrollViewChangedEventArgs e)
    {
        LastChangeSourceText.Text = e.ChangeSource.ToString();
        SynchronizeSliders();
        UpdateCurrentAnchorState();
    }

    private void PlaygroundScrollView_OnScrollStarting(object? sender, ScrollingScrollStartingEventArgs e)
    {
        _lastOperationCorrelationId = e.CorrelationId;
        LastOperationText.Text = $"Scroll #{e.CorrelationId}: Running";
    }

    private void PlaygroundScrollView_OnScrollCompleted(object? sender, ScrollingScrollCompletedEventArgs e)
    {
        if (e.CorrelationId == _lastOperationCorrelationId)
            LastOperationText.Text = $"Scroll #{e.CorrelationId}: {e.Result}";
    }

    private void PlaygroundScrollView_OnZoomStarting(object? sender, ScrollingZoomStartingEventArgs e)
    {
        _lastOperationCorrelationId = e.CorrelationId;
        LastOperationText.Text = $"Zoom #{e.CorrelationId}: Running";
    }

    private void PlaygroundScrollView_OnZoomCompleted(object? sender, ScrollingZoomCompletedEventArgs e)
    {
        if (e.CorrelationId == _lastOperationCorrelationId)
            LastOperationText.Text = $"Zoom #{e.CorrelationId}: {e.Result}";
    }

    private void SynchronizeSliders()
    {
        _synchronizingSliders = true;
        ZoomFactorSlider.Value = PlaygroundScrollView.ZoomFactor;
        _synchronizingSliders = false;
    }

    private void ZoomFactorSlider_OnValueChanged(object? sender, RangeBaseValueChangedEventArgs e)
    {
        if (!_synchronizingSliders)
            PlaygroundScrollView.ZoomTo(e.NewValue, false);
    }

    private void SynchronizeAnchorControls()
    {
        _synchronizingAnchorControls = true;
        var horizontalRatio = PlaygroundScrollView.HorizontalAnchorRatio;
        var verticalRatio = PlaygroundScrollView.VerticalAnchorRatio;
        HorizontalAnchorEnabled.IsChecked = !double.IsNaN(horizontalRatio);
        VerticalAnchorEnabled.IsChecked = !double.IsNaN(verticalRatio);
        if (!double.IsNaN(horizontalRatio))
            HorizontalAnchorRatioSlider.Value = horizontalRatio;
        if (!double.IsNaN(verticalRatio))
            VerticalAnchorRatioSlider.Value = verticalRatio;
        _synchronizingAnchorControls = false;
    }

    private void HorizontalAnchorEnabled_OnIsCheckedChanged(object? sender, RoutedEventArgs e)
    {
        if (!_synchronizingAnchorControls)
        {
            PlaygroundScrollView.HorizontalAnchorRatio = HorizontalAnchorEnabled.IsChecked is true
                ? HorizontalAnchorRatioSlider.Value
                : double.NaN;
        }
    }

    private void VerticalAnchorEnabled_OnIsCheckedChanged(object? sender, RoutedEventArgs e)
    {
        if (!_synchronizingAnchorControls)
        {
            PlaygroundScrollView.VerticalAnchorRatio = VerticalAnchorEnabled.IsChecked is true
                ? VerticalAnchorRatioSlider.Value
                : double.NaN;
        }
    }

    private void HorizontalAnchorRatioSlider_OnValueChanged(object? sender, RangeBaseValueChangedEventArgs e)
    {
        if (!_synchronizingAnchorControls && HorizontalAnchorEnabled.IsChecked is true)
            PlaygroundScrollView.HorizontalAnchorRatio = e.NewValue;
    }

    private void VerticalAnchorRatioSlider_OnValueChanged(object? sender, RangeBaseValueChangedEventArgs e)
    {
        if (!_synchronizingAnchorControls && VerticalAnchorEnabled.IsChecked is true)
            PlaygroundScrollView.VerticalAnchorRatio = e.NewValue;
    }

    private void AddGestureButton_OnClick(object? sender, RoutedEventArgs e)
    {
        var editor = GestureEditors.FirstOrDefault(value =>
            value is { InputGesture: ScrollInputGesture.MouseRightDrag, Modifiers: KeyModifiers.None });
        if (editor is null)
        {
            GestureEditors.Add(new GestureBindingEditor(
                ScrollInputGesture.MouseRightDrag,
                KeyModifiers.None,
                ScrollGestureAction.Pan,
                GestureEditor_OnChanged));
        }
        else
        {
            editor.Action = ScrollGestureAction.Pan;
        }

        ApplyGestureEditors();
    }

    private void RemoveGestureButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: GestureBindingEditor editor })
        {
            GestureEditors.Remove(editor);
            ApplyGestureEditors();
        }
    }

    private void ResetGesturesButton_OnClick(object? sender, RoutedEventArgs e)
    {
        var bindings = ScrollGestureBindings.CreateDefault();
        PlaygroundScrollView.GestureBindings = bindings;
        LoadGestureEditors(bindings);
    }

    private void GestureEditor_OnChanged(GestureBindingEditor editor)
    {
        if (_synchronizingGestureEditors)
            return;

        var duplicate = GestureEditors.FirstOrDefault(value =>
            !ReferenceEquals(value, editor)
            && value.InputGesture == editor.InputGesture
            && value.Modifiers == editor.Modifiers);
        if (duplicate is not null)
            GestureEditors.Remove(duplicate);

        ApplyGestureEditors();
    }

    private void LoadGestureEditors(IReadOnlyDictionary<ScrollGesture, ScrollGestureAction> bindings)
    {
        _synchronizingGestureEditors = true;
        GestureEditors.Clear();
        foreach (var (gesture, action) in bindings)
        {
            GestureEditors.Add(new GestureBindingEditor(
                gesture.InputGesture,
                gesture.Modifiers,
                action,
                GestureEditor_OnChanged));
        }

        _synchronizingGestureEditors = false;
    }

    private void ApplyGestureEditors()
    {
        if (_synchronizingGestureEditors)
            return;

        var bindings = new ScrollGestureBindings();
        foreach (var editor in GestureEditors)
        {
            bindings[new ScrollGesture(editor.InputGesture, editor.Modifiers)] = editor.Action;
        }

        PlaygroundScrollView.GestureBindings = bindings;
    }

    private void ZoomInButton_OnClick(object? sender, RoutedEventArgs e) =>
        PlaygroundScrollView.ZoomTo(PlaygroundScrollView.ZoomFactor * 1.2);

    private void ZoomOutButton_OnClick(object? sender, RoutedEventArgs e) =>
        PlaygroundScrollView.ZoomTo(PlaygroundScrollView.ZoomFactor / 1.2);

    private void ResetZoomButton_OnClick(object? sender, RoutedEventArgs e) =>
        PlaygroundScrollView.ZoomTo(1);

    private void ZoomTopLeftButton_OnClick(object? sender, RoutedEventArgs e) =>
        PlaygroundScrollView.ZoomBy(0.25, new Point(0, 0));

    private void ZoomBottomRightButton_OnClick(object? sender, RoutedEventArgs e) =>
        PlaygroundScrollView.ZoomBy(
            0.25,
            new Point(PlaygroundScrollView.Viewport.Width, PlaygroundScrollView.Viewport.Height));

    private void ScrollOriginButton_OnClick(object? sender, RoutedEventArgs e) =>
        PlaygroundScrollView.ScrollTo(default);

    private void ScrollCenterButton_OnClick(object? sender, RoutedEventArgs e)
    {
        var maximum = PlaygroundScrollView.ScrollBarMaximum;
        PlaygroundScrollView.ScrollTo(new Vector(maximum.X / 2, maximum.Y / 2), true);
    }

    private void ScrollByLeftButton_OnClick(object? sender, RoutedEventArgs e) =>
        ScrollBy(new Vector(-ScrollByStep, 0));

    private void ScrollByUpButton_OnClick(object? sender, RoutedEventArgs e) =>
        ScrollBy(new Vector(0, -ScrollByStep));

    private void ScrollByDownButton_OnClick(object? sender, RoutedEventArgs e) =>
        ScrollBy(new Vector(0, ScrollByStep));

    private void ScrollByRightButton_OnClick(object? sender, RoutedEventArgs e) =>
        ScrollBy(new Vector(ScrollByStep, 0));

    private void ScrollByViewportLeftButton_OnClick(object? sender, RoutedEventArgs e) =>
        ScrollBy(new Vector(-PlaygroundScrollView.Viewport.Width, 0));

    private void ScrollByViewportUpButton_OnClick(object? sender, RoutedEventArgs e) =>
        ScrollBy(new Vector(0, -PlaygroundScrollView.Viewport.Height));

    private void ScrollByViewportDownButton_OnClick(object? sender, RoutedEventArgs e) =>
        ScrollBy(new Vector(0, PlaygroundScrollView.Viewport.Height));

    private void ScrollByViewportRightButton_OnClick(object? sender, RoutedEventArgs e) =>
        ScrollBy(new Vector(PlaygroundScrollView.Viewport.Width, 0));

    private void ScrollBy(Vector offsetDelta) =>
        PlaygroundScrollView.ScrollBy(offsetDelta, isAnimated: true);

    private void ResetViewMenuItem_OnClick(object? sender, RoutedEventArgs e)
    {
        PlaygroundScrollView.ZoomTo(1, false);
        PlaygroundScrollView.ScrollTo(default);
    }
}

public sealed class GestureBindingEditor : INotifyPropertyChanged
{
    private readonly Action<GestureBindingEditor> _changed;
    private ScrollInputGesture _inputGesture;
    private KeyModifiers _modifiers;
    private ScrollGestureAction _action;

    public GestureBindingEditor(
        ScrollInputGesture inputGesture,
        KeyModifiers modifiers,
        ScrollGestureAction action,
        Action<GestureBindingEditor> changed)
    {
        _inputGesture = inputGesture;
        _modifiers = modifiers;
        _action = action;
        _changed = changed;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ScrollInputGesture InputGesture
    {
        get => _inputGesture;
        set => SetField(ref _inputGesture, value);
    }

    public KeyModifiers Modifiers
    {
        get => _modifiers;
        set => SetField(ref _modifiers, value);
    }

    public ScrollGestureAction Action
    {
        get => _action;
        set => SetField(ref _action, value);
    }

    private void SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return;

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        _changed(this);
    }
}

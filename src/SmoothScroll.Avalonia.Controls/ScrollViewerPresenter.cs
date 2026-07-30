using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Interactivity;
using Avalonia.Reactive;
using Avalonia.Utilities;
using Avalonia.VisualTree;
using Vector = Avalonia.Vector;

namespace SmoothScroll.Avalonia.Controls;

/// <summary>
/// Adapts the shared smooth scrolling engine to native <see cref="ScrollViewer"/> layout and logical scrolling semantics.
/// </summary>
public sealed class ScrollViewerPresenter : ScrollPresenter
{
    private CompositeDisposable? _ownerSubscriptions;
    private CompositeDisposable? _logicalScrollSubscriptions;
    private ILogicalScrollable? _logicalScrollable;
    private bool _logicalScrollActive;
    private ScrollViewer? _owner;
    private bool _synchronizingOwnerOffset;

    /// <inheritdoc />
    protected override bool IsLogicalScrollActive => _logicalScrollActive;

    /// <inheritdoc />
    protected override void OnLoaded(RoutedEventArgs e)
    {
        // Keep owner bindings across temporary visual-tree detachment so cached pages retain their scroll state.
        AttachToScrollViewer();
        base.OnLoaded(e);
    }

    /// <inheritdoc />
    protected override void OnChildChanged(Control? child) => UpdateLogicalScrollableSubscription(child);

    /// <inheritdoc />
    protected override void OnIsZoomEnabledChanged() => UpdateLogicalScrollableSubscription(Child);

    /// <inheritdoc />
    protected override void OnOffsetChanged(Vector offset)
    {
        if (_owner is null || _synchronizingOwnerOffset || _owner.Offset.NearlyEquals(offset))
            return;

        try
        {
            _synchronizingOwnerOffset = true;
            _owner.SetCurrentValue(ScrollViewer.OffsetProperty, offset);
        }
        finally
        {
            _synchronizingOwnerOffset = false;
        }
    }

    /// <inheritdoc />
    protected override void OnExtentChanged(Size extent)
    {
        if (_owner is not null)
            _owner.Extent = extent;
    }

    /// <inheritdoc />
    protected override void OnViewportChanged(Size viewport)
    {
        if (_owner is not null)
            _owner.Viewport = viewport;
    }

    /// <inheritdoc />
    protected override void UpdateCanScroll(bool horizontalOverflow, bool verticalOverflow) =>
        UpdateComputedScrollModeProperties();

    /// <inheritdoc />
    protected override bool CanScrollHorizontally(bool hasScrollableRange) => CanHorizontallyScroll;

    /// <inheritdoc />
    protected override bool CanScrollVertically(bool hasScrollableRange) => CanVerticallyScroll;

    /// <inheritdoc />
    protected override Size GetContentExtent() =>
        IsLogicalScrollActive ? _logicalScrollable!.Extent : base.GetContentExtent();

    /// <inheritdoc />
    protected override double GetHorizontalContentAlignmentRatio() => 0;

    /// <inheritdoc />
    protected override double GetVerticalContentAlignmentRatio() => 0;

    /// <inheritdoc />
    protected override Vector GetArrangeOffset() => -Offset;

    /// <inheritdoc />
    protected override void SynchronizeLogicalScrollState()
    {
        if (_logicalScrollable is { } scrollable)
            UpdateFromLogicalScrollable(scrollable);
    }

    /// <inheritdoc />
    protected override bool BringLogicalDescendantIntoView(Control target, Rect targetRect, bool isAnimated)
    {
        var scrollable = _logicalScrollable!;
        var startingOffset = scrollable.Offset;
        if (!scrollable.BringIntoView(target, targetRect))
            return false;

        var targetOffset = scrollable.Offset;
        if (startingOffset.NearlyEquals(targetOffset))
            return false;

        UpdateFromLogicalScrollable(scrollable);
        if (!isAnimated)
            return true;

        scrollable.Offset = startingOffset;
        UpdateFromLogicalScrollable(scrollable);
        _ = ScrollTo(
            targetOffset,
            isAnimated: true,
            ScrollChangeSource.Programmatic,
            startingOffset);
        return true;
    }

    /// <summary>
    /// Locates the containing <see cref="ScrollViewer"/> and binds properties which were not supplied by its template.
    /// </summary>
    private void AttachToScrollViewer()
    {
        var owner = this.FindAncestorOfType<ScrollViewer>();
        if (owner is null)
        {
            DetachFromScrollViewer();
            return;
        }

        if (ReferenceEquals(owner, _owner))
            return;

        DetachFromScrollViewer();
        _owner = owner;

        // Custom ScrollViewer themes may omit the semantic setters used by the bundled theme.
        var subscriptionDisposables = new IDisposable?[]
        {
            IfUnset(CanHorizontallyScrollProperty, property => Bind(property, owner.GetObservable(ScrollViewer.HorizontalScrollBarVisibilityProperty, NotDisabled), BindingPriority.Template)),
            IfUnset(CanVerticallyScrollProperty, property => Bind(property, owner.GetObservable(ScrollViewer.VerticalScrollBarVisibilityProperty, NotDisabled), BindingPriority.Template)),
            IfUnset(IsHorizontalMeasureInfiniteProperty, property => Bind(property, owner.GetObservable(ScrollViewer.HorizontalScrollBarVisibilityProperty, NotDisabled), BindingPriority.Template)),
            IfUnset(IsVerticalMeasureInfiniteProperty, property => Bind(property, owner.GetObservable(ScrollViewer.VerticalScrollBarVisibilityProperty, NotDisabled), BindingPriority.Template)),
            IfUnset(HorizontalScrollModeProperty, property => Bind(property, owner.GetObservable(ScrollViewer.HorizontalScrollBarVisibilityProperty, ToScrollMode), BindingPriority.Template)),
            IfUnset(VerticalScrollModeProperty, property => Bind(property, owner.GetObservable(ScrollViewer.VerticalScrollBarVisibilityProperty, ToScrollMode), BindingPriority.Template)),
            IfUnset(OffsetProperty, property => Bind(property, owner.GetBindingObservable(ScrollViewer.OffsetProperty), BindingPriority.Template)),
            IfUnset(HorizontalContentAlignmentProperty, property => Bind(property, owner.GetBindingObservable(ContentControl.HorizontalContentAlignmentProperty), BindingPriority.Template)),
            IfUnset(VerticalContentAlignmentProperty, property => Bind(property, owner.GetBindingObservable(ContentControl.VerticalContentAlignmentProperty), BindingPriority.Template)),
            IfUnset(IsScrollChainingEnabledProperty, property => Bind(property, owner.GetBindingObservable(ScrollViewer.IsScrollChainingEnabledProperty), BindingPriority.Template)),
            IfUnset(ContentProperty, property => Bind(property, owner.GetBindingObservable(ContentProperty), BindingPriority.Template)),
        }.OfType<IDisposable>().ToArray();

        _ownerSubscriptions = new CompositeDisposable(subscriptionDisposables);
        UpdateLogicalScrollableSubscription(Child);

        static bool NotDisabled(ScrollBarVisibility value) => value is not ScrollBarVisibility.Disabled;

        static ScrollMode ToScrollMode(ScrollBarVisibility value) =>
            value is ScrollBarVisibility.Disabled ? ScrollMode.Disabled : ScrollMode.Enabled;

        IDisposable? IfUnset<TProperty>(TProperty property, Func<TProperty, IDisposable> bind)
            where TProperty : AvaloniaProperty =>
            IsSet(property) ? null : bind(property);
    }

    private void DetachFromScrollViewer()
    {
        // Disposing the template-priority bindings restores their fallback values.
        // Stop forwarding presenter changes before Offset falls back to zero.
        _owner = null;
        _ownerSubscriptions?.Dispose();
        _ownerSubscriptions = null;
    }

    private void UpdateLogicalScrollableSubscription(Control? child)
    {
        var wasActive = IsLogicalScrollActive;
        _logicalScrollSubscriptions?.Dispose();
        _logicalScrollSubscriptions = null;
        _logicalScrollable = child as ILogicalScrollable;
        _logicalScrollActive = _logicalScrollable is { IsLogicalScrollEnabled: true }
                               && !IsZoomEnabled;

        if (_logicalScrollable is not { } scrollable)
        {
            UpdateLogicalScrollMode(wasActive);
            return;
        }

        scrollable.ScrollInvalidated += LogicalScrollableScrollInvalidated;
        var subscriptions = new List<IDisposable>
        {
            Disposable.Create(() => scrollable.ScrollInvalidated -= LogicalScrollableScrollInvalidated)
        };

        if (IsLogicalScrollActive)
        {
            subscriptions.Add(this.GetObservable(CanHorizontallyScrollProperty)
                .Subscribe(value => scrollable.CanHorizontallyScroll = value));
            subscriptions.Add(this.GetObservable(CanVerticallyScrollProperty)
                .Subscribe(value => scrollable.CanVerticallyScroll = value));
            subscriptions.Add(this.GetObservable(OffsetProperty)
                .Skip(1)
                .Subscribe(value => scrollable.Offset = value));
        }

        _logicalScrollSubscriptions = new CompositeDisposable(subscriptions);
        UpdateFromLogicalScrollable(scrollable);
        UpdateLogicalScrollMode(wasActive);
    }

    private void LogicalScrollableScrollInvalidated(object? sender, EventArgs e)
    {
        if (sender is ILogicalScrollable scrollable)
            UpdateFromLogicalScrollable(scrollable);
    }

    private void UpdateFromLogicalScrollable(ILogicalScrollable scrollable)
    {
        var shouldBeActive = scrollable.IsLogicalScrollEnabled && !IsZoomEnabled;
        if (shouldBeActive != IsLogicalScrollActive)
        {
            UpdateLogicalScrollableSubscription(Child);
            SetCurrentValue(OffsetProperty, default);
            InvalidateMeasure();
            return;
        }

        if (IsLogicalScrollActive)
            ApplyLogicalScrollState(scrollable.Viewport, scrollable.Offset);
    }
}

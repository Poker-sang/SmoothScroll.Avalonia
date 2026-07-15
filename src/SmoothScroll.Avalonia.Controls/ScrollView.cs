using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Utilities;
using SmoothScroll.Avalonia.Interaction;

namespace SmoothScroll.Avalonia.Controls;

/// <summary>
/// Provides smooth scrolling, panning, zooming, and optional scroll bars for a single child.
/// </summary>
/// <remarks>
/// Unlike <see cref="ScrollViewer"/>, this control owns a composition-based interaction tracker.
/// Its <see cref="Offset"/> is always a zero-based logical offset, including when a child smaller
/// than the viewport is visually aligned away from the top-left corner.
/// </remarks>
[TemplatePart("PART_ContentPresenter", typeof(ScrollPresenter))]
[TemplatePart("PART_HorizontalScrollBar", typeof(ScrollBar))]
[TemplatePart("PART_VerticalScrollBar", typeof(ScrollBar))]
public sealed partial class ScrollView : ContentControl, IScrollable, IScrollAnchorProvider
{
    private const double ScrollBarSmallChange = 16;

    /// <summary>
    /// Defines the <see cref="Extent"/> property.
    /// </summary>
    public static readonly DirectProperty<ScrollView, Size> ExtentProperty =
        AvaloniaProperty.RegisterDirect<ScrollView, Size>(nameof(Extent), view => view.Extent);

    /// <summary>
    /// Defines the <see cref="LogicalExtent"/> property.
    /// </summary>
    public static readonly DirectProperty<ScrollView, Size> LogicalExtentProperty =
        AvaloniaProperty.RegisterDirect<ScrollView, Size>(nameof(LogicalExtent), view => view.LogicalExtent);

    /// <summary>
    /// Defines the <see cref="Offset"/> property.
    /// </summary>
    public static readonly StyledProperty<Vector> OffsetProperty =
        AvaloniaProperty.Register<ScrollView, Vector>(nameof(Offset), coerce: CoerceOffset);

    /// <summary>
    /// Defines the <see cref="Viewport"/> property.
    /// </summary>
    public static readonly DirectProperty<ScrollView, Size> ViewportProperty =
        AvaloniaProperty.RegisterDirect<ScrollView, Size>(nameof(Viewport), view => view.Viewport);

    /// <summary>
    /// Defines the <see cref="ScrollBarMaximum"/> property.
    /// </summary>
    public static readonly DirectProperty<ScrollView, Vector> ScrollBarMaximumProperty =
        AvaloniaProperty.RegisterDirect<ScrollView, Vector>(nameof(ScrollBarMaximum), view => view.ScrollBarMaximum);

    /// <summary>
    /// Defines the <see cref="ComputedHorizontalScrollMode"/> property.
    /// </summary>
    public static readonly DirectProperty<ScrollView, ScrollMode> ComputedHorizontalScrollModeProperty =
        AvaloniaProperty.RegisterDirect<ScrollView, ScrollMode>(
            nameof(ComputedHorizontalScrollMode),
            view => view.ComputedHorizontalScrollMode);

    /// <summary>
    /// Defines the <see cref="ComputedVerticalScrollMode"/> property.
    /// </summary>
    public static readonly DirectProperty<ScrollView, ScrollMode> ComputedVerticalScrollModeProperty =
        AvaloniaProperty.RegisterDirect<ScrollView, ScrollMode>(
            nameof(ComputedVerticalScrollMode),
            view => view.ComputedVerticalScrollMode);

    /// <summary>
    /// Defines the <see cref="ComputedHorizontalScrollBarVisibility"/> property.
    /// </summary>
    public static readonly DirectProperty<ScrollView, ScrollBarVisibilityMode> ComputedHorizontalScrollBarVisibilityProperty =
        AvaloniaProperty.RegisterDirect<ScrollView, ScrollBarVisibilityMode>(
            nameof(ComputedHorizontalScrollBarVisibility),
            view => view.ComputedHorizontalScrollBarVisibility);

    /// <summary>
    /// Defines the <see cref="ComputedVerticalScrollBarVisibility"/> property.
    /// </summary>
    public static readonly DirectProperty<ScrollView, ScrollBarVisibilityMode> ComputedVerticalScrollBarVisibilityProperty =
        AvaloniaProperty.RegisterDirect<ScrollView, ScrollBarVisibilityMode>(
            nameof(ComputedVerticalScrollBarVisibility),
            view => view.ComputedVerticalScrollBarVisibility);

    /// <summary>
    /// Defines the <see cref="ScrollPresenter"/> property.
    /// </summary>
    public static readonly DirectProperty<ScrollView, ScrollPresenter?> ScrollPresenterProperty =
        AvaloniaProperty.RegisterDirect<ScrollView, ScrollPresenter?>(
            nameof(ScrollPresenter),
            view => view.ScrollPresenter);

    /// <summary>
    /// Defines the <see cref="IsExpanded"/> property.
    /// </summary>
    public static readonly DirectProperty<ScrollView, bool> IsExpandedProperty =
        AvaloniaProperty.RegisterDirect<ScrollView, bool>(nameof(IsExpanded), view => view.IsExpanded);

    /// <summary>
    /// Defines the <see cref="HorizontalScrollBarVisibility"/> property.
    /// </summary>
    public static readonly StyledProperty<ScrollBarVisibilityMode> HorizontalScrollBarVisibilityProperty =
        AvaloniaProperty.Register<ScrollView, ScrollBarVisibilityMode>(nameof(HorizontalScrollBarVisibility));

    /// <summary>
    /// Defines the <see cref="VerticalScrollBarVisibility"/> property.
    /// </summary>
    public static readonly StyledProperty<ScrollBarVisibilityMode> VerticalScrollBarVisibilityProperty =
        AvaloniaProperty.Register<ScrollView, ScrollBarVisibilityMode>(nameof(VerticalScrollBarVisibility));

    /// <summary>
    /// Defines the <see cref="IsScrollChainingEnabled"/> property.
    /// </summary>
    public static readonly StyledProperty<bool> IsScrollChainingEnabledProperty =
        ScrollViewer.IsScrollChainingEnabledProperty.AddOwner<ScrollView>();

    /// <summary>
    /// Defines the <see cref="IsScrollInertiaEnabled"/> property.
    /// </summary>
    public static readonly StyledProperty<bool> IsScrollInertiaEnabledProperty =
        ScrollViewer.IsScrollInertiaEnabledProperty.AddOwner<ScrollView>();

    /// <summary>
    /// Defines the <see cref="IsDeferredScrollingEnabled"/> property.
    /// </summary>
    public static readonly StyledProperty<bool> IsDeferredScrollingEnabledProperty =
        ScrollViewer.IsDeferredScrollingEnabledProperty.AddOwner<ScrollView>();

    /// <summary>
    /// Defines the <see cref="HorizontalSnapPointsType"/> property.
    /// </summary>
    public static readonly StyledProperty<SnapPointsType> HorizontalSnapPointsTypeProperty =
        ScrollViewer.HorizontalSnapPointsTypeProperty.AddOwner<ScrollView>();

    /// <summary>
    /// Defines the <see cref="VerticalSnapPointsType"/> property.
    /// </summary>
    public static readonly StyledProperty<SnapPointsType> VerticalSnapPointsTypeProperty =
        ScrollViewer.VerticalSnapPointsTypeProperty.AddOwner<ScrollView>();

    /// <summary>
    /// Defines the <see cref="HorizontalSnapPointsAlignment"/> property.
    /// </summary>
    public static readonly StyledProperty<SnapPointsAlignment> HorizontalSnapPointsAlignmentProperty =
        ScrollViewer.HorizontalSnapPointsAlignmentProperty.AddOwner<ScrollView>();

    /// <summary>
    /// Defines the <see cref="VerticalSnapPointsAlignment"/> property.
    /// </summary>
    public static readonly StyledProperty<SnapPointsAlignment> VerticalSnapPointsAlignmentProperty =
        ScrollViewer.VerticalSnapPointsAlignmentProperty.AddOwner<ScrollView>();

    /// <summary>
    /// Defines the <see cref="MinZoomFactor"/> property.
    /// </summary>
    public static readonly StyledProperty<double> MinZoomFactorProperty =
        AvaloniaProperty.Register<ScrollView, double>(nameof(MinZoomFactor), 0.1, coerce: CoercePositive);

    /// <summary>
    /// Defines the <see cref="MaxZoomFactor"/> property.
    /// </summary>
    public static readonly StyledProperty<double> MaxZoomFactorProperty =
        AvaloniaProperty.Register<ScrollView, double>(nameof(MaxZoomFactor), 10, coerce: CoercePositive);

    /// <summary>
    /// Defines the <see cref="ZoomFactor"/> property.
    /// </summary>
    public static readonly DirectProperty<ScrollView, double> ZoomFactorProperty =
        AvaloniaProperty.RegisterDirect<ScrollView, double>(
            nameof(ZoomFactor),
            view => view.ZoomFactor,
            (view, value) => view.ZoomFactor = value);

    /// <summary>
    /// Defines the <see cref="IsZoomEnabled"/> property.
    /// </summary>
    public static readonly StyledProperty<bool> IsZoomEnabledProperty =
        AvaloniaProperty.Register<ScrollView, bool>(nameof(IsZoomEnabled));

    /// <summary>
    /// Defines the <see cref="IsHorizontalMeasureInfinite"/> property.
    /// </summary>
    public static readonly StyledProperty<bool> IsHorizontalMeasureInfiniteProperty =
        AvaloniaProperty.Register<ScrollView, bool>(nameof(IsHorizontalMeasureInfinite));

    /// <summary>
    /// Defines the <see cref="IsVerticalMeasureInfinite"/> property.
    /// </summary>
    public static readonly StyledProperty<bool> IsVerticalMeasureInfiniteProperty =
        AvaloniaProperty.Register<ScrollView, bool>(nameof(IsVerticalMeasureInfinite), true);

    /// <summary>
    /// Defines the <see cref="HorizontalScrollMode"/> property.
    /// </summary>
    public static readonly StyledProperty<ScrollMode> HorizontalScrollModeProperty =
        AvaloniaProperty.Register<ScrollView, ScrollMode>(nameof(HorizontalScrollMode), ScrollMode.Auto);

    /// <summary>
    /// Defines the <see cref="VerticalScrollMode"/> property.
    /// </summary>
    public static readonly StyledProperty<ScrollMode> VerticalScrollModeProperty =
        AvaloniaProperty.Register<ScrollView, ScrollMode>(nameof(VerticalScrollMode), ScrollMode.Auto);

    /// <summary>
    /// Defines the <see cref="HorizontalAnchorRatio"/> property.
    /// </summary>
    public static readonly StyledProperty<double> HorizontalAnchorRatioProperty =
        AvaloniaProperty.Register<ScrollView, double>(nameof(HorizontalAnchorRatio), coerce: CoerceAnchorRatio);

    /// <summary>
    /// Defines the <see cref="VerticalAnchorRatio"/> property.
    /// </summary>
    public static readonly StyledProperty<double> VerticalAnchorRatioProperty =
        AvaloniaProperty.Register<ScrollView, double>(nameof(VerticalAnchorRatio), coerce: CoerceAnchorRatio);

    /// <summary>
    /// Defines the <see cref="GestureBindings"/> property.
    /// </summary>
    public static readonly DirectProperty<ScrollView, ScrollGestureBindings> GestureBindingsProperty =
        AvaloniaProperty.RegisterDirect<ScrollView, ScrollGestureBindings>(
            nameof(GestureBindings),
            view => view.GestureBindings,
            (view, value) => view.GestureBindings = value);

    /// <summary>
    /// Defines the <see cref="ScrollInputMultiplier"/> property.
    /// </summary>
    public static readonly StyledProperty<double> ScrollInputMultiplierProperty =
        AvaloniaProperty.Register<ScrollView, double>(nameof(ScrollInputMultiplier), 1, coerce: CoerceNonNegative);

    /// <summary>
    /// Defines the <see cref="ZoomInputMultiplier"/> property.
    /// </summary>
    public static readonly StyledProperty<double> ZoomInputMultiplierProperty =
        AvaloniaProperty.Register<ScrollView, double>(nameof(ZoomInputMultiplier), 1, coerce: CoerceNonNegative);

    /// <summary>
    /// Defines the <see cref="ScrollInertiaDecayRate"/> property.
    /// </summary>
    public static readonly StyledProperty<double> ScrollInertiaDecayRateProperty =
        AvaloniaProperty.Register<ScrollView, double>(
            nameof(ScrollInertiaDecayRate),
            ScrollPhysicsDefaults.LegacyWheelAndZoomInertiaDecayRate,
            coerce: CoerceDecayRate);

    /// <summary>
    /// Defines the <see cref="ZoomInertiaDecayRate"/> property.
    /// </summary>
    public static readonly StyledProperty<double> ZoomInertiaDecayRateProperty =
        AvaloniaProperty.Register<ScrollView, double>(
            nameof(ZoomInertiaDecayRate),
            ScrollPhysicsDefaults.LegacyWheelAndZoomInertiaDecayRate,
            coerce: CoerceDecayRate);

    /// <summary>
    /// Defines the <see cref="OverscrollElasticity"/> property.
    /// </summary>
    public static readonly StyledProperty<double> OverscrollElasticityProperty =
        AvaloniaProperty.Register<ScrollView, double>(nameof(OverscrollElasticity), 0.5, coerce: CoerceUnitInterval);

    /// <summary>
    /// Defines the <see cref="OverscrollBounceRate"/> property.
    /// </summary>
    public static readonly StyledProperty<double> OverscrollBounceRateProperty =
        AvaloniaProperty.Register<ScrollView, double>(nameof(OverscrollBounceRate), 1, coerce: CoercePositive);

    /// <summary>
    /// Identifies the <see cref="ScrollChanged"/> routed event.
    /// </summary>
    public static readonly RoutedEvent<ScrollViewChangedEventArgs> ScrollChangedEvent =
        RoutedEvent.Register<ScrollView, ScrollViewChangedEventArgs>(nameof(ScrollChanged), RoutingStrategies.Bubble);

    /// <summary>
    /// Identifies the <see cref="ZoomChanged"/> routed event.
    /// </summary>
    public static readonly RoutedEvent<ZoomChangedEventArgs> ZoomChangedEvent =
        RoutedEvent.Register<ScrollView, ZoomChangedEventArgs>(nameof(ZoomChanged), RoutingStrategies.Bubble);

    private ScrollPresenter? _presenter;
    private ScrollBar? _horizontalScrollBar;
    private ScrollBar? _verticalScrollBar;
    private Size _extent;
    private Size _logicalExtent;
    private Size _viewport;
    private Vector _scrollBarMaximum;
    private ScrollMode _computedHorizontalScrollMode = ScrollMode.Disabled;
    private ScrollMode _computedVerticalScrollMode = ScrollMode.Disabled;
    private ScrollBarVisibilityMode _computedHorizontalScrollBarVisibility = ScrollBarVisibilityMode.Hidden;
    private ScrollBarVisibilityMode _computedVerticalScrollBarVisibility = ScrollBarVisibilityMode.Hidden;
    private bool _isExpanded;
    private double _zoomFactor = 1;
    private double _notifiedZoomFactor = 1;
    private Size _notifiedExtent;
    private Vector _notifiedOffset;
    private Size _notifiedViewport;
    private bool _updatingFromPresenter;

    /// <summary>
    /// Occurs when the presenter needs to select a new element anchor during layout.
    /// </summary>
    /// <remarks>
    /// Set <see cref="ScrollingAnchorRequestedEventArgs.AnchorElement"/> to override automatic ratio-based
    /// selection for the current request. Handlers run during layout and should complete quickly.
    /// </remarks>
    public event EventHandler<ScrollingAnchorRequestedEventArgs>? AnchorRequested;

    /// <summary>
    /// Occurs when the extent, viewport, or logical offset changes.
    /// </summary>
    /// <remarks>
    /// This routed event bubbles. Use <see cref="ScrollViewChangedEventArgs.ChangeSource"/> or
    /// <see cref="ScrollViewChangedEventArgs.IsUserInitiated"/> to distinguish user input from
    /// programmatic and layout-driven changes.
    /// </remarks>
    public event EventHandler<ScrollViewChangedEventArgs>? ScrollChanged
    {
        add => AddHandler(ScrollChangedEvent, value);
        remove => RemoveHandler(ScrollChangedEvent, value);
    }

    /// <summary>
    /// Occurs when the zoom factor changes.
    /// </summary>
    /// <remarks>
    /// This routed event bubbles. Use <see cref="ZoomChangedEventArgs.ChangeSource"/> or
    /// <see cref="ZoomChangedEventArgs.IsUserInitiated"/> to distinguish direct user input from
    /// programmatic and layout-driven changes.
    /// </remarks>
    public event EventHandler<ZoomChangedEventArgs>? ZoomChanged
    {
        add => AddHandler(ZoomChangedEvent, value);
        remove => RemoveHandler(ZoomChangedEvent, value);
    }

    /// <summary>
    /// Gets the current extent of the content in the <see cref="IScrollable"/> coordinate space.
    /// </summary>
    /// <remarks>
    /// This value includes the active <see cref="ZoomFactor"/> so that existing Avalonia
    /// consumers such as virtualizing panels continue to receive a consistent extent,
    /// viewport, and offset coordinate space.
    /// </remarks>
    public Size Extent => _extent;

    /// <summary>
    /// Gets the content extent before applying <see cref="ZoomFactor"/>.
    /// </summary>
    /// <remarks>
    /// Use this property when content dimensions should remain stable while the view is
    /// zoomed. It is an additional convenience property and does not replace the
    /// <see cref="IScrollable.Extent"/> contract exposed by <see cref="Extent"/>.
    /// </remarks>
    public Size LogicalExtent => _logicalExtent;

    /// <summary>
    /// Gets or sets the zero-based logical scroll offset.
    /// </summary>
    /// <remarks>
    /// Values are coerced to finite, non-negative coordinates within <see cref="ScrollBarMaximum"/>.
    /// Setting this property creates an immediate programmatic operation and is reported through
    /// <see cref="ScrollStarting"/>, <see cref="ScrollChanged"/>, and <see cref="ScrollCompleted"/>.
    /// </remarks>
    public Vector Offset
    {
        get => GetValue(OffsetProperty);
        set => SetValue(OffsetProperty, value);
    }

    /// <summary>
    /// Gets the size of the visible content area.
    /// </summary>
    public Size Viewport => _viewport;

    /// <summary>
    /// Gets the largest valid logical <see cref="Offset"/> on each axis.
    /// </summary>
    /// <remarks>
    /// An axis has a maximum of zero when it has no overflow or its <see cref="ScrollMode"/> is disabled.
    /// </remarks>
    public Vector ScrollBarMaximum => _scrollBarMaximum;

    /// <summary>
    /// Gets the effective horizontal user-input mode after resolving <see cref="HorizontalScrollMode"/>.
    /// </summary>
    /// <remarks>This property is always <see cref="ScrollMode.Enabled"/> or <see cref="ScrollMode.Disabled"/>.</remarks>
    public ScrollMode ComputedHorizontalScrollMode => _computedHorizontalScrollMode;

    /// <summary>
    /// Gets the effective vertical user-input mode after resolving <see cref="VerticalScrollMode"/>.
    /// </summary>
    /// <remarks>This property is always <see cref="ScrollMode.Enabled"/> or <see cref="ScrollMode.Disabled"/>.</remarks>
    public ScrollMode ComputedVerticalScrollMode => _computedVerticalScrollMode;

    /// <summary>
    /// Gets the effective horizontal scroll bar visibility after resolving <see cref="HorizontalScrollBarVisibility"/>.
    /// </summary>
    /// <remarks>
    /// This property is always <see cref="ScrollBarVisibilityMode.Visible"/> or
    /// <see cref="ScrollBarVisibilityMode.Hidden"/>. It does not describe the scroll bar's compact visual state.
    /// </remarks>
    public ScrollBarVisibilityMode ComputedHorizontalScrollBarVisibility => _computedHorizontalScrollBarVisibility;

    /// <summary>
    /// Gets the effective vertical scroll bar visibility after resolving <see cref="VerticalScrollBarVisibility"/>.
    /// </summary>
    /// <inheritdoc cref="ComputedHorizontalScrollBarVisibility"/>
    public ScrollBarVisibilityMode ComputedVerticalScrollBarVisibility => _computedVerticalScrollBarVisibility;

    /// <summary>
    /// Gets the presenter loaded from the current control template, or <see langword="null"/> before template application.
    /// </summary>
    public ScrollPresenter? ScrollPresenter
    {
        get => _presenter;
        private set => SetAndRaise(ScrollPresenterProperty, ref _presenter, value);
    }

    /// <summary>
    /// Gets whether either template scroll bar is currently expanded.
    /// </summary>
    /// <remarks>
    /// This is a visual state supplied by the active scroll bar theme. Scroll bars retain Avalonia's
    /// auto-collapse behavior, so the state normally becomes <see langword="true"/> while a scroll bar is
    /// expanded for pointer interaction. It does not indicate whether an axis can scroll.
    /// </remarks>
    public bool IsExpanded => _isExpanded;

    /// <summary>
    /// Gets or sets how the horizontal scroll bar is displayed.
    /// </summary>
    /// <remarks>
    /// This changes presentation only. It never enables or disables horizontal input; use
    /// <see cref="HorizontalScrollMode"/> for that purpose.
    /// </remarks>
    public ScrollBarVisibilityMode HorizontalScrollBarVisibility
    {
        get => GetValue(HorizontalScrollBarVisibilityProperty);
        set => SetValue(HorizontalScrollBarVisibilityProperty, value);
    }

    /// <summary>
    /// Gets or sets how the vertical scroll bar is displayed.
    /// </summary>
    /// <remarks>
    /// This changes presentation only. It never enables or disables vertical input; use
    /// <see cref="VerticalScrollMode"/> for that purpose.
    /// </remarks>
    public ScrollBarVisibilityMode VerticalScrollBarVisibility
    {
        get => GetValue(VerticalScrollBarVisibilityProperty);
        set => SetValue(VerticalScrollBarVisibilityProperty, value);
    }

    /// <summary>
    /// Gets or sets whether an unconsumed pan delta may continue to an ancestor scrollable control.
    /// </summary>
    public bool IsScrollChainingEnabled
    {
        get => GetValue(IsScrollChainingEnabledProperty);
        set => SetValue(IsScrollChainingEnabledProperty, value);
    }

    /// <summary>
    /// Gets or sets whether user pan, wheel, and zoom input may continue after release.
    /// </summary>
    /// <remarks>
    /// When <see langword="false"/>, gesture deltas are applied immediately and neither translation nor
    /// scale receives release inertia.
    /// </remarks>
    public bool IsScrollInertiaEnabled
    {
        get => GetValue(IsScrollInertiaEnabledProperty);
        set => SetValue(IsScrollInertiaEnabledProperty, value);
    }

    /// <summary>
    /// Gets or sets whether dragging a scroll bar thumb delays the content update until the thumb is released.
    /// </summary>
    /// <remarks>
    /// This setting affects only template scroll bars. It does not defer wheel, pan, pinch, zoom, or
    /// programmatic changes, and it is independent of <see cref="IsScrollInertiaEnabled"/>.
    /// </remarks>
    public bool IsDeferredScrollingEnabled
    {
        get => GetValue(IsDeferredScrollingEnabledProperty);
        set => SetValue(IsDeferredScrollingEnabledProperty, value);
    }

    /// <summary>
    /// Gets or sets how horizontal gesture inertia uses snap points exposed by the content.
    /// </summary>
    /// <remarks>
    /// The content, or an <see cref="ItemsControl"/>'s panel, must implement
    /// <see cref="IScrollSnapPointsInfo"/> for this setting to have an effect.
    /// </remarks>
    public SnapPointsType HorizontalSnapPointsType
    {
        get => GetValue(HorizontalSnapPointsTypeProperty);
        set => SetValue(HorizontalSnapPointsTypeProperty, value);
    }

    /// <summary>
    /// Gets or sets how vertical gesture inertia uses snap points exposed by the content.
    /// </summary>
    /// <remarks>
    /// The content, or an <see cref="ItemsControl"/>'s panel, must implement
    /// <see cref="IScrollSnapPointsInfo"/> for this setting to have an effect.
    /// </remarks>
    public SnapPointsType VerticalSnapPointsType
    {
        get => GetValue(VerticalSnapPointsTypeProperty);
        set => SetValue(VerticalSnapPointsTypeProperty, value);
    }

    /// <summary>
    /// Gets or sets which horizontal viewport position is aligned with a snap point.
    /// </summary>
    /// <remarks>
    /// <see cref="SnapPointsAlignment.Near"/> uses the left edge, <see cref="SnapPointsAlignment.Center"/>
    /// uses the center, and <see cref="SnapPointsAlignment.Far"/> uses the right edge.
    /// </remarks>
    public SnapPointsAlignment HorizontalSnapPointsAlignment
    {
        get => GetValue(HorizontalSnapPointsAlignmentProperty);
        set => SetValue(HorizontalSnapPointsAlignmentProperty, value);
    }

    /// <summary>
    /// Gets or sets which vertical viewport position is aligned with a snap point.
    /// </summary>
    /// <remarks>
    /// <see cref="SnapPointsAlignment.Near"/> uses the top edge, <see cref="SnapPointsAlignment.Center"/>
    /// uses the center, and <see cref="SnapPointsAlignment.Far"/> uses the bottom edge.
    /// </remarks>
    public SnapPointsAlignment VerticalSnapPointsAlignment
    {
        get => GetValue(VerticalSnapPointsAlignmentProperty);
        set => SetValue(VerticalSnapPointsAlignmentProperty, value);
    }

    /// <summary>
    /// Gets or sets the lowest allowed zoom factor.
    /// </summary>
    /// <remarks>
    /// The value must be finite and greater than zero. Keep it less than or equal to
    /// <see cref="MaxZoomFactor"/>.
    /// </remarks>
    public double MinZoomFactor
    {
        get => GetValue(MinZoomFactorProperty);
        set => SetValue(MinZoomFactorProperty, value);
    }

    /// <summary>
    /// Gets or sets the highest allowed zoom factor.
    /// </summary>
    /// <remarks>
    /// The value must be finite and greater than zero. Keep it greater than or equal to
    /// <see cref="MinZoomFactor"/>.
    /// </remarks>
    public double MaxZoomFactor
    {
        get => GetValue(MaxZoomFactorProperty);
        set => SetValue(MaxZoomFactorProperty, value);
    }

    /// <summary>
    /// Gets or sets the current zoom factor.
    /// </summary>
    /// <remarks>
    /// Setting this property immediately applies a programmatic zoom around the viewport center. The value is
    /// constrained to <see cref="MinZoomFactor"/> and <see cref="MaxZoomFactor"/>. Use
    /// <see cref="ZoomTo(double, bool)"/> to request an animated zoom. A changed value creates a lifecycle
    /// reported through <see cref="ZoomStarting"/>, <see cref="ZoomChanged"/>, and <see cref="ZoomCompleted"/>.
    /// </remarks>
    public double ZoomFactor
    {
        get => _zoomFactor;
        set => SetZoomFactor(value, ScrollChangeSource.Programmatic);
    }

    /// <summary>
    /// Gets or sets whether the configured zoom gestures are accepted.
    /// </summary>
    /// <remarks>
    /// This setting affects user input only; <see cref="ZoomTo(double, bool)"/> remains available for
    /// programmatic zooming.
    /// </remarks>
    public bool IsZoomEnabled
    {
        get => GetValue(IsZoomEnabledProperty);
        set => SetValue(IsZoomEnabledProperty, value);
    }

    /// <summary>
    /// Gets or sets whether the child is measured with an infinite horizontal constraint.
    /// </summary>
    /// <remarks>
    /// Use <see langword="true"/> when the child should retain its natural width. Use
    /// <see langword="false"/> when it should receive the viewport width and can stretch horizontally.
    /// </remarks>
    public bool IsHorizontalMeasureInfinite
    {
        get => GetValue(IsHorizontalMeasureInfiniteProperty);
        set => SetValue(IsHorizontalMeasureInfiniteProperty, value);
    }

    /// <summary>
    /// Gets or sets whether the child is measured with an infinite vertical constraint.
    /// </summary>
    /// <remarks>
    /// Use <see langword="true"/> when the child should retain its natural height. Use
    /// <see langword="false"/> when it should receive the viewport height and can stretch vertically.
    /// </remarks>
    public bool IsVerticalMeasureInfinite
    {
        get => GetValue(IsVerticalMeasureInfiniteProperty);
        set => SetValue(IsVerticalMeasureInfiniteProperty, value);
    }

    /// <summary>
    /// Gets or sets whether horizontal user input is accepted.
    /// </summary>
    /// <remarks>
    /// <see cref="ScrollMode.Auto"/> enables the axis only when content overflows, while
    /// <see cref="ScrollMode.Enabled"/> enables its interaction source unconditionally and
    /// <see cref="ScrollMode.Disabled"/> disables it.
    /// </remarks>
    public ScrollMode HorizontalScrollMode
    {
        get => GetValue(HorizontalScrollModeProperty);
        set => SetValue(HorizontalScrollModeProperty, value);
    }

    /// <summary>
    /// Gets or sets whether vertical user input is accepted.
    /// </summary>
    /// <remarks>
    /// <see cref="ScrollMode.Auto"/> enables the axis only when content overflows, while
    /// <see cref="ScrollMode.Enabled"/> enables its interaction source unconditionally and
    /// <see cref="ScrollMode.Disabled"/> disables it.
    /// </remarks>
    public ScrollMode VerticalScrollMode
    {
        get => GetValue(VerticalScrollModeProperty);
        set => SetValue(VerticalScrollModeProperty, value);
    }

    /// <summary>
    /// Gets or sets the horizontal viewport ratio used for automatic anchor selection.
    /// </summary>
    /// <remarks>
    /// Values are coerced to <c>[0, 1]</c>. Zero represents the left edge, one represents the right edge,
    /// and <see cref="double.NaN"/> disables horizontal anchoring.
    /// </remarks>
    public double HorizontalAnchorRatio
    {
        get => GetValue(HorizontalAnchorRatioProperty);
        set => SetValue(HorizontalAnchorRatioProperty, value);
    }

    /// <summary>
    /// Gets or sets the vertical viewport ratio used for automatic anchor selection.
    /// </summary>
    /// <remarks>
    /// Values are coerced to <c>[0, 1]</c>. Zero represents the top edge, one represents the bottom edge,
    /// and <see cref="double.NaN"/> disables vertical anchoring.
    /// </remarks>
    public double VerticalAnchorRatio
    {
        get => GetValue(VerticalAnchorRatioProperty);
        set => SetValue(VerticalAnchorRatioProperty, value);
    }

    /// <summary>
    /// Gets or sets the unique gesture-and-modifier mappings used for pan, scroll, and zoom input.
    /// </summary>
    /// <remarks>
    /// The default dictionary supports touch pan and pinch, left/middle mouse drag pan, automatic wheel
    /// axis selection, <c>Shift</c> wheel horizontal scrolling, <c>Control</c> wheel zooming, and tilt-wheel
    /// horizontal scrolling. Each <see cref="ScrollGesture"/> key can map to only one action, and the value
    /// cannot be <see langword="null"/>.
    /// </remarks>
    public ScrollGestureBindings GestureBindings
    {
        get;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            SetAndRaise(GestureBindingsProperty, ref field, value);
        }
    } = ScrollGestureBindings.CreateDefault();

    /// <summary>
    /// Gets or sets the multiplier applied to pan and wheel translation input.
    /// </summary>
    /// <remarks>
    /// Values are coerced to finite, non-negative numbers. A value of zero suppresses translation deltas
    /// without changing scroll bar or programmatic scrolling.
    /// </remarks>
    public double ScrollInputMultiplier
    {
        get => GetValue(ScrollInputMultiplierProperty);
        set => SetValue(ScrollInputMultiplierProperty, value);
    }

    /// <summary>
    /// Gets or sets the multiplier applied to pinch, wheel, and drag zoom input.
    /// </summary>
    /// <remarks>
    /// Values are coerced to finite, non-negative numbers. A value of zero suppresses zoom gesture deltas
    /// without changing programmatic zooming.
    /// </remarks>
    public double ZoomInputMultiplier
    {
        get => GetValue(ZoomInputMultiplierProperty);
        set => SetValue(ZoomInputMultiplierProperty, value);
    }

    /// <summary>
    /// Gets or sets the fraction of translation velocity retained by each 60 Hz inertia frame.
    /// </summary>
    /// <remarks>
    /// Values closer to one produce longer pan and wheel inertia. Values are coerced to the inclusive
    /// range <c>[0.01, 0.9999]</c>.
    /// </remarks>
    public double ScrollInertiaDecayRate
    {
        get => GetValue(ScrollInertiaDecayRateProperty);
        set => SetValue(ScrollInertiaDecayRateProperty, value);
    }

    /// <summary>
    /// Gets or sets the fraction of logarithmic scale velocity retained by each 60 Hz inertia frame.
    /// </summary>
    /// <remarks>
    /// Values closer to one produce longer zoom inertia. Values are coerced to the inclusive range
    /// <c>[0.01, 0.9999]</c>.
    /// </remarks>
    public double ZoomInertiaDecayRate
    {
        get => GetValue(ZoomInertiaDecayRateProperty);
        set => SetValue(ZoomInertiaDecayRateProperty, value);
    }

    /// <summary>
    /// Gets or sets the amount of nonlinear resistance applied when user input pulls content beyond a boundary.
    /// </summary>
    /// <remarks>
    /// Elastic overscroll is applied to touch and pen manipulation only. Mouse dragging, wheel input, scroll bars,
    /// and programmatic changes remain clamped to the legal range.
    /// A value of zero clamps at the boundary without overscroll. A value of one allows the largest elastic
    /// displacement. Values are coerced to the inclusive range <c>[0, 1]</c>.
    /// </remarks>
    public double OverscrollElasticity
    {
        get => GetValue(OverscrollElasticityProperty);
        set => SetValue(OverscrollElasticityProperty, value);
    }

    /// <summary>
    /// Gets or sets the speed multiplier for the critically damped return from an overscroll boundary.
    /// </summary>
    /// <remarks>
    /// Higher values return to the legal range faster. This setting has no visible effect when
    /// <see cref="OverscrollElasticity"/> is zero. Values must be finite and greater than zero.
    /// </remarks>
    public double OverscrollBounceRate
    {
        get => GetValue(OverscrollBounceRateProperty);
        set => SetValue(OverscrollBounceRateProperty, value);
    }

    bool IScrollable.CanHorizontallyScroll => ComputedHorizontalScrollMode is ScrollMode.Enabled;

    bool IScrollable.CanVerticallyScroll => ComputedVerticalScrollMode is ScrollMode.Enabled;

    /// <summary>
    /// Gets the anchor candidate selected by the most recently completed layout pass.
    /// </summary>
    /// <remarks>
    /// Reading this property does not start anchor selection. It is <see langword="null"/> before a candidate is
    /// selected and while anchoring is satisfied directly by the near or far extent boundary.
    /// </remarks>
    public Control? CurrentAnchor => (_presenter as IScrollAnchorProvider)?.CurrentAnchor;

    /// <summary>
    /// Scrolls to a logical offset.
    /// </summary>
    /// <param name="offset">The requested zero-based horizontal and vertical offset.</param>
    /// <param name="isAnimated"><see langword="true"/> to animate the transition when a presenter is available.</param>
    /// <returns>
    /// A positive identifier shared by the operation events, or <see cref="NoCorrelationId"/> when the
    /// coerced offset is unchanged or direct manipulation currently owns the tracker.
    /// </returns>
    /// <remarks>
    /// The requested value is constrained by the enabled axes and <see cref="ScrollBarMaximum"/>. Changes
    /// made through this method are reported as <see cref="ScrollChangeSource.Programmatic"/>.
    /// </remarks>
    public int ScrollTo(Vector offset, bool isAnimated = true)
    {
        offset = ClampOffsetToEnabledAxes(offset);
        if (_presenter is { } presenter)
            return presenter.ScrollTo(offset, isAnimated, ScrollChangeSource.Programmatic);

        return SetOffset(offset, ScrollChangeSource.Programmatic);
    }

    /// <summary>
    /// Scrolls by a relative logical offset.
    /// </summary>
    /// <param name="offsetDelta">The horizontal and vertical offset increments.</param>
    /// <param name="isAnimated"><see langword="true"/> to animate the transition when a presenter is available.</param>
    /// <returns>
    /// A positive identifier shared by the operation events, or <see cref="NoCorrelationId"/> when no
    /// operation is created.
    /// </returns>
    /// <remarks>
    /// The final offset is constrained by the enabled axes and <see cref="ScrollBarMaximum"/>. Changes made
    /// through this method are reported as <see cref="ScrollChangeSource.Programmatic"/>.
    /// </remarks>
    public int ScrollBy(Vector offsetDelta, bool isAnimated = true) =>
        ScrollTo(Offset + offsetDelta, isAnimated);

    /// <summary>
    /// Changes the zoom to an absolute factor.
    /// </summary>
    /// <param name="zoomFactor">The requested scale, constrained to <see cref="MinZoomFactor"/> and <see cref="MaxZoomFactor"/>.</param>
    /// <param name="isAnimated"><see langword="true"/> to animate the transition.</param>
    /// <returns>
    /// A positive identifier shared by the operation events, or <see cref="NoCorrelationId"/> when no
    /// operation is created.
    /// </returns>
    /// <remarks>
    /// This method is programmatic and can be used even when <see cref="IsZoomEnabled"/> is
    /// <see langword="false"/>. Before the control template is applied, the requested factor is retained
    /// and applied without animation when a presenter becomes available.
    /// </remarks>
    public int ZoomTo(double zoomFactor, bool isAnimated = true) =>
        ZoomTo(zoomFactor, centerPoint: null, isAnimated: isAnimated);

    /// <summary>
    /// Changes the zoom to an absolute factor around a viewport-relative point.
    /// </summary>
    /// <param name="zoomFactor">The requested scale, constrained to <see cref="MinZoomFactor"/> and <see cref="MaxZoomFactor"/>.</param>
    /// <param name="centerPoint">
    /// The viewport-relative point that remains visually stationary, or <see langword="null"/> to use the viewport center.
    /// </param>
    /// <param name="isAnimated"><see langword="true"/> to animate the transition.</param>
    /// <returns>
    /// A positive identifier shared by the operation events, or <see cref="NoCorrelationId"/> when no
    /// operation is created.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="centerPoint"/> contains a non-finite coordinate.
    /// </exception>
    /// <remarks>
    /// This method is programmatic and can be used even when <see cref="IsZoomEnabled"/> is
    /// <see langword="false"/>. Before the control template is applied, the requested factor is retained;
    /// the center point cannot affect the offset until a presenter is available.
    /// </remarks>
    public int ZoomTo(double zoomFactor, Point? centerPoint, bool isAnimated = true)
    {
        ValidateZoomCenter(centerPoint);

        if (_presenter is null)
            return SetZoomFactor(zoomFactor, ScrollChangeSource.Programmatic);

        return _presenter.ZoomTo(
            zoomFactor,
            centerPoint,
            isAnimated,
            source: ScrollChangeSource.Programmatic);
    }

    /// <summary>
    /// Changes the zoom by an additive factor.
    /// </summary>
    /// <param name="zoomFactorDelta">The amount added to the current <see cref="ZoomFactor"/>.</param>
    /// <param name="isAnimated"><see langword="true"/> to animate the transition.</param>
    /// <returns>
    /// A positive identifier shared by the operation events, or <see cref="NoCorrelationId"/> when no
    /// operation is created.
    /// </returns>
    /// <inheritdoc cref="ZoomTo(double, bool)"/>
    public int ZoomBy(double zoomFactorDelta, bool isAnimated = true) =>
        ZoomBy(zoomFactorDelta, centerPoint: null, isAnimated: isAnimated);

    /// <summary>
    /// Changes the zoom by an additive factor around a viewport-relative point.
    /// </summary>
    /// <param name="zoomFactorDelta">The amount added to the current <see cref="ZoomFactor"/>.</param>
    /// <param name="centerPoint">
    /// The viewport-relative point that remains visually stationary, or <see langword="null"/> to use the viewport center.
    /// </param>
    /// <param name="isAnimated"><see langword="true"/> to animate the transition.</param>
    /// <returns>
    /// A positive identifier shared by the operation events, or <see cref="NoCorrelationId"/> when no
    /// operation is created.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="centerPoint"/> contains a non-finite coordinate.
    /// </exception>
    /// <inheritdoc cref="ZoomTo(double, Point?, bool)"/>
    public int ZoomBy(double zoomFactorDelta, Point? centerPoint, bool isAnimated = true)
    {
        ValidateZoomCenter(centerPoint);

        if (_presenter is null)
            return SetZoomFactor(ZoomFactor + zoomFactorDelta, ScrollChangeSource.Programmatic);

        return _presenter.ZoomBy(zoomFactorDelta, centerPoint, isAnimated);
    }

    /// <summary>
    /// Registers a descendant control as a candidate to keep visually stable during layout changes.
    /// </summary>
    /// <param name="element">A control in this <see cref="ScrollView"/>'s visual descendant tree.</param>
    /// <remarks>
    /// Registration is forwarded to the active presenter. It has no effect before the control template is applied.
    /// </remarks>
    public void RegisterAnchorCandidate(Control element) =>
        (_presenter as IScrollAnchorProvider)?.RegisterAnchorCandidate(element);

    /// <summary>
    /// Removes a previously registered layout anchor candidate.
    /// </summary>
    /// <param name="element">The candidate control to remove.</param>
    public void UnregisterAnchorCandidate(Control element) =>
        (_presenter as IScrollAnchorProvider)?.UnregisterAnchorCandidate(element);

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        DetachTemplateParts();
        base.OnApplyTemplate(e);

        ScrollPresenter = e.NameScope.Find<ScrollPresenter>("PART_ContentPresenter");
        _horizontalScrollBar = e.NameScope.Find<ScrollBar>("PART_HorizontalScrollBar");
        _verticalScrollBar = e.NameScope.Find<ScrollBar>("PART_VerticalScrollBar");

        if (_presenter is not null)
            _presenter.AttachToScrollView(this);

        if (_horizontalScrollBar is not null)
        {
            _horizontalScrollBar.Scroll += HorizontalScrollBarOnScroll;
            _horizontalScrollBar.PropertyChanged += ScrollBarOnPropertyChanged;
        }

        if (_verticalScrollBar is not null)
        {
            _verticalScrollBar.Scroll += VerticalScrollBarOnScroll;
            _verticalScrollBar.PropertyChanged += ScrollBarOnPropertyChanged;
        }

        UpdateScrollBars();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        switch (e.Key)
        {
            case Key.PageUp:
                ScrollByUserInput(new Vector(0, -Viewport.Height));
                e.Handled = true;
                break;
            case Key.PageDown:
                ScrollByUserInput(new Vector(0, Viewport.Height));
                e.Handled = true;
                break;
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == OffsetProperty)
        {
            UpdateCalculatedProperties();
            UpdateScrollBars();

            if (!_updatingFromPresenter)
            {
                var context = _offsetChangeContext;
                _offsetChangeContext = null;
                var source = context?.Source ?? ScrollChangeSource.Programmatic;
                var correlationId = _presenter?.SetOffsetFromOwner(
                    change.GetOldValue<Vector>(),
                    change.GetNewValue<Vector>(),
                    source)
                    ?? BeginScrollOperation(
                        change.GetOldValue<Vector>(),
                        change.GetNewValue<Vector>(),
                        isAnimated: false,
                        source);
                if (context is not null)
                    context.CorrelationId = correlationId;

                RaiseScrollChanged(source);
                if (_presenter is null)
                    CompleteScrollOperation(correlationId, ScrollingOperationResult.Completed);
            }
        }
        else if (change.Property == ZoomFactorProperty)
        {
            if (!_updatingFromPresenter)
            {
                var context = _zoomChangeContext;
                _zoomChangeContext = null;
                var source = context?.Source ?? ScrollChangeSource.Programmatic;
                var correlationId = _presenter?.ZoomTo(
                    change.GetNewValue<double>(),
                    centerPoint: null,
                    isAnimated: false,
                    source)
                    ?? BeginZoomOperation(
                        change.GetOldValue<double>(),
                        change.GetNewValue<double>(),
                        centerPoint: null,
                        isAnimated: false,
                        source);
                if (context is not null)
                    context.CorrelationId = correlationId;

                RaiseZoomChanged(source);
                if (_presenter is null)
                    CompleteZoomOperation(correlationId, ScrollingOperationResult.Completed);
            }
        }
        else if (change.Property == GestureBindingsProperty || IsInteractionConfigurationProperty(change.Property))
        {
            _presenter?.UpdateOwnerConfiguration(this);
            if (IsScrollModeProperty(change.Property))
            {
                UpdateCalculatedProperties();
                UpdateScrollBars();
            }
        }
        else if (IsScrollBarProperty(change.Property))
        {
            UpdateScrollBars();
        }
    }

    internal void UpdateFromPresenter(
        Size extent,
        Size viewport,
        Vector offset,
        double zoomFactor,
        ScrollChangeSource source)
        => UpdateFromPresenter(extent, extent, viewport, offset, zoomFactor, source);

    internal void UpdateFromPresenter(
        Size extent,
        Size logicalExtent,
        Size viewport,
        Vector offset,
        double zoomFactor,
        ScrollChangeSource source)
    {
        try
        {
            _updatingFromPresenter = true;
            SetAndRaise(ExtentProperty, ref _extent, extent);
            SetAndRaise(LogicalExtentProperty, ref _logicalExtent, logicalExtent);
            SetAndRaise(ViewportProperty, ref _viewport, viewport);
            UpdateCalculatedProperties();
            SetCurrentValue(OffsetProperty, offset);
            SetAndRaise(ZoomFactorProperty, ref _zoomFactor, zoomFactor);
        }
        finally
        {
            _updatingFromPresenter = false;
        }

        UpdateScrollBars();
        RaiseScrollChanged(source);
        RaiseZoomChanged(source);
    }

    internal ScrollingAnchorRequestedEventArgs? RaiseAnchorRequested(IEnumerable<Control> candidates)
    {
        var handler = AnchorRequested;
        if (handler is null)
            return null;

        var args = new ScrollingAnchorRequestedEventArgs(candidates);
        handler(this, args);
        return args;
    }

    private void ScrollByUserInput(Vector offsetDelta) =>
        SetOffset(Offset + offsetDelta, ScrollChangeSource.User);

    private int SetOffset(Vector offset, ScrollChangeSource source)
    {
        offset = CoerceOffset(this, offset);
        if (Offset.NearlyEquals(offset) || State is ScrollingInteractionState.Interaction)
            return NoCorrelationId;

        var context = new OperationRequestContext(source);
        var previousContext = _offsetChangeContext;
        try
        {
            _offsetChangeContext = context;
            SetCurrentValue(OffsetProperty, offset);
            return context.CorrelationId;
        }
        finally
        {
            _offsetChangeContext = previousContext;
        }
    }

    private int SetZoomFactor(double zoomFactor, ScrollChangeSource source)
    {
        zoomFactor = CoerceZoomFactor(zoomFactor);
        if (MathUtilities.AreClose(ZoomFactor, zoomFactor)
            || State is ScrollingInteractionState.Interaction)
            return NoCorrelationId;

        var context = new OperationRequestContext(source);
        var previousContext = _zoomChangeContext;
        try
        {
            _zoomChangeContext = context;
            SetAndRaise(ZoomFactorProperty, ref _zoomFactor, zoomFactor);
            return context.CorrelationId;
        }
        finally
        {
            _zoomChangeContext = previousContext;
        }
    }

    private void UpdateCalculatedProperties()
    {
        var horizontalOverflow = Math.Max(Extent.Width - Viewport.Width, 0);
        var verticalOverflow = Math.Max(Extent.Height - Viewport.Height, 0);
        var horizontalMode = ResolveComputedScrollMode(HorizontalScrollMode, horizontalOverflow > 0);
        var verticalMode = ResolveComputedScrollMode(VerticalScrollMode, verticalOverflow > 0);
        var maximum = new Vector(
            horizontalMode is ScrollMode.Enabled ? horizontalOverflow : 0,
            verticalMode is ScrollMode.Enabled ? verticalOverflow : 0);

        SetAndRaise(ComputedHorizontalScrollModeProperty, ref _computedHorizontalScrollMode, horizontalMode);
        SetAndRaise(ComputedVerticalScrollModeProperty, ref _computedVerticalScrollMode, verticalMode);
        SetAndRaise(ScrollBarMaximumProperty, ref _scrollBarMaximum, maximum);
        CoerceValue(OffsetProperty);
    }

    private void UpdateScrollBars()
    {
        var horizontalVisibility = ResolveComputedScrollBarVisibility(
            HorizontalScrollBarVisibility,
            ComputedHorizontalScrollMode is ScrollMode.Enabled);
        var verticalVisibility = ResolveComputedScrollBarVisibility(
            VerticalScrollBarVisibility,
            ComputedVerticalScrollMode is ScrollMode.Enabled);
        SetAndRaise(
            ComputedHorizontalScrollBarVisibilityProperty,
            ref _computedHorizontalScrollBarVisibility,
            horizontalVisibility);
        SetAndRaise(
            ComputedVerticalScrollBarVisibilityProperty,
            ref _computedVerticalScrollBarVisibility,
            verticalVisibility);
        UpdateScrollBar(_horizontalScrollBar, Orientation.Horizontal);
        UpdateScrollBar(_verticalScrollBar, Orientation.Vertical);
        UpdateExpandedState();
    }

    private void UpdateScrollBar(ScrollBar? scrollBar, Orientation orientation)
    {
        if (scrollBar is null)
            return;

        var horizontal = orientation is Orientation.Horizontal;
        var visibilityMode = horizontal ? ComputedHorizontalScrollBarVisibility : ComputedVerticalScrollBarVisibility;
        scrollBar.Maximum = horizontal ? ScrollBarMaximum.X : ScrollBarMaximum.Y;
        scrollBar.ViewportSize = horizontal ? Viewport.Width : Viewport.Height;
        scrollBar.Value = horizontal ? Offset.X : Offset.Y;
        scrollBar.Visibility = visibilityMode is ScrollBarVisibilityMode.Visible ? ScrollBarVisibility.Visible : ScrollBarVisibility.Hidden;
        scrollBar.AllowAutoHide = true;
        scrollBar.LargeChange = horizontal ? Viewport.Width : Viewport.Height;
        scrollBar.SmallChange = ScrollBarSmallChange;
        ScrollViewer.SetIsDeferredScrollingEnabled(scrollBar, IsDeferredScrollingEnabled);
    }

    private void HorizontalScrollBarOnScroll(object? sender, ScrollEventArgs e) =>
        SetOffset(Offset.WithX(e.NewValue), ScrollChangeSource.User);

    private void VerticalScrollBarOnScroll(object? sender, ScrollEventArgs e) =>
        SetOffset(Offset.WithY(e.NewValue), ScrollChangeSource.User);

    private void ScrollBarOnPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == ScrollBar.IsExpandedProperty)
            UpdateExpandedState();
    }

    private void UpdateExpandedState()
    {
        var expanded = _horizontalScrollBar?.IsExpanded is true || _verticalScrollBar?.IsExpanded is true;
        SetAndRaise(IsExpandedProperty, ref _isExpanded, expanded);
    }

    private void DetachTemplateParts()
    {
        if (_presenter is not null)
            _presenter.DetachFromScrollView(this);

        if (_horizontalScrollBar is not null)
        {
            _horizontalScrollBar.Scroll -= HorizontalScrollBarOnScroll;
            _horizontalScrollBar.PropertyChanged -= ScrollBarOnPropertyChanged;
        }

        if (_verticalScrollBar is not null)
        {
            _verticalScrollBar.Scroll -= VerticalScrollBarOnScroll;
            _verticalScrollBar.PropertyChanged -= ScrollBarOnPropertyChanged;
        }

        ScrollPresenter = null;
        _horizontalScrollBar = null;
        _verticalScrollBar = null;
    }

    private void RaiseScrollChanged(ScrollChangeSource source)
    {
        var extentDelta = new Vector(Extent.Width - _notifiedExtent.Width, Extent.Height - _notifiedExtent.Height);
        var offsetDelta = Offset - _notifiedOffset;
        var viewportDelta = new Vector(Viewport.Width - _notifiedViewport.Width, Viewport.Height - _notifiedViewport.Height);

        if (extentDelta.NearlyEquals(default) && offsetDelta.NearlyEquals(default) && viewportDelta.NearlyEquals(default))
            return;

        var args = new ScrollViewChangedEventArgs(extentDelta, offsetDelta, viewportDelta, source) { RoutedEvent = ScrollChangedEvent };
        _notifiedExtent = Extent;
        _notifiedOffset = Offset;
        _notifiedViewport = Viewport;
        RaiseEvent(args);
    }

    private void RaiseZoomChanged(ScrollChangeSource source)
    {
        var zoomFactorDelta = ZoomFactor - _notifiedZoomFactor;
        if (MathUtilities.AreClose(zoomFactorDelta, 0))
            return;

        var args = new ZoomChangedEventArgs(zoomFactorDelta, source) { RoutedEvent = ZoomChangedEvent };
        _notifiedZoomFactor = ZoomFactor;
        RaiseEvent(args);
    }

    private static bool IsInteractionConfigurationProperty(AvaloniaProperty property) =>
        property == MinZoomFactorProperty
        || property == MaxZoomFactorProperty
        || property == IsZoomEnabledProperty
        || property == HorizontalScrollModeProperty
        || property == VerticalScrollModeProperty
        || property == HorizontalAnchorRatioProperty
        || property == VerticalAnchorRatioProperty
        || property == IsHorizontalMeasureInfiniteProperty
        || property == IsVerticalMeasureInfiniteProperty
        || property == IsScrollChainingEnabledProperty
        || property == IsScrollInertiaEnabledProperty
        || property == ScrollInputMultiplierProperty
        || property == ZoomInputMultiplierProperty
        || property == ScrollInertiaDecayRateProperty
        || property == ZoomInertiaDecayRateProperty
        || property == OverscrollElasticityProperty
        || property == OverscrollBounceRateProperty;

    private static bool IsScrollBarProperty(AvaloniaProperty property) =>
        property == HorizontalScrollBarVisibilityProperty
        || property == VerticalScrollBarVisibilityProperty
        || property == IsDeferredScrollingEnabledProperty;

    private static bool IsScrollModeProperty(AvaloniaProperty property) =>
        property == HorizontalScrollModeProperty || property == VerticalScrollModeProperty;

    private Vector ClampOffsetToEnabledAxes(Vector offset) => new(
        CanScroll(HorizontalScrollMode, Extent.Width > Viewport.Width) ? offset.X : 0,
        CanScroll(VerticalScrollMode, Extent.Height > Viewport.Height) ? offset.Y : 0);

    internal static bool CanScroll(ScrollMode mode, bool isOverflowing) =>
        ResolveComputedScrollMode(mode, isOverflowing) is ScrollMode.Enabled;

    internal static ScrollMode ResolveComputedScrollMode(ScrollMode mode, bool isOverflowing) => mode switch
    {
        ScrollMode.Auto => isOverflowing ? ScrollMode.Enabled : ScrollMode.Disabled,
        ScrollMode.Enabled => ScrollMode.Enabled,
        ScrollMode.Disabled => ScrollMode.Disabled,
        _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, null)
    };

    private static ScrollBarVisibilityMode ResolveComputedScrollBarVisibility(
        ScrollBarVisibilityMode mode,
        bool canScroll) => mode switch
    {
        ScrollBarVisibilityMode.Auto => canScroll ? ScrollBarVisibilityMode.Visible : ScrollBarVisibilityMode.Hidden,
        ScrollBarVisibilityMode.Visible => ScrollBarVisibilityMode.Visible,
        ScrollBarVisibilityMode.Hidden => ScrollBarVisibilityMode.Hidden,
        _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, null)
    };

    private static Vector CoerceOffset(AvaloniaObject sender, Vector value)
    {
        var view = (ScrollView)sender;
        if (view.Viewport == default || view.Extent == default)
            return new Vector(CoerceFinite(value.X), CoerceFinite(value.Y));

        return new Vector(
            Math.Clamp(CoerceFinite(value.X), 0, view.ScrollBarMaximum.X),
            Math.Clamp(CoerceFinite(value.Y), 0, view.ScrollBarMaximum.Y));
    }

    private static double CoerceFinite(double value) => double.IsFinite(value) ? Math.Max(value, 0) : 0;

    private static double CoerceNonNegative(AvaloniaObject sender, double value) =>
        double.IsFinite(value) ? Math.Max(value, 0) : 0;

    private static double CoerceAnchorRatio(AvaloniaObject sender, double value) =>
        double.IsNaN(value) ? value : Math.Clamp(value, 0, 1);

    private static void ValidateZoomCenter(Point? centerPoint)
    {
        if (centerPoint is { } point && (!double.IsFinite(point.X) || !double.IsFinite(point.Y)))
            throw new ArgumentOutOfRangeException(nameof(centerPoint), centerPoint, "The zoom center must be finite.");
    }

    private static double CoercePositive(AvaloniaObject sender, double value) =>
        double.IsFinite(value) && value > 0 ? value : 1;

    private double CoerceZoomFactor(double value)
    {
        var minimum = Math.Min(MinZoomFactor, MaxZoomFactor);
        var maximum = Math.Max(MinZoomFactor, MaxZoomFactor);
        var finitePositiveValue = double.IsFinite(value) && value > 0 ? value : 1;
        return Math.Clamp(finitePositiveValue, minimum, maximum);
    }

    private static double CoerceDecayRate(AvaloniaObject sender, double value) =>
        double.IsFinite(value) ? Math.Clamp(value, 0.01, 0.9999) : ScrollPhysicsDefaults.LegacyWheelAndZoomInertiaDecayRate;

    private static double CoerceUnitInterval(AvaloniaObject sender, double value) =>
        double.IsFinite(value) ? Math.Clamp(value, 0, 1) : 0.5;
}

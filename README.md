# SmoothScroll.Avalonia

[![NuGet Version](https://img.shields.io/nuget/vpre/SmoothScroll.Avalonia)](https://www.nuget.org/packages/SmoothScroll.Avalonia)
[![NuGet Downloads](https://img.shields.io/nuget/dt/SmoothScroll.Avalonia)](https://www.nuget.org/packages/SmoothScroll.Avalonia)

🌏: [Chinese](.github/README.zh.md)

SmoothScroll.Avalonia provides composition-driven smooth scrolling for Avalonia and a standalone
`ScrollView` control. Its interaction model is inspired by WinUI's `InteractionTracker` and
`ScrollView`.

## Features

- Smooth scrolling for both `ScrollViewer` and `ScrollView`
- Scroll chaining between nested controls
- Mouse, pen, touch, and multi-touch input
- Panning and zooming with concurrent inertia
- Nonlinear elastic overscroll and boundary return
- Configurable input multipliers, inertia decay, bounce speed, and gesture mappings
- Separate user, programmatic, and layout change sources
- Operation lifecycle events with correlation identifiers
- Snap points and element anchoring

SmoothScroll.Avalonia can enhance Avalonia's native `ScrollViewer` through a theme, or provide the
more capable standalone `ScrollView` when an application needs panning, zooming, custom gestures,
or detailed operation tracking.

https://github.com/user-attachments/assets/927a8c80-ac2b-4d50-b86b-8b2fe853ce5d

## ScrollViewer

If an application already uses `ScrollViewer`, `ListBox`, or another control backed by a
`ScrollViewer`, add `ScrollViewerSmoothTheme` to the application styles:

```xml
<Application
    xmlns="https://github.com/avaloniaui"
    xmlns:smoothScroll="using:SmoothScroll.Avalonia.Controls">
    <Application.Styles>
        <!-- Other application styles -->
        <smoothScroll:ScrollViewerSmoothTheme />
    </Application.Styles>
</Application>
```

The theme replaces the internal presenter with the smooth presenter while preserving the public
`ScrollViewer` contract. Use the standalone `ScrollView` for independent axis modes, panning,
zooming, gesture remapping, and operation lifecycle events.

## ScrollView

Add `ScrollViewDefaultTheme` to the application styles before using `ScrollView`:

```xml
<Application
    xmlns="https://github.com/avaloniaui"
    xmlns:smoothScroll="using:SmoothScroll.Avalonia.Controls">
    <Application.Styles>
        <!-- Other application styles -->
        <smoothScroll:ScrollViewDefaultTheme />
    </Application.Styles>
</Application>
```

A minimal image viewer can then be declared as follows:

```xml
<smoothScroll:ScrollView
    HorizontalContentAlignment="Center"
    VerticalContentAlignment="Center"
    HorizontalScrollBarVisibility="Hidden"
    VerticalScrollBarVisibility="Hidden"
    IsZoomEnabled="True">
    <Image Source="avares://MyApp/Assets/image.jpg" />
</smoothScroll:ScrollView>
```

### Coordinate model

| API                                                   | Meaning                                                                                         |
| ----------------------------------------------------- | ----------------------------------------------------------------------------------------------- |
| `Size Extent { get; }`                                | Content size in the Avalonia `IScrollable` coordinate space, including the current `ZoomFactor` |
| `Size LogicalExtent { get; }`                         | Content size before zoom; independent of `ZoomFactor`                                           |
| `Size Viewport { get; }`                              | Size of the visible content area                                                                |
| `Vector Offset { get; set; }`                         | Current zero-based scroll position                                                              |
| `Vector ScrollBarMaximum { get; }`                    | Largest legal offset on each enabled axis                                                       |
| `bool IsExpanded { get; }`                            | Whether either themed scroll bar is currently in its expanded visual state                      |
| `ScrollPresenter? ScrollPresenter { get; }`           | Presenter from the current template, or `null` before template application                      |
| `int ScrollTo(Vector offset, bool isAnimated = true)` | Scroll to an absolute offset and return an operation identifier                                 |
| `int ScrollBy(Vector delta, bool isAnimated = true)`  | Scroll relative to the current offset and return an operation identifier                        |

`Extent`, `Viewport`, and `Offset` intentionally share the Avalonia `IScrollable` coordinate space.
Changing `ZoomFactor` therefore changes `Extent`. Use `LogicalExtent` when content dimensions must
remain stable while zooming. This distinction preserves compatibility with Avalonia consumers such
as virtualizing panels.

Each `Offset` component is coerced to the finite range from zero through the matching
`ScrollBarMaximum` component. Assigning `Offset` applies an immediate programmatic change and uses
the same operation lifecycle as `ScrollTo(offset, false)`.

`Offset` remains zero-based when content is smaller than the viewport and aligned to the center,
right, or bottom. Negative tracker positions used internally for visual alignment are never exposed.

### Programmatic navigation

`ScrollTo`, `ScrollBy`, `ZoomTo`, and `ZoomBy` return a positive correlation identifier when a new
operation is accepted. They return `ScrollView.NoCorrelationId` (`-1`) when coercion produces no
change, direct manipulation currently owns the tracker, or no operation is created.

```csharp
var scrollId = scrollView.ScrollBy(new Vector(120, 0), isAnimated: true);
var zoomId = scrollView.ZoomTo(2, new Point(100, 80), isAnimated: true);
```

`ScrollBy` is additive relative to `Offset`. `ZoomBy` is also additive: passing `0.25` changes a
factor of `1.5` to `1.75`, rather than multiplying it.

The control intentionally does not expose `LineUp`, `LineDown`, `PageUp`, or `PageDown` methods.
Use `ScrollBy` with an application-defined line or page distance. Keyboard Page Up and Page Down
remain supported internally and move by one `Viewport.Height`, reported as user input.

### Bringing content into view

Avalonia's parameterless `BringIntoView()` performs an immediate minimal-distance scroll. Importing
`SmoothScroll.Avalonia.Controls` adds animation-aware overloads:

| API                                                    | Meaning                                                  |
| ------------------------------------------------------ | -------------------------------------------------------- |
| `void BringIntoView(bool isAnimated)`                  | Bring the entire control to the nearest visible position |
| `void BringIntoView(Rect targetRect, bool isAnimated)` | Bring a rectangle in the control's coordinates into view |

```csharp
using SmoothScroll.Avalonia.Controls;

target.BringIntoView(isAnimated: true);
target.BringIntoView(targetRect, isAnimated: true);
```

These overloads work with both `ScrollView` and an Avalonia `ScrollViewer` using
`ScrollViewerSmoothTheme`. A request handled by `ScrollView` is reported as programmatic. Passing
`false` retains Avalonia's immediate minimal-scroll behavior.

### Zooming

| API                                                                     | Meaning                                                                              |
| ----------------------------------------------------------------------- | ------------------------------------------------------------------------------------ |
| `bool IsZoomEnabled { get; set; } = false`                              | Accept configured user zoom gestures; programmatic zoom remains available when false |
| `double MinZoomFactor { get; set; } = 0.1`                              | Lowest finite positive zoom factor                                                   |
| `double MaxZoomFactor { get; set; } = 10`                               | Highest finite positive zoom factor                                                  |
| `double ZoomFactor { get; set; } = 1`                                   | Current zoom factor; assignment is immediate                                         |
| `int ZoomTo(double factor, bool isAnimated = true)`                     | Zoom to an absolute factor around the viewport center                                |
| `int ZoomTo(double factor, Point? centerPoint, bool isAnimated = true)` | Zoom around a viewport-relative point                                                |
| `int ZoomBy(double delta, bool isAnimated = true)`                      | Add a delta to the current factor around the viewport center                         |
| `int ZoomBy(double delta, Point? centerPoint, bool isAnimated = true)`  | Add a delta around a viewport-relative point                                         |

Keep `0 < MinZoomFactor <= MaxZoomFactor`. User and programmatic zoom requests are both constrained
to this range.

Assigning `ZoomFactor` applies an immediate programmatic zoom around the viewport center. The
`centerPoint` overloads use viewport coordinates, not content coordinates. `null` means the viewport
center. A supplied point remains visually stationary during the zoom; both coordinates must be
finite, but the point does not need to lie inside the viewport.

Before template application, a zoom request retains its final factor and applies it immediately once
a presenter becomes available. Because no viewport exists at request time, a supplied center point
cannot later affect `Offset`.

## Changes and operation lifecycle

`ScrollChanged` and `ZoomChanged` report value changes. The starting/completed events report the
lifetime and outcome of each accepted operation.

### Value-change events

`ScrollChanged` bubbles when `Extent`, `Viewport`, or `Offset` changes. `ZoomChanged` bubbles when
`ZoomFactor` changes.

| `ScrollChangeSource` | Cause                                                                          |
| -------------------- | ------------------------------------------------------------------------------ |
| `User`               | Mouse, touch, pen, wheel, scroll-bar, or keyboard input handled by the control |
| `Programmatic`       | Property assignment, navigation method, or bring-into-view request             |
| `Layout`             | A size, content, anchor, or other layout recalculation                         |

`IsUserInitiated` is equivalent to `ChangeSource == ScrollChangeSource.User`.

| Event argument                             | Meaning                                 |
| ------------------------------------------ | --------------------------------------- |
| `ScrollViewChangedEventArgs.ExtentDelta`   | Change in `Extent`                      |
| `ScrollViewChangedEventArgs.OffsetDelta`   | Change in `Offset`                      |
| `ScrollViewChangedEventArgs.ViewportDelta` | Change in `Viewport`                    |
| `ZoomChangedEventArgs.ZoomFactorDelta`     | Change in `ZoomFactor`                  |
| `ChangeSource`                             | Source of the reported change           |
| `IsUserInitiated`                          | Whether the source is direct user input |

```csharp
private void OnScrollChanged(object? sender, ScrollViewChangedEventArgs e)
{
    if (!e.IsUserInitiated)
        return;

    // React only to direct user navigation.
}
```

### Starting and completed events

| API                                        | Meaning                                                       |
| ------------------------------------------ | ------------------------------------------------------------- |
| `ScrollStarting`                           | A scroll operation was accepted                               |
| `ScrollCompleted`                          | A scroll operation completed, was interrupted, or was ignored |
| `ZoomStarting`                             | A zoom operation was accepted                                 |
| `ZoomCompleted`                            | A zoom operation completed, was interrupted, or was ignored   |
| `StateChanged`                             | `State` changed                                               |
| `ScrollingInteractionState State { get; }` | Current manipulation, inertia, or animation state             |

Navigation methods return the same `CorrelationId` supplied by the matching starting and completed
events. Property assignments cannot return an identifier, so their identifier is available only
through the event arguments. Immediate operations still emit both starting and completed events.

Starting arguments contain the initial value, requested target, animation flag, and change source.
Completion arguments contain the final value and a `ScrollingOperationResult`:

| Result        | Meaning                                                                    |
| ------------- | -------------------------------------------------------------------------- |
| `Completed`   | The requested or natural resting value was reached                         |
| `Interrupted` | Another request, direct manipulation, or teardown superseded the operation |
| `Ignored`     | The interaction tracker rejected the operation before applying it          |

Starting a new scroll operation interrupts the previous scroll operation; zoom operations follow the
same rule independently. Translation and zoom may remain active at the same time and have different
correlation identifiers.

`State` has the following values:

| State         | Meaning                                                       |
| ------------- | ------------------------------------------------------------- |
| `Idle`        | No manipulation, inertia, or programmatic animation is active |
| `Interaction` | Pointer or touch input is directly manipulating the view      |
| `Inertia`     | Translation, zoom, or boundary-return inertia is active       |
| `Animation`   | A programmatic scroll or zoom animation is active             |

```csharp
var id = scrollView.ScrollTo(new Vector(0, 800), isAnimated: true);

scrollView.ScrollCompleted += (_, e) =>
{
    if (e.CorrelationId == id && e.Result == ScrollingOperationResult.Completed)
    {
        // The requested navigation reached its final offset.
    }
};
```

## Axis, scroll-bar, and measurement configuration

Scrolling capability, scroll-bar presentation, and child measurement are independent settings on
each axis.

| API                                                                            | Meaning                                                      |
| ------------------------------------------------------------------------------ | ------------------------------------------------------------ |
| `HorizontalScrollMode`, `VerticalScrollMode`                                   | User-input mode for each axis                                |
| `HorizontalScrollBarVisibility`, `VerticalScrollBarVisibility`                 | Scroll-bar presentation only                                 |
| `ComputedHorizontalScrollMode`, `ComputedVerticalScrollMode`                   | Resolved mode: `Enabled` or `Disabled`                       |
| `ComputedHorizontalScrollBarVisibility`, `ComputedVerticalScrollBarVisibility` | Resolved visibility: `Visible` or `Hidden`                   |
| `IsHorizontalMeasureInfinite = false`                                          | Whether the child receives infinite horizontal measure space |
| `IsVerticalMeasureInfinite = true`                                             | Whether the child receives infinite vertical measure space   |

### Scroll modes

| `ScrollMode` | Behavior                                                                                   |
| ------------ | ------------------------------------------------------------------------------------------ |
| `Auto`       | Enable the axis only when zoomed content overflows the viewport                            |
| `Enabled`    | Always enable the axis interaction source; the legal offset is still zero without overflow |
| `Disabled`   | Always disable user input on the axis and coerce programmatic offset to zero               |

When an axis is `Enabled` without overflow, touch or pen manipulation can still pull elastically
against its boundary when overscroll is enabled.

### Scroll-bar visibility

| `ScrollBarVisibilityMode` | Behavior                                            |
| ------------------------- | --------------------------------------------------- |
| `Auto`                    | Show the bar when the resolved axis mode is enabled |
| `Visible`                 | Always show the bar                                 |
| `Hidden`                  | Always hide the bar                                 |

Visibility does not enable or disable scrolling. With `Auto`, an explicitly `Enabled` axis shows its
bar even when content does not overflow.

Visible bars retain Avalonia's `AllowAutoHide` behavior. Themes such as Fluent can collapse an idle
bar and expand it for pointer interaction. This compact visual state is exposed through `IsExpanded`;
it does not change the bar's `Visibility`, computed visibility, or scrolling capability.

### Measurement constraints

`IsHorizontalMeasureInfinite` and `IsVerticalMeasureInfinite` control the constraints supplied to the
child during `Measure`. They do not control whether an axis can scroll.

| Property value | Infinite axis                                  | Finite axis                                                            |
| -------------- | ---------------------------------------------- | ---------------------------------------------------------------------- |
| `true`         | Preserve the child's natural size more readily | N/A                                                                    |
| `false`        | N/A                                            | Supply the viewport dimension so stretch-oriented children can fill it |

Typical combinations are:

| Content                                             | Horizontal infinite | Vertical infinite |
| --------------------------------------------------- | ------------------- | ----------------- |
| Vertical `StackPanel`                               | `false`             | `true`            |
| Horizontal `StackPanel`                             | `true`              | `false`           |
| Zoomable image preserving natural size              | `true`              | `true`            |
| Zoomable image initially stretching to the viewport | `false`             | `false`           |

Explicit child dimensions and the child's own layout logic still affect its final size.

## Gesture mappings

`GestureBindings` is an `AvaloniaDictionary<ScrollGesture, ScrollGestureAction>`. A `ScrollGesture`
combines one physical input gesture with an exact `KeyModifiers` value. Dictionary assignment replaces
an existing mapping, so a gesture/modifier pair can have only one action.

Avalonia's modifier flags can be combined, but the resulting combination must match exactly:

| `KeyModifiers` | Meaning                     |
| -------------- | --------------------------- |
| `None`         | No modifier                 |
| `Alt`          | Alt key                     |
| `Control`      | Control key                 |
| `Shift`        | Shift key                   |
| `Meta`         | Platform meta or system key |

| `ScrollInputGesture` | Physical input                     |
| -------------------- | ---------------------------------- |
| `TouchDrag`          | One-contact touch or pen drag      |
| `TouchPinch`         | Two-contact touch or pen pinch     |
| `MouseLeftDrag`      | Left mouse-button drag             |
| `MouseMiddleDrag`    | Middle mouse-button drag           |
| `MouseRightDrag`     | Right mouse-button drag            |
| `MouseWheel`         | Vertical wheel component           |
| `MouseWheelTilt`     | Horizontal or tilt-wheel component |

| `ScrollGestureAction` | Behavior                                                    |
| --------------------- | ----------------------------------------------------------- |
| `None`                | Leave the matched gesture unhandled                         |
| `Pan`                 | Translate both enabled axes                                 |
| `AutoScroll`          | Use vertical range when present, otherwise horizontal range |
| `HorizontalScroll`    | Scroll only horizontally                                    |
| `VerticalScroll`      | Scroll only vertically                                      |
| `Zoom`                | Change the zoom factor                                      |

`AutoScroll` prefers vertical scrolling when vertical range exists. If only horizontal range exists,
an ordinary wheel scrolls horizontally even when the vertical mode is enabled but has no range.
Explicit `HorizontalScroll` and `VerticalScroll` mappings never switch axes.

The default profile is:

| Gesture           | Modifiers | Action             |
| ----------------- | --------- | ------------------ |
| `TouchDrag`       | None      | `Pan`              |
| `TouchPinch`      | None      | `Zoom`             |
| `MouseLeftDrag`   | None      | `None`             |
| `MouseMiddleDrag` | None      | `Pan`              |
| `MouseRightDrag`  | None      | `None`             |
| `MouseWheel`      | None      | `AutoScroll`       |
| `MouseWheel`      | Shift     | `HorizontalScroll` |
| `MouseWheel`      | Control   | `Zoom`             |
| `MouseWheelTilt`  | None      | `HorizontalScroll` |

The default right-button mapping leaves context menus available. If right-button dragging is mapped
explicitly, the pointer is captured only after the platform drag threshold is crossed.

Creating an empty `ScrollGestureBindings` replaces the entire default profile. Call `CreateDefault()`
before overriding individual entries when the remaining defaults should be retained:

```csharp
var bindings = ScrollGestureBindings.CreateDefault();
bindings[new ScrollGesture(ScrollInputGesture.MouseRightDrag)] = ScrollGestureAction.Pan;
scrollView.GestureBindings = bindings;
```

## Input and motion tuning

| API                               | Range                | Meaning                                                                 |
| --------------------------------- | -------------------- | ----------------------------------------------------------------------- |
| `ScrollInputMultiplier = 1`       | Finite, non-negative | Multiplier for pan and wheel translation; zero suppresses those deltas  |
| `ZoomInputMultiplier = 1`         | Finite, non-negative | Multiplier for pinch, wheel, and drag zoom; zero suppresses zoom deltas |
| `IsScrollInertiaEnabled = true`   | Boolean              | Allow pan, wheel, and zoom input to continue after release              |
| `ScrollInertiaDecayRate = 0.8655` | `[0.01, 0.9999]`     | Translation velocity retained per 60 Hz frame                           |
| `ZoomInertiaDecayRate = 0.8655`   | `[0.01, 0.9999]`     | Logarithmic scale velocity retained per 60 Hz frame                     |
| `OverscrollElasticity = 0.5`      | `[0, 1]`             | Nonlinear touch/pen resistance and maximum visible overscroll           |
| `OverscrollBounceRate = 1`        | Finite, positive     | Speed multiplier for the critically damped boundary return              |

`OverscrollElasticity` acts while touch or pen is still pulling beyond a boundary. Mouse dragging,
wheel input, scroll bars, and programmatic navigation remain clamped. `OverscrollBounceRate` controls
how quickly already-overscrolled content returns after release. It has no visible effect when
elasticity is zero.

Translation and zoom inertia share the same composition clock but retain independent velocity
channels, so both can continue together. Setting `IsScrollInertiaEnabled` to `false` affects subsequent
input and does not forcibly cancel inertia that is already running.

The default decay value corresponds to the legacy wheel and zoom feel with an 80 ms velocity
half-life. It is not directly comparable to the old internal tracker value of `0.95`, which used a
different time unit.

## Anchoring

Anchoring keeps a visible element stable when content before it changes size. The horizontal and
vertical ratios select corresponding points on both the viewport and each candidate: zero is the
left/top edge, `0.5` is the center, and one is the right/bottom edge.

The presenter chooses the registered, visible, intersecting candidate whose ratio point is closest
to the viewport ratio point. If later layout moves that point, `Offset` is corrected by the same
zoomed distance.

Each ratio is coerced to `[0, 1]`; `double.NaN` disables anchoring on that axis. A ratio of one while
already at the far boundary keeps the view at the new far boundary when content or viewport size
changes. The default ratio is zero, which keeps a view already at the near boundary at offset zero
without selecting an element.

| API                                            | Meaning                                                                     |
| ---------------------------------------------- | --------------------------------------------------------------------------- |
| `HorizontalAnchorRatio`, `VerticalAnchorRatio` | Ratio used for automatic selection; `NaN` disables an axis                  |
| `AnchorRequested`                              | Allows an application to override the current automatic candidate selection |
| `CurrentAnchor`                                | Candidate selected by the most recently completed layout pass               |
| `RegisterAnchorCandidate(Control)`             | Register a visual descendant as a candidate                                 |
| `UnregisterAnchorCandidate(Control)`           | Remove a registered candidate                                               |

`ScrollingAnchorRequestedEventArgs.AnchorCandidates` is an immutable snapshot of registered, visible candidates
intersecting the viewport. A handler may assign one of them to `AnchorElement`. If the selection is no
longer valid, the presenter falls back to ratio-based selection. The event runs during layout, so
handlers should avoid expensive work and layout re-entry.

When a near or far boundary itself satisfies an axis, that axis does not independently request an
element anchor. Reading `CurrentAnchor` never starts synchronous selection or raises the event.

Registration is forwarded to the current `ScrollPresenter`; calls made before template application
are not retained.

## Snap points

| API                                                            | Meaning                                     |
| -------------------------------------------------------------- | ------------------------------------------- |
| `HorizontalSnapPointsType`, `VerticalSnapPointsType`           | Snap behavior for each axis                 |
| `HorizontalSnapPointsAlignment`, `VerticalSnapPointsAlignment` | Viewport position aligned with a snap point |

The content, or an `ItemsControl` panel, must implement `IScrollSnapPointsInfo` for these properties
to have an effect.

| Alignment | Horizontal | Vertical    |
| --------- | ---------- | ----------- |
| `Near`    | Left edge  | Top edge    |
| `Center`  | Center     | Center      |
| `Far`     | Right edge | Bottom edge |

Alignment describes where a snap point is placed in the viewport, not which snap point is selected.
For example, a centered horizontal card list uses `HorizontalSnapPointsAlignment="Center"`.

Avalonia defines the types as follows:

| Type              | Meaning                                                                         |
| ----------------- | ------------------------------------------------------------------------------- |
| `None`            | Do not snap                                                                     |
| `Mandatory`       | Choose a point near the natural inertia destination along the inertia direction |
| `MandatorySingle` | Choose a point near the gesture release position along the inertia direction    |

The current implementation distinguishes `None` from enabled snapping but does not yet distinguish
`Mandatory` from `MandatorySingle`. For the same input, impulse inertia selects the next point in its
direction and other inertia selects the point nearest the natural destination. `ScrollTo` does not
snap automatically.

Snapping retargets the active inertia destination and follows the remaining inertia curve. It does
not stop inertia or jump immediately. Additional wheel input during the animation recalculates the
destination, allowing continuous forward or reverse scrolling.

## Other behavior

### Scroll chaining

`IsScrollChainingEnabled` defaults to `true`. When a nested view reaches a boundary, an unconsumed
pan delta may continue to an ancestor scrollable control. Set it to `false` to keep the gesture within
the current `ScrollView`.

### Deferred scrolling

`IsDeferredScrollingEnabled` defaults to `false` and affects only template scroll-bar thumb dragging:

- `false`: every thumb position updates content immediately.
- `true`: the thumb follows the pointer, but content updates to the final position on release.

It is independent of inertia and does not defer wheel, pan, pinch, zoom, `ScrollTo`, or `Offset`
assignment. It is useful when continuous updates of expensive content would be undesirable.

## Common configurations

### Image viewer

Enable two-axis panning, zooming, and elastic boundary return without scroll bars:

```xml
<smoothScroll:ScrollView
    HorizontalScrollBarVisibility="Hidden"
    VerticalScrollBarVisibility="Hidden"
    HorizontalScrollMode="Enabled"
    VerticalScrollMode="Enabled"
    IsHorizontalMeasureInfinite="True"
    IsVerticalMeasureInfinite="True"
    IsZoomEnabled="True"
    OverscrollElasticity="0.5"
    OverscrollBounceRate="1">
    <Image />
</smoothScroll:ScrollView>
```

### Vertical list

Disable horizontal input and enable vertical scrolling only when content overflows:

```xml
<smoothScroll:ScrollView
    HorizontalScrollMode="Disabled"
    VerticalScrollMode="Auto"
    HorizontalScrollBarVisibility="Hidden"
    VerticalScrollBarVisibility="Auto"
    IsHorizontalMeasureInfinite="False"
    IsVerticalMeasureInfinite="True">
    <ItemsControl />
</smoothScroll:ScrollView>
```

### Expensive content with deferred thumb tracking

```xml
<smoothScroll:ScrollView
    IsDeferredScrollingEnabled="True"
    VerticalScrollMode="Auto"
    VerticalScrollBarVisibility="Visible">
    <!-- Expensive content -->
</smoothScroll:ScrollView>
```

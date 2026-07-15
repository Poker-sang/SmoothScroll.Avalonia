# SmoothScroll.Avalonia

[![NuGet Version](https://img.shields.io/nuget/vpre/SmoothScroll.Avalonia)](https://www.nuget.org/packages/SmoothScroll.Avalonia)
[![NuGet Downloads](https://img.shields.io/nuget/dt/SmoothScroll.Avalonia)](https://www.nuget.org/packages/SmoothScroll.Avalonia)

SmoothScroll.Avalonia 为 Avalonia 提供基于组合线程的平滑滚动交互和独立的 `ScrollView` 控件，
其设计参考了 WinUI 的 `InteractionTracker` 与 `ScrollView`。

## 功能

- 平滑滚动（`ScrollViewer` 也支持）
- 链式滚动（`ScrollViewer` 也支持）
- 拖动和缩放
- 多点触控
- 非线性滚动、缩放、回弹动画
- 可调节惯性衰减、回弹速率和输入倍率，并可完全自定义手势映射
- 完善的区分程序/用户操作事件
- 吸附点、锚点支持

SmoothScroll.Avalonia 为 Avalonia 原生 `ScrollViewer` 提供平滑滚动主题，同时提供功能更完整的独立 `ScrollView` 控件。

https://github.com/user-attachments/assets/927a8c80-ac2b-4d50-b86b-8b2fe853ce5d

## ScrollViewer

如果你只需要平滑滚动的功能，并且已经在使用 `ListBox`、`ScrollViewer` 等控件，你只需引入这个主题即可实现：

```xml
<Application
    xmlns="https://github.com/avaloniaui"
    xmlns:smoothScroll="using:SmoothScroll.Avalonia.Controls">
    <Application.Styles>
        <!-- 其他应用样式 -->
        <smoothScroll:ScrollViewerSmoothTheme />
    </Application.Styles>
</Application>
```

但如果你需要更精细的滚动控制，或者对拖拽、缩放有需求，你可以使用以下完全独立的 `ScrollView` 控件。

## ScrollView

### 使用前提

同样，使用 `ScrollView` 需要引入对应主题：

```xml
<Application
    xmlns="https://github.com/avaloniaui"
    xmlns:smoothScroll="using:SmoothScroll.Avalonia.Controls">
    <Application.Styles>
        <!-- 其他应用样式 -->
        <smoothScroll:ScrollViewDefaultTheme />
    </Application.Styles>
</Application>
```

### 坐标

| API                                                        | 说明                                                                 |
| ---------------------------------------------------------- | -------------------------------------------------------------------- |
| `Size Extent { get; }`                                     | `IScrollable` 坐标系中的内容尺寸，包含当前 `ZoomFactor`              |
| `Size LogicalExtent { get; }`                              | 应用缩放前的逻辑内容尺寸，不受 `ZoomFactor` 影响                     |
| `Size Viewport { get; }`                                   | 实际可见的内容区域尺寸                                               |
| `Vector Offset { get; set; }`                              | 当前逻辑滚动位置                                                     |
| `Vector ScrollBarMaximum { get; }`                         | 每个方向允许的最大逻辑偏移。没有溢出或该轴被禁用时为零               |
| `bool IsExpanded { get; }`                                 | 当前主题中的任一滚动条是否处于扩展视觉状态；它不是“是否可滚动”的标志 |
| `ScrollPresenter? ScrollPresenter { get; }`                | 当前模板中的强类型呈现器；模板尚未应用或部件缺失时为 `null`          |
| `int ScrollTo(Vector offset, bool isAnimated = true)`      | 滚动到 `offset`，默认使用动画，并返回操作关联 ID                     |
| `int ScrollBy(Vector offsetDelta, bool isAnimated = true)` | 滚动到当前 `Offset` + `offsetDelta`，默认使用动画，并返回操作关联 ID |

直接写入 `Offset` 时，每个分量都会被限制为
`0 <= Offset.X <= ScrollBarMaximum.X` 和 `0 <= Offset.Y <= ScrollBarMaximum.Y`。
写入不使用动画，会产生 `Programmatic` 变化，
相当于调用 `ScrollTo(offset, false)`。

`Offset` 始终是从 `(0, 0)` 开始的逻辑滚动坐标。即使内容比视口小、且通过
`HorizontalContentAlignment="Center"` 或 `VerticalContentAlignment="Center"` 被视觉居中，
`Offset` 仍然是零，不会暴露内部追踪器为实现对齐而使用的负坐标。

### 将元素带入视区

Avalonia 原有的无参数 `BringIntoView()` 会立即执行最小距离滚动。引入
`SmoothScroll.Avalonia.Controls` 命名空间后，还可以使用以下支持动画的重载：

| API                                                    | 说明                                       |
| ------------------------------------------------------ | ------------------------------------------ |
| `void BringIntoView(bool isAnimated)`                  | 将整个控件带入最近的可见位置               |
| `void BringIntoView(Rect targetRect, bool isAnimated)` | 将控件坐标系中的指定矩形带入最近的可见位置 |

```csharp
target.BringIntoView(isAnimated: true);
target.BringIntoView(targetRect, isAnimated: true);
```

这两个扩展方法同时适用于 `ScrollView` 和使用 `ScrollViewerSmoothTheme` 的 Avalonia
`ScrollViewer`。在 `ScrollView` 中，由此产生的变化会报告为 `Programmatic`。传入 `false`
时使用与 Avalonia 原有 `BringIntoView()` 一致的立即滚动语义。

### 缩放

| API                                                                              | 说明                                                                       |
| -------------------------------------------------------------------------------- | -------------------------------------------------------------------------- |
| `bool IsZoomEnabled { get; set; } = false;`                                      | 是否接受已绑定的用户缩放手势。设为 `false` 不会禁止 `Programmatic` 变化    |
| `double MinZoomFactor { get; set; } = 0.1;`                                      | 最小缩放比例，必须是有限正数                                               |
| `double MaxZoomFactor { get; set; } = 10;`                                       | 最大缩放比例，必须是有限正数                                               |
| `double ZoomFactor { get; set; } = 1;`                                           | 当前实际缩放比例                                                           |
| `int ZoomTo(double zoomFactor, bool isAnimated = true)`                          | 缩放到 `zoomFactor`，默认使用动画，以视口中心为缩放中心，并返回操作关联 ID |
| `int ZoomTo(double zoomFactor, Point? centerPoint, bool isAnimated = true)`      | 围绕指定的视口坐标缩放到 `zoomFactor`，并返回操作关联 ID                   |
| `int ZoomBy(double zoomFactorDelta, bool isAnimated = true)`                     | 缩放到当前比例 + `zoomFactorDelta`，默认使用动画，并返回操作关联 ID        |
| `int ZoomBy(double zoomFactorDelta, Point? centerPoint, bool isAnimated = true)` | 围绕指定的视口坐标增加 `zoomFactorDelta`，并返回操作关联 ID                |

应始终保持 `0 < MinZoomFactor <= MaxZoomFactor`。用户手势和程序缩放都会限制在该区间内。

直接写入 `ZoomFactor` 时，限制为 `MinZoomFactor` <= `ZoomFactor` <= `MaxZoomFactor`，会立即缩放，不使用动画，会产生 `Programmatic` 变化，并以视口中心为缩放中心。

`centerPoint` 使用 `ScrollView` 视口坐标，而不是内容坐标；传入 `null` 等同于视口中心。
指定点在缩放过程中保持视觉位置不变。两个分量都必须是有限值，但不要求落在视口边界内。

模板尚未应用时设置 `ZoomFactor` 或调用 `ZoomTo` 会保留最终比例，待呈现器可用后立即应用；
此时没有可用视口，指定的 `centerPoint` 不会延后影响 `Offset`。

### 变化事件

`ScrollChanged` 在 `Extent`、`Viewport` 或 `Offset` 改变时冒泡。
`ZoomChanged` 在 `ZoomFactor` 变化时冒泡。

事件参数中的来源可用于过滤：

| `ChangeSource` | 触发途径                                         |
| -------------- | ------------------------------------------------ |
| `User`         | 控件直接处理的鼠标、触控、滚轮、滚动条或键盘输入 |
| `Programmatic` | 写属性、调用方法，或发出 `BringIntoView` 请求    |
| `Layout`       | 尺寸、内容、锚点或其他布局重新计算导致的位置变化 |

`ScrollViewChangedEventArgs.IsUserInitiated` 等价于
`ChangeSource == ScrollChangeSource.User`。

| 事件参数属性                                 | 说明                         |
| -------------------------------------------- | ---------------------------- |
| `ScrollViewChangedEventArgs.ExtentDelta`     | 本次 `Extent` 的宽高变化量   |
| `ScrollViewChangedEventArgs.OffsetDelta`     | 本次 `Offset` 变化量         |
| `ScrollViewChangedEventArgs.ViewportDelta`   | 本次 `Viewport` 的宽高变化量 |
| `ScrollViewChangedEventArgs.ChangeSource`    | 本次滚动变化的来源           |
| `ScrollViewChangedEventArgs.IsUserInitiated` | 是否为控件直接处理的用户输入 |
| `ZoomChangedEventArgs.ZoomFactorDelta`       | 本次 `ZoomFactor` 变化量     |
| `ZoomChangedEventArgs.ChangeSource`          | 本次缩放变化的来源           |
| `ZoomChangedEventArgs.IsUserInitiated`       | 是否为控件直接处理的用户输入 |

```cs
private void ScrollViewOnScrollChanged(object? sender, ScrollViewChangedEventArgs e)
{
    if (!e.IsUserInitiated)
        return;

    // 在此仅处理用户操作
}
```

### 操作生命周期

`ScrollTo`、`ScrollBy`、`ZoomTo` 和 `ZoomBy` 在接受新操作时返回一个正整数关联 ID。
如果钳制后的值没有变化、直接操作当前占用追踪器，或没有创建操作，则返回
`ScrollView.NoCorrelationId`（值为 `-1`）。方法返回的 ID 与对应开始和完成事件中的
`CorrelationId` 相同；直接写入 `Offset` 或 `ZoomFactor` 时无法从属性写入取得返回值，
但仍可从事件参数读取关联 ID。

| API / 事件                                 | 说明                               |
| ------------------------------------------ | ---------------------------------- |
| `ScrollStarting`                           | 滚动操作被接受                     |
| `ScrollCompleted`                          | 滚动操作完成、被打断或被追踪器忽略 |
| `ZoomStarting`                             | 缩放操作被接受                     |
| `ZoomCompleted`                            | 缩放操作完成、被打断或被追踪器忽略 |
| `StateChanged`                             | `State` 发生变化                   |
| `ScrollingInteractionState State { get; }` | 当前直接操作、惯性或程序动画状态   |

即使是非动画的立即操作，也会按顺序发出开始和完成事件。同一通道上的新操作会把上一个操作
完成为 `Interrupted`；滚动和缩放是两个独立通道，可以同时活动并使用不同的关联 ID。

| `ScrollingOperationResult` | 说明                               |
| -------------------------- | ---------------------------------- |
| `Completed`                | 到达请求值或自然静止值             |
| `Interrupted`              | 被新请求、直接操作或控件卸载取代   |
| `Ignored`                  | 交互追踪器在应用请求前拒绝了该操作 |

| `ScrollingInteractionState` | 说明                             |
| --------------------------- | -------------------------------- |
| `Idle`                      | 没有直接操作、惯性或程序动画     |
| `Interaction`               | 指针或触控正在直接操作视图       |
| `Inertia`                   | 平移、缩放或边界回弹惯性正在运行 |
| `Animation`                 | 程序滚动或缩放动画正在运行       |

开始事件参数提供起始值、请求目标、动画标志和 `ChangeSource`；完成事件参数提供最终值、
`ScrollingOperationResult`、`ChangeSource` 以及相同的 `CorrelationId`。

### 轴、滚动条与测量

每个方向的滚动能力、滚动条显示和内容测量都是三组正交的设置。

| API                                                                           | 说明                   |
| ----------------------------------------------------------------------------- | ---------------------- |
| `ScrollMode HorizontalScrollMode { get; set; } = Auto;`                       | 水平滚动模式           |
| `ScrollMode VerticalScrollMode { get; set; } = Auto;`                         | 垂直滚动模式           |
| `ScrollBarVisibilityMode HorizontalScrollBarVisibility { get; set; } = Auto;` | 水平滚动条可见性       |
| `ScrollBarVisibilityMode VerticalScrollBarVisibility { get; set; } = Auto;`   | 垂直滚动条可见性       |
| `ScrollMode ComputedHorizontalScrollMode { get; }`                            | 解析后的水平滚动模式   |
| `ScrollMode ComputedVerticalScrollMode { get; }`                              | 解析后的垂直滚动模式   |
| `ScrollBarVisibilityMode ComputedHorizontalScrollBarVisibility { get; }`      | 解析后的水平滚动条状态 |
| `ScrollBarVisibilityMode ComputedVerticalScrollBarVisibility { get; }`        | 解析后的垂直滚动条状态 |
| `bool IsHorizontalMeasureInfinite { get; set; } = false;`                     | 是否使用无限宽测量约束 |
| `bool IsVerticalMeasureInfinite { get; set; } = true;`                        | 是否使用无限高测量约束 |

#### `HorizontalScrollMode` 与 `VerticalScrollMode`

二者决定相应方向是否接收用户平移/滚轮输入：

| `ScrollMode` | 说明                                                                                      |
| ------------ | ----------------------------------------------------------------------------------------- |
| `Auto`       | 仅当该轴内容尺寸大于 `Viewport` 时启用                                                    |
| `Enabled`    | 始终启用该轴的交互源。内容不溢出时逻辑最大偏移仍为零，但触控/笔可以在该方向越界拉动并回弹 |
| `Disabled`   | 始终禁用该轴的用户交互；程序设置的偏移也会被限制为零                                      |

#### `HorizontalScrollBarVisibility` 与 `VerticalScrollBarVisibility`

二者决定模板滚动条是否显示，不决定滚动行为：

| `ScrollBarVisibilityMode` | 说明                                                                              |
| ------------------------- | --------------------------------------------------------------------------------- |
| `Auto`                    | 该轴可滚动时显示，不可滚动时隐藏。`ScrollMode.Enabled` 即使内容未溢出也视为可滚动 |
| `Visible`                 | 始终显示                                                                          |
| `Hidden`                  | 始终隐藏                                                                          |

当滚动条处于 `Visible` 状态时，`ScrollView` 保留 Avalonia 默认的 `AllowAutoHide` 行为：
支持该视觉状态的主题（例如 Fluent）会让空闲滚动条收缩，在指针移入后再展开。它只改变
`IsExpanded` 的视觉状态，不改变 `Visibility`、布局中的可见性语义或滚动能力。

四个 `Computed*` 属性用于观察 `Auto` 解析后的当前结果：滚动模式只会是 `Enabled` 或
`Disabled`，滚动条可见性只会是 `Visible` 或 `Hidden`。滚动条收缩后的紧凑视觉状态仍由
`IsExpanded` 表示，不会把 `Computed*ScrollBarVisibility` 改为 `Hidden`。

#### `IsHorizontalMeasureInfinite` 与 `IsVerticalMeasureInfinite`

这两个属性分别控制子元素在 `Measure` 阶段获得的约束，而不是是否允许滚动：

| API                           | `true`                                 | `false`                            |
| ----------------------------- | -------------------------------------- | ---------------------------------- |
| `IsHorizontalMeasureInfinite` | 给子元素无限宽约束，更利于保留自然宽度 | 给子元素视口宽约束，更利于横向拉伸 |
| `IsVerticalMeasureInfinite`   | 给子元素无限高约束，更利于保留自然高度 | 给子元素视口高约束，更利于纵向拉伸 |

元素自身显式设置的宽高及其布局逻辑仍会影响最终结果。以下是常见场景及其对应设置：

| 场景                                             | `IsHorizontalMeasureInfinite` | `IsVerticalMeasureInfinite` |
| ------------------------------------------------ | ----------------------------- | --------------------------- |
| 纵向延伸的 `StackPanel`                          | `false`                       | `true`                      |
| 横向延伸的 `StackPanel`                          | `true`                        | `false`                     |
| 可放大拖拽的图片查看器，默认保持图片原始大小     | `true`                        | `true`                      |
| 可放大拖拽的图片查看器，默认使图片拉伸到视口大小 | `false`                       | `false`                     |

### 手势映射

| API               | 默认值   | 说明                                                                                      |
| ----------------- | -------- | ----------------------------------------------------------------------------------------- |
| `GestureBindings` | 默认映射 | 唯一复合手势键到 `Pan`、`AutoScroll`、横/纵向滚动、`Zoom` 或 `None` 的字典，不能为 `null` |

`GestureBindings` 是一个 `AvaloniaDictionary<ScrollGesture, ScrollGestureAction>`。
不可变的 `ScrollGesture` 键由“物理手势 + **精确匹配**的 `KeyModifiers`”组成，
因此同一个组合只能注册一个动作；
使用字典索引器可以直接添加或替换映射，不会留下重复项。

| `ScrollInputGesture` | 说明         |
| -------------------- | ------------ |
| `TouchDrag`          | 触控/笔拖动  |
| `TouchPinch`         | 触控/笔捏合  |
| `MouseLeftDrag`      | 鼠标左键拖动 |
| `MouseMiddleDrag`    | 鼠标中键拖动 |
| `MouseRightDrag`     | 鼠标右键拖动 |
| `MouseWheel`         | 滚动鼠标滚轮 |
| `MouseWheelTilt`     | 倾斜鼠标滚轮 |

| `[Flags] KeyModifiers` | 说明（枚举可组合）      |
| ---------------------- | ----------------------- |
| `None = 0`             | 无修饰键                |
| `Alt = 1`              | Alt 键                  |
| `Control = 2`          | Ctrl 键                 |
| `Shift = 4`            | Shift 键                |
| `Meta = 8`             | Meta 键（一般是系统键） |

| `ScrollGestureAction` | 说明             |
| --------------------- | ---------------- |
| `None`                | 无操作           |
| `Pan`                 | 拖拽             |
| `AutoScroll`          | 自动选择滚动方向 |
| `HorizontalScroll`    | 水平滚动         |
| `VerticalScroll`      | 纵向滚动         |
| `Zoom`                | 缩放             |

`AutoScroll` 在纵向存在实际滚动范围时使用纵向；若只有横向存在实际滚动范围，则普通滚轮自动
改为横向，即使纵向模式已启用但内容没有溢出。双轴都有实际范围时仍优先纵向。显式配置的
`VerticalScroll` 和 `HorizontalScroll` 始终严格使用指定轴，不会自动切换。

以下是默认手势映射：

| 手势              | 修饰键    | 动作               |
| ----------------- | --------- | ------------------ |
| `TouchDrag`       | 无        | `Pan`              |
| `TouchPinch`      | 无        | `Zoom`             |
| `MouseLeftDrag`   | 无        | `Pan`              |
| `MouseMiddleDrag` | 无        | `Pan`              |
| `MouseRightDrag`  | 无        | 无操作             |
| `MouseWheel`      | 无        | `AutoScroll`       |
| `MouseWheel`      | `Shift`   | `HorizontalScroll` |
| `MouseWheel`      | `Control` | `Zoom`             |
| `MouseWheelTilt`  | 无        | `HorizontalScroll` |

默认不处理右键，因此右键菜单仍可用。即使显式添加右键拖动绑定，只有超过系统拖动阈值后才会
捕获指针。

直接新建空的 `ScrollGestureBindings` 会完全替换默认映射；若仍需默认行为，应先调用
`CreateDefault()` 再通过索引器覆盖所需组合。

示例：仅把右键拖动用作平移，同时保留未拖动时的右键菜单：

```csharp
var bindings = ScrollGestureBindings.CreateDefault();
bindings[new ScrollGesture(ScrollInputGesture.MouseRightDrag)] = ScrollGestureAction.Pan;
scrollView.GestureBindings = bindings;
```

### 输入倍率

| API                                               | 说明                                                             |
| ------------------------------------------------- | ---------------------------------------------------------------- |
| `double ScrollInputMultiplier { get; set; } = 1;` | 平移和滚轮位移倍率；必须为有限非负值，`0` 会抑制这些手势位移。   |
| `double ZoomInputMultiplier { get; set; } = 1;`   | 捏合、滚轮和拖动缩放倍率；必须为有限非负值，`0` 会抑制缩放手势。 |

### 惯性、越界弹性与回弹

| API                                                     | 有效范围         | 说明                                                                                                                                      |
| ------------------------------------------------------- | ---------------- | ----------------------------------------------------------------------------------------------------------------------------------------- |
| `bool IsScrollInertiaEnabled { get; set; } = true;`     | `bool`           | 是否让用户平移、滚轮和缩放在输入结束后进入惯性。关闭后仍会立即应用输入增量，但释放时不产生平移或缩放惯性。                                |
| `double ScrollInertiaDecayRate { get; set; } = 0.8655;` | `[0.01, 0.9999]` | 每个 60 Hz 帧保留的平移速度比例；越接近 `1`，滚动越久。                                                                                   |
| `double ZoomInertiaDecayRate { get; set; } = 0.8655;`   | `[0.01, 0.9999]` | 每个 60 Hz 帧保留的对数缩放速度比例；越接近 `1`，缩放越久。                                                                               |
| `double OverscrollElasticity { get; set; } = 0.5;`      | `[0, 1]`         | 触控/触控笔拖动时越过边界的非线性阻尼和最大可见拉出量。鼠标拖动、滚轮、滚动条和程序滚动始终钳制在边界内。`0` 是硬边界，`1` 是最大的弹性。 |
| `double OverscrollBounceRate { get; set; } = 1;`        | 有限正数         | 已经越界时回到合法范围的弹簧速度倍率；值越大，回弹越快。                                                                                  |

`OverscrollElasticity` 和 `OverscrollBounceRate` 的职责不同：前者发生在触控/触控笔**仍在拖动时**，决定
能把内容拉出边界多少；后者发生在**释放后已经越界时**，决定回弹有多快。
鼠标、滚轮、滚动条和程序操作不会进入越界物理。弹性为 `0` 时不会越界，所以 `OverscrollBounceRate` 没有可见作用。

平移和缩放惯性由同一帧时钟调度，但两个速度通道独立衰减，因此它们可以同时进行。
将 `IsScrollInertiaEnabled` 设为 `false` 只影响后续输入，不会主动取消已经运行的惯性。

默认值 `0.8655` 对应旧版滚轮和缩放惯性使用的 80 ms 速度半衰期；它不是旧版内部
`InteractionTracker` 的 `0.95` 配置值，后者在旧架构中使用了不同的时间单位。

### 锚点

当视区之前的内容尺寸发生变化时，当前可见元素可能整体移动。锚点功能会通过调整 `Offset`
尽量保持其中一个可见元素原有的视觉位置，避免内容在用户眼前跳动。

两个 Ratio 属性分别定义视口及候选元素上的比较位置：`0` 是左/上边缘，`0.5` 是中心，`1`
是右/下边缘。`ScrollPresenter` 会从已注册、可见且与视口相交的候选元素中，选择对应 Ratio
点距离视口 Ratio 点最近的元素。它记录候选元素相对于内容的布局位置和尺寸；若后续布局使该
Ratio 点发生变化，呈现器会按缩放后的相同变化量修正 `Offset`。

每个轴的值都会限制在 `[0, 1]`；设为 `double.NaN` 会单独关闭该轴的锚定。Ratio 为 `1` 且
当前已经位于该轴末端时，不依赖候选元素，内容尺寸增长或视口尺寸变化后仍会保持在新的末端。
默认值为 `0`；位于起点时直接保持零偏移，不额外选择元素。

锚点选择不依赖 `ChangeSource`。注册或取消候选元素以及后续布局都可能使候选元素被重新选择。

`AnchorRequested` 在布局确实需要选择元素锚点时触发。事件参数中的 `AnchorCandidates` 是本次
已注册、可见且与视口相交候选元素的不可修改快照。处理器可以把其中一个元素赋给
`AnchorElement`，从而覆盖本次 Ratio 自动选择；如果该元素已失效、被取消注册或不在快照中，
Presenter 会忽略它并回退到自动选择。事件处理器运行在布局过程中，应避免耗时操作和布局重入。

某个轴的 Ratio 为 `0` 且已经位于起点，或为 `1` 且已经位于末端时，该轴由边界本身完成锚定，
不会为该轴单独请求元素 `AnchorRequested`。`CurrentAnchor` 表示最近一次已完成布局的选择结果；读取它
不会同步选择锚点或触发事件，在边界锚定和尚未完成选择时为 `null`。

与锚点相关的 API 如下：

| API                                                                     | 说明                                 |
| ----------------------------------------------------------------------- | ------------------------------------ |
| `double HorizontalAnchorRatio { get; set; } = 0;`                       | 水平锚点比例；`NaN` 关闭水平锚定     |
| `double VerticalAnchorRatio { get; set; } = 0;`                         | 垂直锚点比例；`NaN` 关闭垂直锚定     |
| `event EventHandler<ScrollingAnchorRequestedEventArgs> AnchorRequested` | 允许应用覆盖本次元素锚点选择         |
| `Control? CurrentAnchor { get; }`                                       | 最近一次布局选中的锚点候选元素       |
| `void RegisterAnchorCandidate(Control control)`                         | 将内容呈现器的视觉后代注册为锚点候选 |
| `void UnregisterAnchorCandidate(Control control)`                       | 将控件取消注册为锚点候选             |

注册操作会转发给当前模板中的 `ScrollPresenter`，因此应在模板应用后调用；模板尚未应用时调用不会保留该候选元素。

### 吸附点

| API                                                                       | 说明               |
| ------------------------------------------------------------------------- | ------------------ |
| `SnapPointsType HorizontalSnapPointsType { get; set; } = None;`           | 水平吸附模式       |
| `SnapPointsType VerticalSnapPointsType { get; set; } = None;`             | 垂直吸附模式       |
| `SnapPointsAlignment HorizontalSnapPointsAlignment { get; set; } = Near;` | 水平吸附点对齐模式 |
| `SnapPointsAlignment VerticalSnapPointsAlignment { get; set; } = Near;`   | 垂直吸附点对齐模式 |

内容本身或 `ItemsControl` 的面板必须实现 `IScrollSnapPointsInfo`，否则这些设置没有效果。

`SnapPointsAlignment` 不是“选择哪一个吸附点”，而是“把吸附点对齐到视口的哪个位置”：

| `SnapPointsAlignment` | 横向       | 纵向       |
| --------------------- | ---------- | ---------- |
| `Near`                | 视口左边缘 | 视口上边缘 |
| `Center`              | 视口中心   | 视口中心   |
| `Far`                 | 视口右边缘 | 视口下边缘 |

例如，一个横向卡片列表要让选中的卡片居中，应使用
`HorizontalSnapPointsAlignment="Center"`；若要让每一项的左边缘贴住视口左边，使用 `Near`。

`SnapPointsType` 的 Avalonia 语义如下：

| `SnapPointsType`  | 说明                                     |
| ----------------- | ---------------------------------------- |
| `None`            | 不吸附                                   |
| `Mandatory`       | 沿惯性方向选择最接近自然惯性落点的吸附点 |
| `MandatorySingle` | 沿惯性方向选择最接近手势释放位置的吸附点 |

当前 `ScrollView` 的实现已经支持 `None` 与“启用吸附”的分支，并在用户惯性时计算吸附位置；但
`Mandatory` 和 `MandatorySingle` 目前尚未分开实现，对相同输入会采用相同行为：冲量惯性沿方向
选择下一个吸附点，其他惯性选择最接近自然落点的吸附点。直接调用 `ScrollTo` 不会自动吸附。
该差异在未来实现两个枚举值的完整语义时应作为兼容性注意项。

吸附会修改当前惯性的最终落点，并沿剩余惯性曲线动画到精确位置；它不会中止惯性或立即跳转。
动画过程中继续滚轮输入会重新计算后续落点，因此可以连续向前或反向滚动。

### 其他属性

| API                                                      | 说明         |
| -------------------------------------------------------- | ------------ |
| `bool IsScrollChainingEnabled { get; set; } = true;`     | 允许链式滚动 |
| `bool IsDeferredScrollingEnabled { get; set; } = false;` | 允许延迟滚动 |

#### `IsScrollChainingEnabled`

当嵌套在另一个可滚动控件中时，子控件到达边界后，未消费的平移可以继续传给
祖先滚动控件。设为 `false` 可把手势限制在当前 `ScrollView` 中。

#### `IsDeferredScrollingEnabled`

它只影响模板中**滚动条滑块的拖动**：

- `false`：滑块拖动的每个位置都会立即更新内容。
- `true`：滑块本身会跟随指针，但内容在释放滑块后才跳到最终位置。

它与惯性无关，也不影响鼠标滚轮、触控平移、缩放、`ScrollTo` 或直接设置 `Offset`。它适合
内容很重、连续更新代价高，而用户主要通过滚动条定位的场景。

## 行/页导航

`ScrollView` 不公开 `LineUp`、`LineDown`、`PageUp`、`PageDown` 等行/页导航方法。应用需要执行
相对行移或翻页时，应按所需步长调用 `ScrollBy`；需要移动到绝对坐标时再使用 `ScrollTo`。

键盘 `PageUp` / `PageDown` 仍由控件内部按一个 `Viewport.Height` 处理，并报告为 `User`。

## 常用配置

**图片查看器：** 允许双向平移、缩放、回弹，不显示滚动条。

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

**普通纵向列表：** 禁止横向输入，只有内容超出高度时才允许纵向滚动和显示滚动条。

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

**昂贵内容的滚动条定位：** 开启 `IsDeferredScrollingEnabled`，但保留手势和滚轮的实时反馈。

```xml
<smoothScroll:ScrollView
    IsDeferredScrollingEnabled="True"
    VerticalScrollMode="Auto"
    VerticalScrollBarVisibility="Visible">
    <!-- 渲染代价很高的内容 -->
</smoothScroll:ScrollView>
```

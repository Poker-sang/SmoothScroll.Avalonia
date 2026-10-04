namespace SmoothScroll.Avalonia.Interaction.Experimental;

/// <summary>The movement path, with offsets and velocities measured in DIP and DIP/second.</summary>
public enum ExperimentalScrollMovementSource { Direct, Wheel, Inertia }

/// <summary>
/// Numeric compositor-thread input. Positive delta increases the scroll offset.
/// For Direct and Wheel, Velocity and ElapsedSeconds are zero because input sample timing is
/// unavailable in this prototype. For Inertia, they contain the live velocity and integrated frame interval.
/// </summary>
public readonly record struct ExperimentalScrollMovementContext(
    double Delta, double Velocity, double Position, double Minimum, double Maximum,
    double ElapsedSeconds, ExperimentalScrollMovementSource Source);

/// <summary>
/// One completed pre/body/post transaction, including transactions with a stationary body.
/// Direct and Wheel report zero Velocity and ElapsedSeconds as unavailable, following the input context.
/// </summary>
public readonly record struct ExperimentalScrollMovementResult(
    double Requested, double PreConsumed, double SelfConsumed, double PostConsumed,
    double Remaining, double Position, double Velocity, double ElapsedSeconds,
    ExperimentalScrollMovementSource Source);

/// <summary>Effective vertical body bounds in DIP, including compositor-owned viewport changes.</summary>
public readonly record struct ExperimentalScrollBounds(double Minimum, double Maximum);

/// <summary>Creates the participant on the compositor thread, in tracker request order.</summary>
public interface IExperimentalScrollMovementParticipantFactory
{
    IExperimentalScrollMovementParticipant Create();
}

/// <summary>
/// Experimental vertical movement consumer. Every method runs on the compositor thread.
/// The participant must not read or mutate UI controls. Consumption must have the offered sign
/// and must not exceed the offered delta. Overscroll, snapping and zoom are outside this prototype.
/// </summary>
public interface IExperimentalScrollMovementParticipant
{
    double PreScroll(in ExperimentalScrollMovementContext context);
    // Delta is what remains after pre-consumption and the clamped body's movement.
    double PostScroll(in ExperimentalScrollMovementContext context, double selfConsumed);
    /// <summary>Called before movement and after each consumption stage. Must return finite ordered bounds.</summary>
    ExperimentalScrollBounds GetVerticalBounds(in ExperimentalScrollMovementContext context) =>
        new(context.Minimum, context.Maximum);
    void OnScrollCompleted(in ExperimentalScrollMovementResult result) { }
    /// <summary>
    /// Publishes the committed body position after absolute/animated movement or geometry correction.
    /// Configuration also invokes it even if unchanged. Completed pre/body/post transactions use
    /// OnScrollCompleted instead, so this callback never invents input consumption.
    /// </summary>
    void OnPositionChanged(double position) { }
    /// <summary>Actual input release or completed inertia; cancellation and configuration do not invoke this.</summary>
    void OnIdle(ExperimentalScrollMovementSource source) { }
    /// <summary>
    /// Called at idle with zero, then with seconds since idle began while returning true.
    /// Use for compositor-owned settling. New input, cancellation and detachment stop the clock.
    /// </summary>
    bool OnFrame(double elapsedSeconds) => false;
    void OnCancelled() { }
    void Detach() { }
}

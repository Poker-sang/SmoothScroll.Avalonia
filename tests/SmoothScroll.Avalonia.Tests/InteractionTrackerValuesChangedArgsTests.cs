using Avalonia;
using SmoothScroll.Avalonia.Interaction;

namespace SmoothScroll.Avalonia.Tests;

public sealed class InteractionTrackerValuesChangedArgsTests
{
    [Theory]
    [InlineData(InteractionTrackerValuesChangedArgs.UserRequestId, true)]
    [InlineData(0, false)]
    [InlineData(42, false)]
    public void UserRequestIsIdentifiedIndependentlyOfState(int requestId, bool expected)
    {
        var args = new InteractionTrackerValuesChangedArgs(default(Vector3D), 1, requestId);

        Assert.Equal(expected, args.IsUserInitiated);
    }
}

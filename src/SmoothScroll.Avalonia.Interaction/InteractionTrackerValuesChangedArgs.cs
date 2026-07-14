using Avalonia;

namespace SmoothScroll.Avalonia.Interaction;

public sealed class InteractionTrackerValuesChangedArgs
{
    internal const int UserRequestId = -1;

    internal InteractionTrackerValuesChangedArgs(Vector3D position, double scale, int requestId)
    {
        Position = position;
        Scale = scale;
        RequestId = requestId;
    }

    public Vector3D Position { get; }

    public int RequestId { get; }

    public bool IsUserInitiated => RequestId is UserRequestId;

    public double Scale { get; }
}

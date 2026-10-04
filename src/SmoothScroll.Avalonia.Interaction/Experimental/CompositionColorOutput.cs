using Avalonia.Media;
using Avalonia.Rendering.Composition;
using Avalonia.Rendering.Composition.Server;
using Avalonia.Threading;

namespace SmoothScroll.Avalonia.Interaction.Experimental;

/// <summary>Compositor-owned color output, exposed as <c>output.Value</c> to expressions.</summary>
/// <remarks>Capture Writer on the UI thread and use it exclusively from this compositor's callbacks.</remarks>
public sealed class CompositionColorOutput : CompositionObject, IDisposable
{
    private CompositionColorOutput(Compositor compositor, ServerCompositionColorOutput server)
        : base(compositor, server) => Writer = new CompositionColorOutputWriter(server);

    /// <summary>Creates an initially transparent output on the UI thread.</summary>
    public static CompositionColorOutput Create(Compositor compositor)
    {
        ArgumentNullException.ThrowIfNull(compositor);
        Dispatcher.UIThread.VerifyAccess();
        return new CompositionColorOutput(compositor, new ServerCompositionColorOutput(compositor.Server));
    }

    /// <summary>Compositor-only handle for reading and publishing colors.</summary>
    public CompositionColorOutputWriter Writer { get; }

    /// <summary>Detach participants and expressions before disposing this output on the UI thread.</summary>
    public new void Dispose()
    {
        Dispatcher.UIThread.VerifyAccess();
        base.Dispose();
    }
}

/// <summary>A color handle valid only while its compositor lock is held.</summary>
public sealed class CompositionColorOutputWriter
{
    private readonly ServerCompositionColorOutput _server;
    internal CompositionColorOutputWriter(ServerCompositionColorOutput server) => _server = server;

    /// <summary>Reads the current color without a UI round trip.</summary>
    public Color Value => _server.ReadValue();

    /// <summary>Publishes the color and invalidates dependent expressions.</summary>
    public void SetValue(Color value) => _server.WriteValue(value);
}

internal sealed class ServerCompositionColorOutput(ServerCompositor compositor) : ServerObject(compositor), IDisposable
{
    private static readonly CompositionProperty<Color> ValueProperty =
        CompositionProperty.Register<ServerCompositionColorOutput, Color>("Value",
            obj => ((ServerCompositionColorOutput)obj)._value,
            (obj, value) => ((ServerCompositionColorOutput)obj)._value = value,
            obj => ((ServerCompositionColorOutput)obj)._value);

    private Color _value;
    private bool _disposed;

    internal Color ReadValue()
    {
        Compositor.VerifyAccess();
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _value;
    }

    internal void WriteValue(Color value)
    {
        Compositor.VerifyAccess();
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_value != value)
            SetValue(ValueProperty, ref _value, value);
    }

    public override CompositionProperty? GetCompositionProperty(string fieldName) =>
        fieldName == "Value" ? ValueProperty : base.GetCompositionProperty(fieldName);

    public void Dispose()
    {
        Compositor.VerifyAccess();
        _disposed = true;
    }
}

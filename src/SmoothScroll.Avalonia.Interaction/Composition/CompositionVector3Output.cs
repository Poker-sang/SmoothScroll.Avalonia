using Avalonia;
using Avalonia.Rendering.Composition;
using Avalonia.Rendering.Composition.Server;
using Avalonia.Threading;

namespace SmoothScroll.Avalonia.Composition;

/// <summary>
/// Compositor-owned numeric output. Reference this object in an expression
/// animation and read <c>output.Value</c> (or its X, Y and Z components).
/// </summary>
/// <remarks>
/// The UI object deliberately has no value mirror. A participant can capture <see cref="Writer"/>
/// on the UI thread and use it only from callbacks executed by this object's compositor.
/// </remarks>
public sealed class CompositionVector3Output : CompositionObject, IDisposable
{
    private CompositionVector3Output(Compositor compositor, ServerCompositionVector3Output server)
        : base(compositor, server)
    {
        Writer = new CompositionVector3OutputWriter(server);
    }

    /// <summary>Creates an output on the UI thread, initially containing a zero vector.</summary>
    public static CompositionVector3Output Create(Compositor compositor)
    {
        ArgumentNullException.ThrowIfNull(compositor);
        Dispatcher.UIThread.VerifyAccess();
        return new CompositionVector3Output(compositor, new ServerCompositionVector3Output(compositor.Server));
    }

    /// <summary>Gets the handle used exclusively by compositor callbacks to read and write the output.</summary>
    public CompositionVector3OutputWriter Writer { get; }

    /// <summary>
    /// Queues disposal on the compositor. Detach participants and expression animations before
    /// disposal; accesses through the writer fail after the compositor processes disposal.
    /// </summary>
    public new void Dispose()
    {
        Dispatcher.UIThread.VerifyAccess();
        base.Dispose();
    }
}

/// <summary>A numeric output handle that is valid only while its compositor lock is held.</summary>
public sealed class CompositionVector3OutputWriter
{
    private readonly ServerCompositionVector3Output _server;

    internal CompositionVector3OutputWriter(ServerCompositionVector3Output server) => _server = server;

    /// <summary>Reads the current compositor value without a UI round trip.</summary>
    public Vector3D Value => _server.ReadValue();

    /// <summary>Updates the value and invalidates expression animations in the current compositor frame.</summary>
    public void SetValue(Vector3D value) => _server.WriteValue(value);
}

internal sealed class ServerCompositionVector3Output(ServerCompositor compositor) : ServerObject(compositor), IDisposable
{
    private static readonly CompositionProperty<Vector3D> ValueProperty =
        CompositionProperty.Register<ServerCompositionVector3Output, Vector3D>(
            "Value",
            obj => ((ServerCompositionVector3Output)obj)._value,
            (obj, value) => ((ServerCompositionVector3Output)obj)._value = value,
            obj => ((ServerCompositionVector3Output)obj)._value);

    private Vector3D _value;
    private bool _disposed;

    internal Vector3D ReadValue()
    {
        Compositor.VerifyAccess();
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _value;
    }

    internal void WriteValue(Vector3D value)
    {
        Compositor.VerifyAccess();
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_value != value)
            SetValue(ValueProperty, ref _value, value);
    }

    public override CompositionProperty? GetCompositionProperty(string fieldName)
        => fieldName == "Value" ? ValueProperty : base.GetCompositionProperty(fieldName);

    public void Dispose()
    {
        Compositor.VerifyAccess();
        _disposed = true;
    }
}

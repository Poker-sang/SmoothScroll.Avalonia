using Avalonia.Controls;

namespace SmoothScroll.Avalonia.Controls;

/// <summary>
/// Provides data for the <see cref="ScrollView.AnchorRequested"/> event.
/// </summary>
public sealed class ScrollingAnchorRequestedEventArgs : EventArgs
{
    internal ScrollingAnchorRequestedEventArgs(IEnumerable<Control> anchorCandidates)
    {
        AnchorCandidates = Array.AsReadOnly(anchorCandidates.ToArray());
    }

    /// <summary>
    /// Gets an immutable snapshot of the registered, visible candidates eligible for the current selection.
    /// </summary>
    public IReadOnlyList<Control> AnchorCandidates { get; }

    /// <summary>
    /// Gets or sets the candidate to use instead of automatic ratio-based selection.
    /// </summary>
    /// <remarks>
    /// The requested element is used only when it remains in <see cref="AnchorCandidates"/>, remains registered,
    /// visible, and inside the presenter. Otherwise the presenter falls back to automatic selection.
    /// </remarks>
    public Control? AnchorElement { get; set; }
}

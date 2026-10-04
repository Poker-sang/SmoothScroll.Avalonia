using Avalonia.Rendering.Composition;
using Avalonia.Threading;
using SmoothScroll.Avalonia.Interaction;
using SmoothScroll.Avalonia.Interaction.Experimental;

namespace SmoothScroll.Avalonia.Controls;

public partial class ScrollPresenter
{
    private ExperimentalAttachment? _experimentalAttachment;
    private CompositionVector3Output? _experimentalContentTranslation;
    private bool _experimentalPresenterLoaded;

    /// <summary>Whether the presenter is using physical content offsets rather than logical item offsets.</summary>
    public bool IsPhysicalScrollActive => !IsLogicalScrollActive;

    /// <summary>
    /// Attaches a compositor participant when this presenter is loaded and physically scrolling.
    /// The returned registration owns the attachment across reloads and logical mode changes.
    /// The callback and its returned disposal run on the UI thread. Detach the participant before disposing outputs.
    /// </summary>
    public IDisposable AttachExperimentalVerticalScroll(Func<InteractionTracker, IDisposable> attach)
    {
        Dispatcher.UIThread.VerifyAccess();
        ArgumentNullException.ThrowIfNull(attach);
        if (_experimentalAttachment is not null)
            throw new InvalidOperationException("A vertical scroll attachment is already registered.");
        var registration = new ExperimentalAttachment(this, attach);
        _experimentalAttachment = registration;
        UpdateExperimentalAttachment();
        return registration;
    }

    /// <summary>Adds a compositor-owned translation to the existing content scroll expression.</summary>
    public void ConfigureExperimentalContentTranslation(CompositionVector3Output? output)
    {
        Dispatcher.UIThread.VerifyAccess();
        if (output is not null && _interactionTracker is not null && output.Compositor != _interactionTracker.Compositor)
            throw new ArgumentException("The output must belong to the presenter's compositor.", nameof(output));
        if (ReferenceEquals(output, _experimentalContentTranslation))
            return;
        _experimentalContentTranslation = output;
        ClearScrollAnimation(GetCompositionVisual());
        if (_experimentalPresenterLoaded)
            EnsureScrollAnimation();
    }

    private void UpdateExperimentalAttachment()
    {
        if (_experimentalPresenterLoaded && IsPhysicalScrollActive && _interactionTracker is not null)
            _experimentalAttachment?.Attach(_interactionTracker);
        else
            _experimentalAttachment?.Detach();
        UpdateInteractionOptions();
    }

    private sealed class ExperimentalAttachment(ScrollPresenter owner,
        Func<InteractionTracker, IDisposable> attach) : IDisposable
    {
        private InteractionTracker? _tracker;
        private IDisposable? _attachment;
        internal bool IsAttached => _attachment is not null;

        internal void Attach(InteractionTracker tracker)
        {
            if (ReferenceEquals(tracker, _tracker))
                return;
            Detach();
            _tracker = tracker;
            _attachment = attach(tracker);
        }

        internal void Detach()
        {
            _attachment?.Dispose();
            _attachment = null;
            _tracker = null;
        }

        public void Dispose()
        {
            Dispatcher.UIThread.VerifyAccess();
            if (!ReferenceEquals(owner._experimentalAttachment, this))
                return;
            Detach();
            owner._experimentalAttachment = null;
            owner.UpdateInteractionOptions();
        }
    }
}

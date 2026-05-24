namespace Ted;

/// <summary>Event args for <see cref="MarkdownPreview.SourceLineClicked" />.</summary>
internal sealed class SourceLineClickedEventArgs : EventArgs
{
    public SourceLineClickedEventArgs (int sourceLine)
    {
        SourceLine = sourceLine;
    }

    /// <summary>Gets the estimated 0-based source line number that was clicked.</summary>
    public int SourceLine { get; }
}

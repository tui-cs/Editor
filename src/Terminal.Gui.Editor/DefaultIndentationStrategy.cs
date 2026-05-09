using Terminal.Gui.Text.Document;

namespace Terminal.Gui.Views;

/// <summary>Default indentation strategy that copies leading whitespace from the previous line.</summary>
public sealed class DefaultIndentationStrategy : IIndentationStrategy
{
    /// <summary>Shared stateless instance.</summary>
    public static DefaultIndentationStrategy Instance { get; } = new ();

    private DefaultIndentationStrategy ()
    {
    }

    /// <inheritdoc />
    public string GetIndentationForNewLine (TextDocument document, DocumentLine previousLine)
    {
        ArgumentNullException.ThrowIfNull (document);
        ArgumentNullException.ThrowIfNull (previousLine);

        ISegment segment = TextUtilities.GetLeadingWhitespace (document, previousLine);

        return document.GetText (segment);
    }
}

using Terminal.Gui.Text.Document;

namespace Terminal.Gui.Views;

/// <summary>Computes indentation inserted when the editor creates a new line.</summary>
public interface IIndentationStrategy
{
    /// <summary>Gets the indentation text to insert after a newline following <paramref name="previousLine" />.</summary>
    string GetIndentationForNewLine (TextDocument document, DocumentLine previousLine);
}

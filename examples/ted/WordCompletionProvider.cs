using Terminal.Gui.Document;
using Terminal.Gui.Editor.Completion;
using Terminal.Gui.Input;

namespace Ted;

/// <summary>
///     A trivial word-completion provider for the <c>ted</c> demo. Scans the document for unique
///     word tokens (letters, digits, underscores) and offers them as suggestions when the prefix
///     matches. Triggered by <c>Ctrl+Space</c>.
/// </summary>
internal sealed class WordCompletionProvider : IEditorCompletionProvider
{
    /// <inheritdoc />
    public IReadOnlyList<CompletionItem> GetCompletions (TextDocument document, int caretOffset, string prefix)
    {
        if (string.IsNullOrEmpty (prefix))
        {
            return [];
        }

        var text = document.Text;
        HashSet<string> seen = new (StringComparer.OrdinalIgnoreCase);
        List<CompletionItem> results = [];

        // Walk the document text for word tokens.
        var i = 0;

        while (i < text.Length)
        {
            if (!IsWordChar (text[i]))
            {
                i++;

                continue;
            }

            var start = i;

            while (i < text.Length && IsWordChar (text[i]))
            {
                i++;
            }

            var word = text.Substring (start, i - start);

            // Skip the exact prefix and short tokens.
            if (word.Length <= prefix.Length || string.Equals (word, prefix, StringComparison.Ordinal))
            {
                continue;
            }

            if (word.StartsWith (prefix, StringComparison.OrdinalIgnoreCase) && seen.Add (word))
            {
                results.Add (new CompletionItem { Label = word });
            }
        }

        results.Sort ((a, b) => string.Compare (a.Label, b.Label, StringComparison.OrdinalIgnoreCase));

        return results;
    }

    /// <inheritdoc />
    public bool ShouldTrigger (Key key)
    {
        return key == Key.Space.WithCtrl;
    }

    private static bool IsWordChar (char ch)
    {
        return char.IsLetterOrDigit (ch) || ch == '_';
    }
}

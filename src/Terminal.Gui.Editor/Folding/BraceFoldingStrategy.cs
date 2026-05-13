namespace Terminal.Gui.Document.Folding;

/// <summary>
///     Detects foldable regions based on matching brace pairs (<c>{}</c>). Useful as a
///     language-agnostic folding strategy for C-family languages, JSON, etc.
/// </summary>
public class BraceFoldingStrategy
{
    /// <summary>Gets or sets the opening brace character. Defaults to <c>{</c>.</summary>
    public char OpeningBrace { get; set; } = '{';

    /// <summary>Gets or sets the closing brace character. Defaults to <c>}</c>.</summary>
    public char ClosingBrace { get; set; } = '}';

    /// <summary>
    ///     Creates <see cref="NewFolding" />s for the specified document and updates the folding manager.
    /// </summary>
    public void UpdateFoldings (FoldingManager manager, TextDocument document)
    {
        IEnumerable<NewFolding> foldings = CreateNewFoldings (document, out var firstErrorOffset);
        manager.UpdateFoldings (foldings, firstErrorOffset);
    }

    /// <summary>
    ///     Creates <see cref="NewFolding" />s for the specified document by scanning for brace pairs.
    /// </summary>
    public IEnumerable<NewFolding> CreateNewFoldings (TextDocument document, out int firstErrorOffset)
    {
        firstErrorOffset = -1;
        List<NewFolding> newFoldings = new ();
        Stack<int> startOffsets = new ();
        var lastNewLineOffset = 0;

        for (var i = 0; i < document.TextLength; i++)
        {
            var c = document.GetCharAt (i);

            if (c == '\n' || c == '\r')
            {
                lastNewLineOffset = i + 1;
            }
            else if (c == OpeningBrace)
            {
                startOffsets.Push (i);
            }
            else if (c == ClosingBrace && startOffsets.Count > 0)
            {
                var startOffset = startOffsets.Pop ();

                // Only create a folding if it spans multiple lines.
                if (startOffset < lastNewLineOffset)
                {
                    newFoldings.Add (new NewFolding (startOffset, i + 1));
                }
            }
        }

        newFoldings.Sort (static (a, b) => a.StartOffset.CompareTo (b.StartOffset));

        return newFoldings;
    }
}

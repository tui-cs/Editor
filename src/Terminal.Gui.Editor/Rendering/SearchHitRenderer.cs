using System.Drawing;
using Terminal.Gui.Document;
using Terminal.Gui.Document.Search;
using Terminal.Gui.Drawing;
using Terminal.Gui.ViewBase;
using Attribute = Terminal.Gui.Drawing.Attribute;

namespace Terminal.Gui.Editor.Rendering;

/// <summary>
///     Paints all <see cref="ISearchStrategy" /> matches in the visible viewport using
///     <see cref="VisualRole.Highlight" />. Registered/unregistered automatically by
///     <see cref="Editor.SearchStrategy" />.
/// </summary>
public sealed class SearchHitRenderer : IBackgroundRenderer
{
    // Cache: avoids re-running FindAll every draw pass when nothing changed.
    private ITextSourceVersion? _cachedVersion;
    private ISearchStrategy? _cachedStrategy;
    private int _cachedViewportStart;
    private int _cachedViewportEnd;
    private ISearchResult[] _cachedHits = [];

    /// <inheritdoc />
    public void Draw (View host, CellVisualLine line, int row, Rectangle viewport)
    {
        if (host is not Editor editor)
        {
            return;
        }

        ISearchStrategy? strategy = editor.SearchStrategy;
        TextDocument? document = editor.Document;

        if (strategy is null || document is null || document.TextLength == 0)
        {
            return;
        }

        // Compute the document-offset range for the visible viewport (all visible lines).
        DocumentLine docLine = line.DocumentLine;
        var lineStart = docLine.Offset;
        var lineEnd = docLine.EndOffset;

        // Get matches for the visible viewport (lazily cached across lines within the same draw pass).
        ISearchResult[] hits = GetHits (editor, strategy, document);

        if (hits.Length == 0)
        {
            return;
        }

        Attribute highlight = host.GetAttributeForRole (VisualRole.Highlight);

        // Selection range — selection wins visually over hit highlight.
        var hasSelection = editor.HasSelection;
        var selStart = hasSelection ? editor.SelectionStart : 0;
        var selEnd = hasSelection ? editor.SelectionEnd : 0;

        foreach (ISearchResult hit in hits)
        {
            // Skip hits that don't intersect this line.
            if (hit.Offset >= lineEnd || hit.Offset + hit.Length <= lineStart)
            {
                continue;
            }

            // Clamp hit range to this line.
            var hitStart = Math.Max (hit.Offset, lineStart);
            var hitEnd = Math.Min (hit.Offset + hit.Length, lineEnd);

            // Walk elements and override their attribute for the hit range.
            foreach (CellVisualLineElement element in line.Elements)
            {
                var elemStart = element.DocumentOffset;
                var elemEnd = element.DocumentEndOffset;

                // Skip elements outside the hit range.
                if (elemEnd <= hitStart || elemStart >= hitEnd)
                {
                    continue;
                }

                // If selection covers this element entirely, selection wins.
                if (hasSelection && elemStart >= selStart && elemEnd <= selEnd)
                {
                    continue;
                }

                element.Attribute = highlight;
            }
        }
    }

    /// <summary>Invalidates the cached hits, forcing a re-query on next draw.</summary>
    internal void Invalidate ()
    {
        _cachedVersion = null;
        _cachedHits = [];
    }

    private ISearchResult[] GetHits (Editor editor, ISearchStrategy strategy, TextDocument document)
    {
        // Determine the viewport document range from the editor's visible lines.
        List<int> visibleLines = editor.GetVisibleLineNumbers ();
        Rectangle viewport = editor.Viewport;
        var firstVisibleIndex = viewport.Y;
        var lastVisibleIndex = viewport.Y + viewport.Height - 1;

        if (firstVisibleIndex < 0 || firstVisibleIndex >= visibleLines.Count)
        {
            return [];
        }

        lastVisibleIndex = Math.Min (lastVisibleIndex, visibleLines.Count - 1);

        var vpStartOffset = document.GetLineByNumber (visibleLines[firstVisibleIndex]).Offset;
        var vpEndOffset = document.GetLineByNumber (visibleLines[lastVisibleIndex]).EndOffset;

        ITextSourceVersion currentVersion = document.Version;

        if (ReferenceEquals (_cachedVersion, currentVersion)
            && ReferenceEquals (_cachedStrategy, strategy)
            && _cachedViewportStart == vpStartOffset
            && _cachedViewportEnd == vpEndOffset)
        {
            return _cachedHits;
        }

        // Materialize hits for the visible range.
        var length = vpEndOffset - vpStartOffset;

        _cachedHits = length > 0
            ? strategy.FindAll (document, vpStartOffset, length).ToArray ()
            : [];

        _cachedVersion = currentVersion;
        _cachedStrategy = strategy;
        _cachedViewportStart = vpStartOffset;
        _cachedViewportEnd = vpEndOffset;

        return _cachedHits;
    }
}

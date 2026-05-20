using Terminal.Gui.Drawing;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace Ted;

public sealed partial class TedApp
{
    private Markdown? _markdownPreview;
    private bool _syncingScroll;

    /// <summary>Toggle state used by the View menu item that shows or hides the Markdown preview pane.</summary>
    public CheckBox PreviewCheckBox { get; } = new ()
    {
        AllowCheckStateNone = false,
        CanFocus = false,
        Text = "_Preview",
        Value = CheckState.UnChecked,
        Visible = false
    };

    /// <summary>Gets whether the current file is a Markdown file.</summary>
    private bool IsMarkdownFile =>
        CurrentFilePath is not null
        && Path.GetExtension (CurrentFilePath).Equals (".md", StringComparison.OrdinalIgnoreCase);

    /// <summary>Shows or hides the <see cref="PreviewCheckBox" /> based on the current file extension.</summary>
    internal void UpdatePreviewVisibility ()
    {
        var isMd = IsMarkdownFile;
        PreviewCheckBox.Visible = isMd;
        _previewMarkdownMenuItem.Enabled = isMd;

        if (!isMd && _markdownPreview is not null)
        {
            HideMarkdownPreview ();
            PreviewCheckBox.Value = CheckState.UnChecked;
        }
        else if (isMd && _markdownPreview is not null)
        {
            // Document was swapped while preview was open — refresh content and re-hook.
            RefreshPreviewDocument ();
        }

        _previewMarkdownMenuItem.Title = ToggleTitle (
            PreviewCheckBox.Value == CheckState.Checked,
            "_Preview Markdown");
    }

    private void ToggleMarkdownPreview ()
    {
        if (PreviewCheckBox.Value == CheckState.Checked)
        {
            ShowMarkdownPreview ();
        }
        else
        {
            HideMarkdownPreview ();
        }
    }

    private void ShowMarkdownPreview ()
    {
        if (_markdownPreview is not null)
        {
            return;
        }

        _markdownPreview = new Markdown
        {
            X = Pos.Right (Editor),
            Y = Editor.Y,
            Width = Dim.Fill (),
            Height = Editor.Height,
            Text = Editor.Document?.Text ?? string.Empty,
            ViewportSettings = ViewportSettingsFlags.HasScrollBars,
            SyntaxHighlighter = new TextMateSyntaxHighlighter ()
        };

        // Editor takes the left half, preview takes the right half.
        Editor.Width = Dim.Percent (50);

        Add (_markdownPreview);

        // Sync scrolling bidirectionally.
        Editor.ViewportChanged += OnEditorViewportChanged;
        _markdownPreview.ViewportChanged += OnPreviewViewportChanged;

        // Update preview when document content changes.
        if (Editor.Document is not null)
        {
            Editor.Document.Changed += OnDocumentChangedForPreview;
        }
    }

    private void HideMarkdownPreview ()
    {
        if (_markdownPreview is null)
        {
            return;
        }

        Editor.ViewportChanged -= OnEditorViewportChanged;
        _markdownPreview.ViewportChanged -= OnPreviewViewportChanged;

        if (Editor.Document is not null)
        {
            Editor.Document.Changed -= OnDocumentChangedForPreview;
        }

        Remove (_markdownPreview);
        _markdownPreview.Dispose ();
        _markdownPreview = null;

        // Restore editor to full width.
        Editor.Width = Dim.Fill ();
    }

    private void OnEditorViewportChanged (object? sender, DrawEventArgs e)
    {
        if (_markdownPreview is null || _syncingScroll)
        {
            return;
        }

        _syncingScroll = true;

        try
        {
            // Synchronise vertical scroll position. The Markdown view's content size may differ
            // from the editor's, so we use a proportional mapping.
            var editorContentHeight = Editor.GetContentSize ().Height;
            var editorViewportHeight = Editor.Viewport.Height;
            var maxEditorY = Math.Max (0, editorContentHeight - editorViewportHeight);
            var editorY = Editor.Viewport.Y;

            var previewContentHeight = _markdownPreview.GetContentSize ().Height;
            var previewViewportHeight = _markdownPreview.Viewport.Height;
            var maxPreviewY = Math.Max (0, previewContentHeight - previewViewportHeight);

            var newY = maxEditorY > 0
                ? (int)((long)editorY * maxPreviewY / maxEditorY)
                : 0;

            _markdownPreview.Viewport = _markdownPreview.Viewport with { Y = Math.Clamp (newY, 0, maxPreviewY) };
        }
        finally
        {
            _syncingScroll = false;
        }
    }

    private void OnPreviewViewportChanged (object? sender, DrawEventArgs e)
    {
        if (_markdownPreview is null || _syncingScroll)
        {
            return;
        }

        _syncingScroll = true;

        try
        {
            var previewContentHeight = _markdownPreview.GetContentSize ().Height;
            var previewViewportHeight = _markdownPreview.Viewport.Height;
            var maxPreviewY = Math.Max (0, previewContentHeight - previewViewportHeight);
            var previewY = _markdownPreview.Viewport.Y;

            var editorContentHeight = Editor.GetContentSize ().Height;
            var editorViewportHeight = Editor.Viewport.Height;
            var maxEditorY = Math.Max (0, editorContentHeight - editorViewportHeight);

            var newY = maxPreviewY > 0
                ? (int)((long)previewY * maxEditorY / maxPreviewY)
                : 0;

            Editor.Viewport = Editor.Viewport with { Y = Math.Clamp (newY, 0, maxEditorY) };
        }
        finally
        {
            _syncingScroll = false;
        }
    }

    private void OnDocumentChangedForPreview (object? sender, EventArgs e)
    {
        if (_markdownPreview is null)
        {
            return;
        }

        _markdownPreview.Text = Editor.Document?.Text ?? string.Empty;
    }

    /// <summary>
    ///     Re-hooks the document change handler and refreshes the preview content after a document swap.
    /// </summary>
    private void RefreshPreviewDocument ()
    {
        if (_markdownPreview is null)
        {
            return;
        }

        // Detach from the current document first to avoid duplicate subscriptions when
        // RefreshPreviewDocument is called multiple times for the same document instance
        // (e.g. Save As that keeps the .md extension).
        if (Editor.Document is not null)
        {
            Editor.Document.Changed -= OnDocumentChangedForPreview;
            Editor.Document.Changed += OnDocumentChangedForPreview;
        }

        _markdownPreview.Text = Editor.Document?.Text ?? string.Empty;
    }
}

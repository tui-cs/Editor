using Terminal.Gui.Document;
using Terminal.Gui.Highlighting;
using Terminal.Gui.Resources;
using Terminal.Gui.Views;

namespace Ted;

public sealed partial class TedApp
{
    private const long StreamingStatusInterval = 256 * 1024;
    private const int StreamingStatusMilliseconds = 100;
    private long _lastStreamingStatusUnits;
    private DateTime _lastStreamingStatusUpdate = DateTime.MinValue;

    /// <summary>The path currently associated with <see cref="Editor" />, or <see langword="null" /> for an untitled buffer.</summary>
    public string? CurrentFilePath { get; private set; }

    /// <summary>Dialog hook used by <see cref="OpenFile" />. Tests can replace it to avoid interactive UI.</summary>
    public Func<string?> ShowOpenDialog { get; set; }

    /// <summary>Dialog hook used by <see cref="SaveFileAs" />. Tests can replace it to avoid interactive UI.</summary>
    public Func<string?> ShowSaveDialog { get; set; }

    /// <summary>Dialog hook used by <see cref="QuitFile" />. Tests can replace it to avoid interactive UI.</summary>
    public Func<SaveChangesChoice> ShowSaveChangesDialog { get; set; }

    /// <summary>File read hook retained for source compatibility. Streaming opens use <see cref="OpenRead" />.</summary>
    public Func<string, string> ReadAllText { get; set; } = File.ReadAllText;

    /// <summary>File stream hook used by <see cref="OpenFile" />. Tests can replace it with an in-memory fake.</summary>
    public Func<string, Stream> OpenRead { get; set; } = File.OpenRead;

    /// <summary>
    ///     File write hook retained for source compatibility. Streaming saves use <see cref="CreateWrite" />.
    /// </summary>
    public Action<string, string> WriteAllText { get; set; } = File.WriteAllText;

    /// <summary>File stream hook used by <see cref="SaveFile" /> and <see cref="SaveFileAs" />.</summary>
    public Func<string, Stream> CreateWrite { get; set; } = path => File.Create (path);

    /// <summary>The currently running background load, if any.</summary>
    public Task<bool>? CurrentLoadTask { get; private set; }

    /// <summary>Gets whether the current editor document has unsaved changes.</summary>
    public bool IsDocumentModified => Editor.Document?.UndoStack.IsOriginalFile == false;

    /// <summary>Clears the editor and makes the buffer untitled.</summary>
    public void NewFile ()
    {
        SetDocument (string.Empty, null);
    }

    /// <summary>Prompts for a file path, then loads that file into the editor.</summary>
    public bool OpenFile ()
    {
        var filePath = ShowOpenDialog ();

        return !string.IsNullOrWhiteSpace (filePath) && OpenFileAsync (filePath).GetAwaiter ().GetResult ();
    }

    /// <summary>Prompts for a file path, then asynchronously streams that file into the editor.</summary>
    public async Task<bool> OpenFileAsync (CancellationToken cancellationToken = default)
    {
        var filePath = ShowOpenDialog ();

        if (string.IsNullOrWhiteSpace (filePath))
        {
            return false;
        }

        return await OpenFileAsync (filePath, false, cancellationToken);
    }

    /// <summary>Opens a CLI-requested missing file path as an empty, modified document bound to that path.</summary>
    public void OpenMissingFile (string filePath)
    {
        if (string.IsNullOrWhiteSpace (filePath))
        {
            throw new ArgumentException ("Path must not be null, empty, or whitespace.", nameof (filePath));
        }

        SetDocument (string.Empty, filePath);
        TextDocument document = Editor.Document
                                ?? throw new InvalidOperationException (
                                    "ted cannot open a missing file because the editor has no document.");
        document.UndoStack.DiscardOriginalFileMarker ();
    }

    /// <summary>Saves the editor text to the current file, or prompts for a path if the buffer is untitled.</summary>
    public bool SaveFile ()
    {
        return CurrentFilePath is null ? SaveFileAs () : SaveFileAsync ().GetAwaiter ().GetResult ();
    }

    /// <summary>Asynchronously streams the editor text to the current file, or prompts for a path if untitled.</summary>
    public Task<bool> SaveFileAsync (CancellationToken cancellationToken = default)
    {
        return SaveFileAsync (false, cancellationToken);
    }

    private async Task<bool> SaveFileAsync (bool marshalToApp, CancellationToken cancellationToken = default)
    {
        if (CurrentFilePath is null)
        {
            return await SaveFileAsAsync (marshalToApp, cancellationToken);
        }

        await SaveFileToAsync (CurrentFilePath, marshalToApp, cancellationToken);

        return true;
    }

    /// <summary>Prompts for a file path, then saves the editor text to that path.</summary>
    public bool SaveFileAs ()
    {
        var filePath = ShowSaveDialog ();

        return !string.IsNullOrWhiteSpace (filePath) && SaveFileAsAsync (filePath).GetAwaiter ().GetResult ();
    }

    /// <summary>Prompts for a file path, then asynchronously streams the editor text to that path.</summary>
    public Task<bool> SaveFileAsAsync (CancellationToken cancellationToken = default)
    {
        return SaveFileAsAsync (false, cancellationToken);
    }

    private async Task<bool> SaveFileAsAsync (bool marshalToApp, CancellationToken cancellationToken = default)
    {
        var filePath = ShowSaveDialog ();

        if (string.IsNullOrWhiteSpace (filePath))
        {
            return false;
        }

        return await SaveFileAsAsync (filePath, marshalToApp, cancellationToken);
    }

    /// <summary>Quits ted, prompting to save first when the current document has unsaved changes.</summary>
    public bool QuitFile ()
    {
        if (!ConfirmSaveChanges ())
        {
            return false;
        }

        RequestStop ();

        return true;
    }

    internal void SetDocument (string text, string? filePath)
    {
        Editor.ClearSelection ();
        Editor.Document = new TextDocument (text);
        Editor.CaretOffset = 0;
        CurrentFilePath = filePath;

        ApplyFileMetadata (filePath);
    }

    private string? ShowDefaultOpenDialog ()
    {
        using OpenDialog dialog = new ();
        dialog.AllowsMultipleSelection = false;
        dialog.MustExist = true;
        dialog.OpenMode = OpenMode.File;

        if (App is null)
        {
            throw new InvalidOperationException ("ted must be running before showing the open dialog.");
        }

        App.Run (dialog);

        return dialog.FilePaths.FirstOrDefault ();
    }

    private string? ShowDefaultSaveDialog ()
    {
        using SaveDialog dialog = new ();
        dialog.AllowsMultipleSelection = false;
        dialog.OpenMode = OpenMode.File;

        if (App is null)
        {
            throw new InvalidOperationException ("ted must be running before showing the save dialog.");
        }

        App.Run (dialog);

        return dialog.FileName;
    }

    private SaveChangesChoice ShowDefaultSaveChangesDialog ()
    {
        if (App is null)
        {
            throw new InvalidOperationException ("ted must be running before showing the save-changes dialog.");
        }

        var result = MessageBox.Query (
            App,
            "Save changes?",
            "The document has unsaved changes. Save before quitting?",
            Strings.btnCancel,
            "Do_n't Save",
            Strings.btnSave);

        return result switch
        {
            1 => SaveChangesChoice.Discard,
            2 => SaveChangesChoice.Save,
            _ => SaveChangesChoice.Cancel
        };
    }

    private void New ()
    {
        if (!ConfirmSaveChanges ())
        {
            return;
        }

        NewFile ();
    }

    private void Open ()
    {
        if (!ConfirmSaveChanges ())
        {
            return;
        }

        var filePath = ShowOpenDialog ();

        if (string.IsNullOrWhiteSpace (filePath))
        {
            return;
        }

        CurrentLoadTask = OpenFileAsync (filePath, true);
    }

    private void Save ()
    {
        _ = SaveFileAsync (true);
    }

    private void SaveAs ()
    {
        _ = SaveFileAsAsync (true);
    }

    private void Quit ()
    {
        QuitFile ();
    }

    private bool ConfirmSaveChanges ()
    {
        if (!IsDocumentModified)
        {
            return true;
        }

        return ShowSaveChangesDialog () switch
        {
            SaveChangesChoice.Discard => true,
            SaveChangesChoice.Save => SaveFile (),
            _ => false
        };
    }

    private async Task<bool> OpenFileAsync (
        string filePath,
        bool marshalToApp = false,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using Stream stream = OpenRead (filePath);
            var fileSize = GetStreamLength (stream);
            ResetStreamingStatusThrottle ();
            SetLoadStatus (FormatStartingProgress ("Loading", fileSize), true);

            IProgress<TextDocumentProgress> progress = new Progress<TextDocumentProgress> (ReportLoadProgress);
            Editor.Document?.SetOwnerThread (null);
            TextDocument document =
                await Task.Run (
                    () => TextDocument.LoadAsync (stream, progress: progress, cancellationToken: cancellationToken),
                    cancellationToken);

            void ApplyDocument ()
            {
                ApplyLoadedDocument (document, filePath);
                SetLoadStatus (FormatCompletedProgress ("Loaded", fileSize), false);
            }

            if (marshalToApp)
            {
                await InvokeOnAppAsync (ApplyDocument);
            }
            else
            {
                ApplyDocument ();
            }

            if (App is null)
            {
                document.SetOwnerThread (null);
            }

            return true;
        }
        catch (OperationCanceledException)
        {
            SetLoadStatus ("Load canceled", false);

            return false;
        }
    }

    private void ApplyLoadedDocument (TextDocument document, string filePath)
    {
        document.SetOwnerThread (Thread.CurrentThread);
        Editor.ClearSelection ();
        Editor.Document = document;
        Editor.CaretOffset = 0;
        CurrentFilePath = filePath;

        ApplyFileMetadata (filePath);
    }

    private async Task<bool> SaveFileAsAsync (
        string filePath,
        bool marshalToApp = false,
        CancellationToken cancellationToken = default)
    {
        CurrentFilePath = filePath;
        await SaveFileToAsync (filePath, marshalToApp, cancellationToken);
        UpdateFileNameShortcut ();
        UpdatePreviewVisibility ();

        return true;
    }

    private async Task SaveFileToAsync (string filePath, bool marshalToApp, CancellationToken cancellationToken)
    {
        await using Stream stream = CreateWrite (filePath);
        ResetStreamingStatusThrottle ();
        SetLoadStatus (FormatStartingProgress ("Saving", null), true);

        IProgress<TextDocumentProgress> progress = new Progress<TextDocumentProgress> (ReportSaveProgress);
        await Editor.SaveAsync (stream, progress, cancellationToken);
        var fileSize = GetStreamLength (stream);

        void MarkSaved ()
        {
            Editor.Document!.UndoStack.MarkAsOriginalFile ();
            SetLoadStatus (FormatCompletedProgress ("Saved", fileSize), false);
        }

        if (marshalToApp)
        {
            await InvokeOnAppAsync (MarkSaved);
        }
        else
        {
            MarkSaved ();
        }
    }

    private void ApplyFileMetadata (string? filePath)
    {
        IHighlightingDefinition? def = null;

        if (filePath is not null)
        {
            var ext = Path.GetExtension (filePath);

            if (!string.IsNullOrEmpty (ext))
            {
                def = HighlightingManager.Instance.GetDefinitionByExtension (ext);
            }
        }

        Editor.HighlightingDefinition = def;
        LanguageShortcut.Title = def?.Name ?? "Plain Text";

        UpdateFileNameShortcut ();
        UpdatePreviewVisibility ();
        InstallFolding ();
        Editor.SetNeedsDraw ();
    }

    private void ReportLoadProgress (TextDocumentProgress progress)
    {
        if (!ShouldReportStreamingProgress (progress))
        {
            return;
        }

        SetLoadStatus (FormatProgress ("Loading", progress), true);
    }

    private void ReportSaveProgress (TextDocumentProgress progress)
    {
        if (!ShouldReportStreamingProgress (progress))
        {
            return;
        }

        SetLoadStatus (FormatProgress ("Saving", progress), true);
    }

    private void SetLoadStatus (string status, bool showSpinner)
    {
        void Update ()
        {
            // The status spinner is visible only while it is actively spinning.
            LoadStatusSpinner.Visible = showSpinner;
            LoadStatusSpinner.AutoSpin = showSpinner;
            LoadSpinnerShortcut.HelpText = status;
            LoadStatusSpinner.SetNeedsDraw ();
            LoadSpinnerShortcut.SetNeedsDraw ();
        }

        if (App is null)
        {
            Update ();

            return;
        }

        App.Invoke (Update);
    }

    private void ResetStreamingStatusThrottle ()
    {
        _lastStreamingStatusUpdate = DateTime.MinValue;
        _lastStreamingStatusUnits = 0;
    }

    private bool ShouldReportStreamingProgress (TextDocumentProgress progress)
    {
        var units = progress.BytesProcessed ?? progress.CharactersProcessed;
        var totalUnits = progress.TotalBytes ?? progress.TotalCharacters;

        if (totalUnits == units)
        {
            return true;
        }

        DateTime now = DateTime.UtcNow;

        if (units - _lastStreamingStatusUnits < StreamingStatusInterval
            && now - _lastStreamingStatusUpdate < TimeSpan.FromMilliseconds (StreamingStatusMilliseconds))
        {
            return false;
        }

        _lastStreamingStatusUnits = units;
        _lastStreamingStatusUpdate = now;

        return true;
    }

    private Task InvokeOnAppAsync (Action action)
    {
        if (App is null)
        {
            action ();

            return Task.CompletedTask;
        }

        TaskCompletionSource completion = new ();
        App.Invoke (() =>
        {
            try
            {
                action ();
                completion.SetResult ();
            }
            catch (Exception ex)
            {
                completion.SetException (ex);
            }
        });

        return completion.Task;
    }

    private static string FormatProgress (string verb, TextDocumentProgress progress)
    {
        var processed = progress.BytesProcessed is { } bytesProcessed
            ? FormatByteCount (bytesProcessed)
            : $"{progress.CharactersProcessed:N0} chars";

        var total = progress.TotalBytes is { } totalBytes
            ? FormatByteCount (totalBytes)
            : progress.TotalCharacters is { } totalCharacters
                ? $"{totalCharacters:N0} chars"
                : null;

        if (total is null)
        {
            return $"{verb} {processed}";
        }

        if (progress.Fraction is { } fraction)
        {
            return $"{verb} {processed} of {total} ({fraction:P0})";
        }

        return $"{verb} {processed} of {total}";
    }

    private static string FormatStartingProgress (string verb, long? totalBytes)
    {
        return totalBytes is { } bytes
            ? $"{verb} 0 B of {FormatByteCount (bytes)}"
            : $"{verb} 0 B";
    }

    private static string FormatCompletedProgress (string verb, long? totalBytes)
    {
        return totalBytes is { } bytes
            ? $"{verb} {FormatByteCount (bytes)}"
            : verb;
    }

    private static long? GetStreamLength (Stream stream)
    {
        if (!stream.CanSeek)
        {
            return null;
        }

        return stream.Length;
    }

    private static string FormatByteCount (long bytes)
    {
        string[] units = ["B", "KiB", "MiB", "GiB", "TiB"];
        double value = bytes;
        var unitIndex = 0;

        while (value >= 1024 && unitIndex < units.Length - 1)
        {
            value /= 1024;
            unitIndex++;
        }

        var format = unitIndex == 0 ? "N0" : "N1";

        return $"{value.ToString (format)} {units[unitIndex]}";
    }
}

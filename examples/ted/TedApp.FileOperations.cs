using System.Text;
using Terminal.Gui.Document;
using Terminal.Gui.Highlighting;
using Terminal.Gui.Resources;
using Terminal.Gui.Views;

namespace Ted;

public sealed partial class TedApp
{
    /// <summary>Minimum byte/character delta between queued streaming status updates.</summary>
    private const long StreamingStatusInterval = 256 * 1024;

    /// <summary>Minimum elapsed milliseconds between queued streaming status updates.</summary>
    private const int StreamingStatusMilliseconds = 100;

    private readonly Lock _streamingStatusLock = new ();
    private long _lastStreamingStatusUnits;
    private DateTime _lastStreamingStatusUpdate = DateTime.MinValue;
    private long _streamingStatusOperationId;

    /// <summary>The path currently associated with <see cref="Editor" />, or <see langword="null" /> for an untitled buffer.</summary>
    public string? CurrentFilePath { get; private set; }

    /// <summary>Dialog hook used by <see cref="OpenFile" />. Tests can replace it to avoid interactive UI.</summary>
    public Func<string?> ShowOpenDialog { get; set; }

    /// <summary>Dialog hook used by <see cref="SaveFileAs" />. Tests can replace it to avoid interactive UI.</summary>
    public Func<string?> ShowSaveDialog { get; set; }

    /// <summary>Dialog hook used by <see cref="QuitFile" />. Tests can replace it to avoid interactive UI.</summary>
    public Func<SaveChangesChoice> ShowSaveChangesDialog { get; set; }

    private bool _openReadWasSet;

    private Func<string, string> _readAllText = File.ReadAllText;

    /// <summary>
    ///     File read hook retained for source compatibility. Replacing this hook adapts opens by buffering the
    ///     returned text as UTF-8; prefer <see cref="OpenRead" /> for streaming large files.
    /// </summary>
    public Func<string, string> ReadAllText
    {
        get => _readAllText;
        set
        {
            _readAllText = value ?? throw new ArgumentNullException (nameof (value));

            if (!_openReadWasSet)
            {
                _openRead = OpenReadFromReadAllText;
            }
        }
    }

    private Func<string, Stream> _openRead = File.OpenRead;

    /// <summary>File stream hook used by <see cref="OpenFile" />. Tests can replace it with an in-memory fake.</summary>
    public Func<string, Stream> OpenRead
    {
        get => _openRead;
        set
        {
            _openRead = value ?? throw new ArgumentNullException (nameof (value));
            _openReadWasSet = true;
        }
    }

    private bool _createWriteWasSet;

    private Action<string, string> _writeAllText = File.WriteAllText;

    /// <summary>
    ///     File write hook retained for source compatibility. Streaming saves use <see cref="CreateWrite" />.
    /// </summary>
    public Action<string, string> WriteAllText
    {
        get => _writeAllText;
        set
        {
            _writeAllText = value ?? throw new ArgumentNullException (nameof (value));

            if (!_createWriteWasSet)
            {
                _createWrite = CreateWriteFromWriteAllText;
            }
        }
    }

    private Func<string, Stream> _createWrite = path => File.Create (path);

    /// <summary>File stream hook used by <see cref="SaveFile" /> and <see cref="SaveFileAs" />.</summary>
    public Func<string, Stream> CreateWrite
    {
        get => _createWrite;
        set
        {
            _createWrite = value ?? throw new ArgumentNullException (nameof (value));
            _createWriteWasSet = true;
        }
    }

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

    /// <summary>Asynchronously streams the specified file into the editor.</summary>
    public Task<bool> OpenFileAsync (string filePath, CancellationToken cancellationToken = default)
    {
        return OpenFileAsync (filePath, false, cancellationToken);
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

        try
        {
            await SaveFileToAsync (CurrentFilePath, marshalToApp, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return false;
        }
        catch (Exception ex) when (IsFileOperationException (ex))
        {
            return false;
        }

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
        bool marshalToApp,
        CancellationToken cancellationToken = default)
    {
        long? statusOperationId = null;

        try
        {
            await using Stream stream = OpenRead (filePath);
            var fileSize = GetStreamLength (stream);
            var startedStatusOperationId = BeginStreamingStatus (FormatStartingProgress ("Loading", fileSize));
            statusOperationId = startedStatusOperationId;

            IProgress<TextDocumentProgress> progress =
                CreateStreamingProgress (progress => ReportLoadProgress (startedStatusOperationId, progress));

            // When there is a running app loop, hand Editor.LoadAsync a UI-thread marshal so it can read on a
            // background thread and append each chunk on the UI thread — the editor paints an empty buffer
            // immediately and fills in progressively instead of blocking until the whole file is read.
            Func<Action, Task>? marshal = marshalToApp ? InvokeOnAppAsync : null;

            await Editor.LoadAsync (
                stream,
                encoding: null,
                progress: progress,
                cancellationToken: cancellationToken,
                marshal: marshal);

            await RunOnApp (
                marshalToApp,
                () =>
                {
                    CurrentFilePath = filePath;
                    ApplyFileMetadata (filePath);
                    CompleteStreamingStatus (
                        startedStatusOperationId,
                        FormatCompletedProgress ("Loaded", fileSize));
                });

            // Non-marshalled (sync / test) path: post-load work above ran on a background continuation thread and
            // re-claimed TextDocument ownership. Release it as the final step so the caller's thread can use the
            // document. The marshalled path keeps UI-thread ownership and must not do this.
            if (!marshalToApp)
            {
                Editor.Document?.SetOwnerThread (null);
            }

            return true;
        }
        catch (OperationCanceledException)
        {
            if (statusOperationId is { } startedStatusOperationId)
            {
                CompleteStreamingStatus (startedStatusOperationId, "Load canceled");
            }
            else
            {
                CompleteStreamingStatus ("Load canceled");
            }

            return false;
        }
        catch (Exception ex) when (IsFileOperationException (ex))
        {
            if (statusOperationId is { } startedStatusOperationId)
            {
                CompleteStreamingStatus (startedStatusOperationId, "Load failed");
            }
            else
            {
                CompleteStreamingStatus ("Load failed");
            }

            return false;
        }
    }

    /// <summary>
    ///     Begins a progressive, UI-marshalled load of <paramref name="filePath" /> against the running app loop.
    ///     Used by the CLI path so the window appears before the file finishes loading.
    /// </summary>
    public void BeginOpenFile (string filePath)
    {
        CurrentLoadTask = OpenFileAsync (filePath, true);
    }

    private Task RunOnApp (bool marshalToApp, Action action)
    {
        if (marshalToApp)
        {
            return InvokeOnAppAsync (action);
        }

        action ();

        return Task.CompletedTask;
    }

    private async Task<bool> SaveFileAsAsync (
        string filePath,
        bool marshalToApp = false,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await SaveFileToAsync (filePath, marshalToApp, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return false;
        }
        catch (Exception ex) when (IsFileOperationException (ex))
        {
            return false;
        }

        CurrentFilePath = filePath;
        UpdateFileNameShortcut ();
        UpdatePreviewVisibility ();

        return true;
    }

    private async Task SaveFileToAsync (string filePath, bool marshalToApp, CancellationToken cancellationToken)
    {
        long? statusOperationId = null;

        try
        {
            await using Stream stream = CreateWrite (filePath);
            var startedStatusOperationId = BeginStreamingStatus (FormatStartingProgress ("Saving", null));
            statusOperationId = startedStatusOperationId;

            IProgress<TextDocumentProgress> progress =
                CreateStreamingProgress (progress => ReportSaveProgress (startedStatusOperationId, progress));
            await Editor.SaveAsync (stream, progress, cancellationToken);
            var fileSize = GetStreamLength (stream);

            void MarkSaved ()
            {
                Editor.Document!.UndoStack.MarkAsOriginalFile ();
                CompleteStreamingStatus (startedStatusOperationId, FormatCompletedProgress ("Saved", fileSize));
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
        catch (OperationCanceledException)
        {
            if (statusOperationId is { } startedStatusOperationId)
            {
                CompleteStreamingStatus (startedStatusOperationId, "Save canceled");
            }
            else
            {
                CompleteStreamingStatus ("Save canceled");
            }

            throw;
        }
        catch (Exception ex) when (IsFileOperationException (ex))
        {
            if (statusOperationId is { } startedStatusOperationId)
            {
                CompleteStreamingStatus (startedStatusOperationId, "Save failed");
            }
            else
            {
                CompleteStreamingStatus ("Save failed");
            }

            throw;
        }
    }

    private Stream OpenReadFromReadAllText (string path)
    {
        return new MemoryStream (Encoding.UTF8.GetBytes (_readAllText (path)));
    }

    private Stream CreateWriteFromWriteAllText (string path)
    {
        return new WriteAllTextStream (path, _writeAllText);
    }

    private static bool IsFileOperationException (Exception ex)
    {
        return ex is IOException or UnauthorizedAccessException;
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

    private void ReportLoadProgress (long statusOperationId, TextDocumentProgress progress)
    {
        if (!ShouldReportStreamingProgress (statusOperationId, progress))
        {
            return;
        }

        SetLoadStatus (FormatProgress ("Loading", progress), true, statusOperationId);
    }

    private void ReportSaveProgress (long statusOperationId, TextDocumentProgress progress)
    {
        if (!ShouldReportStreamingProgress (statusOperationId, progress))
        {
            return;
        }

        SetLoadStatus (FormatProgress ("Saving", progress), true, statusOperationId);
    }

    private IProgress<TextDocumentProgress> CreateStreamingProgress (Action<TextDocumentProgress> handler)
    {
        return App is null
            ? new InlineProgress<TextDocumentProgress> (handler)
            : new Progress<TextDocumentProgress> (handler);
    }

    private long BeginStreamingStatus (string status)
    {
        var statusOperationId = Interlocked.Increment (ref _streamingStatusOperationId);

        ResetStreamingStatusThrottle ();
        SetLoadStatus (status, true, statusOperationId);

        return statusOperationId;
    }

    private void CompleteStreamingStatus (long statusOperationId, string status)
    {
        var completionOperationId = statusOperationId + 1;

        // A newer operation owns the status item; stale completions must not overwrite it.
        if (Interlocked.CompareExchange (
                ref _streamingStatusOperationId,
                completionOperationId,
                statusOperationId)
            != statusOperationId)
        {
            return;
        }

        SetLoadStatus (status, false, completionOperationId);
    }

    private void CompleteStreamingStatus (string status)
    {
        var completionOperationId = Interlocked.Increment (ref _streamingStatusOperationId);
        SetLoadStatus (status, false, completionOperationId);
    }

    private void SetLoadStatus (string status, bool showSpinner, long statusOperationId)
    {
        void Update ()
        {
            if (Interlocked.Read (ref _streamingStatusOperationId) != statusOperationId)
            {
                return;
            }

            // The status spinner is visible only while it is actively spinning.
            LoadStatusSpinner.Visible = showSpinner;
            LoadStatusSpinner.AutoSpin = showSpinner;
            LoadSpinnerShortcut.Title = status;
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
        lock (_streamingStatusLock)
        {
            _lastStreamingStatusUpdate = DateTime.MinValue;
            _lastStreamingStatusUnits = 0;
        }
    }

    private bool ShouldReportStreamingProgress (long statusOperationId, TextDocumentProgress progress)
    {
        if (Interlocked.Read (ref _streamingStatusOperationId) != statusOperationId)
        {
            return false;
        }

        var processedUnits = progress.BytesProcessed ?? progress.CharactersProcessed;
        var totalUnits = progress.TotalBytes ?? progress.TotalCharacters;

        if (totalUnits == processedUnits)
        {
            return true;
        }

        lock (_streamingStatusLock)
        {
            DateTime now = DateTime.UtcNow;

            if (processedUnits - _lastStreamingStatusUnits < StreamingStatusInterval
                && now - _lastStreamingStatusUpdate < TimeSpan.FromMilliseconds (StreamingStatusMilliseconds))
            {
                return false;
            }

            _lastStreamingStatusUnits = processedUnits;
            _lastStreamingStatusUpdate = now;
        }

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

        // Whole bytes read cleaner without decimals; larger units need one decimal for useful precision.
        var format = unitIndex == 0 ? "N0" : "N1";

        return $"{value.ToString (format)} {units[unitIndex]}";
    }

    /// <summary>
    ///     Adapts streamed saves to the legacy <see cref="WriteAllText" /> hook by buffering bytes in memory and
    ///     writing the final UTF-8 text on disposal.
    /// </summary>
    private sealed class WriteAllTextStream : MemoryStream
    {
        private readonly string _path;
        private readonly Action<string, string> _writeAllText;
        private bool _hasWritten;

        public WriteAllTextStream (string path, Action<string, string> writeAllText)
        {
            _path = path;
            _writeAllText = writeAllText;
        }

        protected override void Dispose (bool disposing)
        {
            if (disposing)
            {
                WriteOnce ();
            }

            base.Dispose (disposing);
        }

        public override async ValueTask DisposeAsync ()
        {
            WriteOnce ();
            await base.DisposeAsync ();
        }

        private void WriteOnce ()
        {
            if (_hasWritten)
            {
                return;
            }

            _hasWritten = true;
            _writeAllText (_path, Encoding.UTF8.GetString (ToArray ()));
        }
    }
}

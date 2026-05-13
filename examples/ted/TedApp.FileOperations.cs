using Terminal.Gui.Document;
using Terminal.Gui.Highlighting;
using Terminal.Gui.Resources;
using Terminal.Gui.Views;

namespace Ted;

public sealed partial class TedApp
{
    /// <summary>The path currently associated with <see cref="Editor" />, or <see langword="null" /> for an untitled buffer.</summary>
    public string? CurrentFilePath { get; private set; }

    /// <summary>Dialog hook used by <see cref="OpenFile" />. Tests can replace it to avoid interactive UI.</summary>
    public Func<string?> ShowOpenDialog { get; set; }

    /// <summary>Dialog hook used by <see cref="SaveFileAs" />. Tests can replace it to avoid interactive UI.</summary>
    public Func<string?> ShowSaveDialog { get; set; }

    /// <summary>Dialog hook used by <see cref="QuitFile" />. Tests can replace it to avoid interactive UI.</summary>
    public Func<SaveChangesChoice> ShowSaveChangesDialog { get; set; }

    /// <summary>File read hook used by <see cref="OpenFile" />. Tests can replace it with an in-memory fake.</summary>
    public Func<string, string> ReadAllText { get; set; } = File.ReadAllText;

    /// <summary>
    ///     File write hook used by <see cref="SaveFile" /> and <see cref="SaveFileAs" />. Tests can replace it with an
    ///     in-memory fake.
    /// </summary>
    public Action<string, string> WriteAllText { get; set; } = File.WriteAllText;

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

        if (string.IsNullOrWhiteSpace (filePath))
        {
            return false;
        }

        SetDocument (ReadAllText (filePath), filePath);

        return true;
    }

    /// <summary>Saves the editor text to the current file, or prompts for a path if the buffer is untitled.</summary>
    public bool SaveFile ()
    {
        if (CurrentFilePath is null)
        {
            return SaveFileAs ();
        }

        WriteAllText (CurrentFilePath, GetEditorText ());
        Editor.Document!.UndoStack.MarkAsOriginalFile ();

        return true;
    }

    /// <summary>Prompts for a file path, then saves the editor text to that path.</summary>
    public bool SaveFileAs ()
    {
        var filePath = ShowSaveDialog ();

        if (string.IsNullOrWhiteSpace (filePath))
        {
            return false;
        }

        CurrentFilePath = filePath;
        WriteAllText (filePath, GetEditorText ());
        Editor.Document!.UndoStack.MarkAsOriginalFile ();
        UpdateFileNameShortcut ();

        return true;
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

        // Auto-detect highlighting from file extension.
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
        Editor.SetNeedsDraw ();
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
            "_Don't Save",
            Strings.btnSave);

        return result switch
        {
            1 => SaveChangesChoice.Discard,
            2 => SaveChangesChoice.Save,
            _ => SaveChangesChoice.Cancel
        };
    }

    private string GetEditorText ()
    {
        return Editor.Document is null
            ? throw new InvalidOperationException ("ted cannot save because the editor has no document.")
            : Editor.Document.Text;
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

        OpenFile ();
    }

    private void Save () { SaveFile (); }

    private void SaveAs () { SaveFileAs (); }

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
}

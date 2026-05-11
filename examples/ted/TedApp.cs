using Terminal.Gui.App;
using Terminal.Gui.Drawing;
using Terminal.Gui.Input;
using Terminal.Gui.Resources;
using Terminal.Gui.Text.Document;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;
using TextMateSharp.Grammars;

namespace Ted;

/// <summary>
///     Top-level <see cref="Window" /> for the <c>ted</c> demo. MenuBar at top,
///     <see cref="Editor" /> in the middle, StatusBar at the bottom. Single-file —
///     no tabs (compare to Terminal.Gui's Notepad scenario).
/// </summary>
public sealed class TedApp : Window
{
    private readonly Shortcut _fileNameShortcut;
    private readonly Shortcut _locShortcut;

    /// <summary>Initializes a new <see cref="TedApp" />.</summary>
    public TedApp ()
    {
        Title = "ted — Terminal.Gui.Editor demo";
        BorderStyle = LineStyle.None;

        // Editor first so menu/status-bar shortcuts can pull their hotkeys directly from
        // Editor's KeyBindings (any commands the editor doesn't claim fall back to Application).
        Editor = new Editor ()
        {
            ShowLineNumbers = true,
            ConvertTabsToSpaces = true,

            ViewportSettings = ViewportSettingsFlags.HasScrollBars
        };

        // ted is the demo for the stopgap Editor.SyntaxHighlighter / SyntaxLanguage surface
        // (issue #32). The CS0618 warning is intentional on the public API; suppressed here
        // because exercising the API is exactly this app's job until issue #28 ships the
        // visual-line HighlightingColorizer pipeline.
#pragma warning disable CS0618 // Type or member is obsolete
        Editor.SyntaxHighlighter = new TextMateSyntaxHighlighter ();
#pragma warning restore CS0618 // Type or member is obsolete
        ShowOpenDialog = ShowDefaultOpenDialog;
        ShowSaveDialog = ShowDefaultSaveDialog;
        ShowSaveChangesDialog = ShowDefaultSaveChangesDialog;

        MenuBar menu = new ();
        CheckBox lineNumbersCheckBox = new ()
        {
            AllowCheckStateNone = false,
            CanFocus = false,
            Text = "_Line Numbers",
            Value = Editor.ShowLineNumbers ? CheckState.Checked : CheckState.UnChecked
        };

        CheckBox convertTabsToSpacesCheckBox = new ()
        {
            AllowCheckStateNone = false,
            CanFocus = false,
            Text = "_Convert Tabs To Spaces",
            Value = Editor.ConvertTabsToSpaces ? CheckState.Checked : CheckState.UnChecked
        };

        convertTabsToSpacesCheckBox.ValueChanged += (_, e) =>
        {
            Editor.ConvertTabsToSpaces = e.NewValue == CheckState.Checked;
        };

        ThemeDropDown = new DropDownList<ThemeName>
        {
            Value = ThemeName.DarkPlus,
            ReadOnly = true,
            CanFocus = false
        };

        ThemeDropDown.ValueChanged += (_, e) =>
        {
            if (e.Value is not { } themeName)
            {
                return;
            }

            // CS0618: Editor.SyntaxHighlighter is the stopgap API
            // ted exists to exercise. See issue #32.
#pragma warning disable CS0618 // Type or member is obsolete
            if (Editor.SyntaxHighlighter is TextMateSyntaxHighlighter highlighter)
            {
                if (highlighter.ThemeName == themeName)
                {
                    return;
                }

                highlighter.SetTheme (themeName);
                Editor.SetNeedsDraw ();

                return;
            }

            Editor.SyntaxHighlighter = new TextMateSyntaxHighlighter (themeName);
#pragma warning restore CS0618 // Type or member is obsolete
        };

        IndentationSizeUpDown = new NumericUpDown<int>
        {
            Value = Editor.IndentationSize,
            Increment = 1
        };

        IndentationSizeUpDown.ValueChanged += (_, e) =>
        {
            if (Editor.IndentationSize == e.NewValue)
            {
                return;
            }

            Editor.IndentationSize = e.NewValue;
        };

        ShowTabsCheckBox = new CheckBox
        {
            AllowCheckStateNone = false,
            CanFocus = false,
            Title = "↹",
            Value = Editor.ShowTabs ? CheckState.Checked : CheckState.UnChecked
        };

        ShowTabsCheckBox.ValueChanged += (_, e) =>
        {
            Editor.ShowTabs = e.NewValue == CheckState.Checked;
        };

        StatusBar statusBar =
            new ([
                new Shortcut (KeyFor (Command.Quit), "Quit", Quit),
                new Shortcut { Title = "Themes", CommandView = ThemeDropDown },
                new Shortcut { Text = "Indent", CommandView = IndentationSizeUpDown, MouseHighlightStates = MouseState.None },
                new Shortcut { CommandView = ShowTabsCheckBox },
                _locShortcut = new Shortcut (Key.Empty, FormatLoc (1, 1), null, "Loc") { MouseHighlightStates = MouseState.None },
                _fileNameShortcut = new Shortcut (Key.Empty, "<untitled>", Open)
                {
                    MouseHighlightStates = MouseState.None
                }
            ])
            {
                AlignmentModes = AlignmentModes.IgnoreFirstOrLast
            };

        menu.Add (new MenuBarItem (Strings.menuFile,
            [
                new MenuItem { Command = Command.New, Action = New, Key = KeyFor (Command.New) },
                new MenuItem { Command = Command.Open, Action = Open, Key = KeyFor (Command.Open) },
                new MenuItem { Command = Command.Save, Action = Save, Key = KeyFor (Command.Save) },
                new MenuItem { Command = Command.SaveAs, Action = SaveAs, Key = KeyFor (Command.SaveAs) },
                new MenuItem { Command = Command.Quit, Action = Quit, Key = KeyFor (Command.Quit) }
            ]),
            new MenuBarItem (Strings.menuEdit,
            [
                new MenuItem ("_Find...", "Find text in the current document", Find),
                new MenuItem ("_Replace...", "Find and replace text in the current document", Replace),
                new Line (), new MenuItem { Command = Command.Undo, Action = Undo, Key = KeyFor (Command.Undo) },
                new MenuItem { Command = Command.Redo, Action = Redo, Key = KeyFor (Command.Redo) },
                new Line (),
                new MenuItem { Command = Command.Cut, Action = Cut, Key = KeyFor (Command.Cut) },
                new MenuItem { Command = Command.Copy, Action = Copy, Key = KeyFor (Command.Copy) },
                new MenuItem { Command = Command.Paste, Action = Paste, Key = KeyFor (Command.Paste) },
                new MenuItem { Command = Command.SelectAll, Action = SelectAll, Key = KeyFor (Command.SelectAll) }
            ]),
            new MenuBarItem ("_Options",
            [
                new MenuItem
                {
                    Action = () =>
                    {
                        Editor.ShowLineNumbers = lineNumbersCheckBox.Value == CheckState.Checked;
                        Editor.SetNeedsDraw ();
                    },
                    CommandView = lineNumbersCheckBox,
                    HelpText = "Show line numbers"
                },
                new MenuItem
                {
                    CommandView = convertTabsToSpacesCheckBox,
                    HelpText = "Insert spaces when Tab is pressed"
                }
            ]),
            new MenuBarItem (Strings.menuHelp,
                [new MenuItem ("_About", "Show About dialog", Action)])
        );

        Editor.Y = Pos.Bottom (menu);
        Editor.Width = Dim.Fill ();
        Editor.Height = Dim.Fill (statusBar);

        Add (menu, Editor, statusBar);

        // Editor.CaretChanged covers both user-driven movement and document edits that shift the
        // caret (insert/remove). Initial render seeds the value before any movement happens.
        Editor.CaretChanged += (_, _) => UpdateLocShortcut ();
        UpdateLocShortcut ();
    }


    /// <summary>The editor View at the centre of the app. Exposed for tests and future commands.</summary>
    public Editor Editor { get; }

    /// <summary>The syntax-highlighting theme selector shown in the status bar.</summary>
    public DropDownList<ThemeName> ThemeDropDown { get; }

    /// <summary>The indentation-size selector shown in the status bar.</summary>
    public NumericUpDown<int> IndentationSizeUpDown { get; }

    /// <summary>The status-bar checkbox that toggles visible tab glyphs.</summary>
    public CheckBox ShowTabsCheckBox { get; }

    /// <summary>
    ///     The status-bar shortcut that mirrors the editor's caret position. Both line and column are
    ///     1-based. Updated whenever <see cref="Views.Editor.CaretChanged" /> fires (user-driven movement
    ///     and document edits that shift the caret).
    /// </summary>
    public Shortcut LocShortcut => _locShortcut;

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

    /// <summary>
    ///     Resolves the key shortcut for <paramref name="command" /> by asking the <see cref="Editor" />'s
    ///     <see cref="View.KeyBindings" /> first; falls back to <see cref="Application.GetDefaultKey" /> for
    ///     commands the editor doesn't claim (Quit, Open/Save, clipboard, …).
    /// </summary>
    private Key KeyFor (Command command)
    {
        return Editor.KeyBindings.GetAllFromCommands (command).FirstOrDefault () ?? Application.GetDefaultKey (command);
    }

    private void Action () { }

    private void Find () { ShowFindReplaceDialog (false); }

    private void Replace () { ShowFindReplaceDialog (true); }

    private void SelectAll () { }

    private void Paste () { }

    private void Copy () { }

    private void Cut () { }

    private void Redo () { Editor.Document?.UndoStack.Redo (); }

    private void Undo () { Editor.Document?.UndoStack.Undo (); }

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

        int? result = MessageBox.Query (
            App,
            "Save changes?",
            "The document has unsaved changes. Save before quitting?",
            Strings.btnCancel,
            "Don't Save",
            Strings.btnSave);

        return result switch
        {
            1 => SaveChangesChoice.Discard,
            2 => SaveChangesChoice.Save,
            _ => SaveChangesChoice.Cancel
        };
    }

    internal void SetDocument (string text, string? filePath)
    {
        Editor.ClearSelection ();
        Editor.Document = new TextDocument (text);
        Editor.CaretOffset = 0;
        CurrentFilePath = filePath;
        UpdateFileNameShortcut ();
        Editor.SetNeedsDraw ();
    }

    private string GetEditorText ()
    {
        return Editor.Document is null
            ? throw new InvalidOperationException ("ted cannot save because the editor has no document.")
            : Editor.Document.Text;
    }

    private void UpdateFileNameShortcut ()
    {
        _fileNameShortcut.Text = CurrentFilePath is null ? "<untitled>" : Path.GetFileName (CurrentFilePath);
        _fileNameShortcut.HelpText = CurrentFilePath ?? "No file";
        _fileNameShortcut.SetNeedsDraw ();
    }

    private void UpdateLocShortcut ()
    {
        TextDocument? document = Editor.Document;

        if (document is null)
        {
            _locShortcut.Text = FormatLoc (1, 1);
        }
        else
        {
            DocumentLine line = document.GetLineByOffset (Editor.CaretOffset);
            _locShortcut.Text = FormatLoc (line.LineNumber, Editor.CaretOffset - line.Offset + 1);
        }

        _locShortcut.SetNeedsDraw ();
    }

    private static string FormatLoc (int line, int column)
    {
        return $"{line}, {column}";
    }

    private void New ()
    {
        // TODO: if unsaved changes, confirm with user before clearing
        NewFile ();
    }

    private void Open ()
    {
        // TODO: if unsaved changes, confirm with user before clearing
        OpenFile ();
    }

    private void Save () { SaveFile (); }

    private void SaveAs () { SaveFileAs (); }

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

    private void Quit ()
    {
        QuitFile ();
    }

    private void ShowFindReplaceDialog (bool selectReplaceTab)
    {
        if (App is null)
        {
            throw new InvalidOperationException ("Cannot show find/replace when Application is not running.");
        }

        using FindReplaceDialog dialog = new (Editor, selectReplaceTab);
        App.Run (dialog);
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

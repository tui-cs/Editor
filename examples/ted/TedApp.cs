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

    /// <summary>Initializes a new <see cref="TedApp" />.</summary>
    public TedApp ()
    {
        Title = "ted — Terminal.Gui.Editor demo";
        BorderStyle = LineStyle.None;

        // Editor first so menu/status-bar shortcuts can pull their hotkeys directly from
        // Editor's KeyBindings (any commands the editor doesn't claim fall back to Application).
        Editor = new ();
        Editor.SyntaxHighlighter = new TextMateSyntaxHighlighter (ThemeName.DarkPlus);
        ShowOpenDialog = ShowDefaultOpenDialog;
        ShowSaveDialog = ShowDefaultSaveDialog;

        MenuBar menu = new ();

        ThemeDropDown = new ()
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
                                      };

        StatusBar statusBar =
            new ([
                new Shortcut (KeyFor (Command.Quit), "Quit", Quit),
                new Shortcut (Key.Empty, "Themes", null) { MouseHighlightStates = MouseState.None },
                new Shortcut { Title = "Themes", CommandView = ThemeDropDown },
                new Shortcut (Key.Empty, "x, y", null, "Loc") { MouseHighlightStates = MouseState.None },
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
                    new Line (),
                    new MenuItem { Command = Command.Undo, Action = Undo, Key = KeyFor (Command.Undo) },
                    new MenuItem { Command = Command.Redo, Action = Redo, Key = KeyFor (Command.Redo) },
                    new Line (),
                    new MenuItem { Command = Command.Cut, Action = Cut, Key = KeyFor (Command.Cut) },
                    new MenuItem { Command = Command.Copy, Action = Copy, Key = KeyFor (Command.Copy) },
                    new MenuItem { Command = Command.Paste, Action = Paste, Key = KeyFor (Command.Paste) },
                    new MenuItem { Command = Command.SelectAll, Action = SelectAll, Key = KeyFor (Command.SelectAll) }
                ]),
            new MenuBarItem (Strings.menuHelp,
                [new MenuItem ("_About", "Show About dialog", Action)])
        );

        Editor.Y = Pos.Bottom (menu);
        Editor.Width = Dim.Fill ();
        Editor.Height = Dim.Fill (statusBar);

        Add (menu, Editor, statusBar);
    }


    /// <summary>The editor View at the centre of the app. Exposed for tests and future commands.</summary>
    public Editor Editor { get; }

    /// <summary>The syntax-highlighting theme selector shown in the status bar.</summary>
    public DropDownList<ThemeName> ThemeDropDown { get; }
    /// <summary>The path currently associated with <see cref="Editor" />, or <see langword="null" /> for an untitled buffer.</summary>
    public string? CurrentFilePath { get; private set; }

    /// <summary>Dialog hook used by <see cref="OpenFile" />. Tests can replace it to avoid interactive UI.</summary>
    public Func<string?> ShowOpenDialog { get; set; }

    /// <summary>Dialog hook used by <see cref="SaveFileAs" />. Tests can replace it to avoid interactive UI.</summary>
    public Func<string?> ShowSaveDialog { get; set; }

    /// <summary>File read hook used by <see cref="OpenFile" />. Tests can replace it with an in-memory fake.</summary>
    public Func<string, string> ReadAllText { get; set; } = File.ReadAllText;

    /// <summary>File write hook used by <see cref="SaveFile" /> and <see cref="SaveFileAs" />. Tests can replace it with an in-memory fake.</summary>
    public Action<string, string> WriteAllText { get; set; } = File.WriteAllText;

    /// <summary>Clears the editor and makes the buffer untitled.</summary>
    public void NewFile ()
    {
        SetDocument (string.Empty, null);
    }

    /// <summary>Prompts for a file path, then loads that file into the editor.</summary>
    public bool OpenFile ()
    {
        string? filePath = ShowOpenDialog ();

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

        return true;
    }

    /// <summary>Prompts for a file path, then saves the editor text to that path.</summary>
    public bool SaveFileAs ()
    {
        string? filePath = ShowSaveDialog ();

        if (string.IsNullOrWhiteSpace (filePath))
        {
            return false;
        }

        CurrentFilePath = filePath;
        WriteAllText (filePath, GetEditorText ());
        UpdateFileNameShortcut ();

        return true;
    }

    /// <summary>
    ///     Resolves the key shortcut for <paramref name="command" /> by asking the <see cref="Editor" />'s
    ///     <see cref="View.KeyBindings" /> first; falls back to <see cref="Application.GetDefaultKey" /> for
    ///     commands the editor doesn't claim (Quit, Open/Save, clipboard, …).
    /// </summary>
    private Key KeyFor (Command command) =>
        Editor.KeyBindings.GetAllFromCommands (command).FirstOrDefault () ?? Application.GetDefaultKey (command);

    private void Action () { }

    private void Find () { ShowFindReplaceDialog (selectReplaceTab: false); }

    private void Replace () { ShowFindReplaceDialog (selectReplaceTab: true); }

    private void SelectAll () { }

    private void Paste () { }

    private void Copy () { }

    private void Cut () { }

    private void Redo () { Editor.Document?.UndoStack.Redo (); }

    private void Undo () { Editor.Document?.UndoStack.Undo (); }

    private string? ShowDefaultOpenDialog ()
    {
        using OpenDialog dialog = new ()
        {
            AllowsMultipleSelection = false,
            MustExist = true,
            OpenMode = OpenMode.File
        };

        if (App is null)
        {
            throw new InvalidOperationException ("ted must be running before showing the open dialog.");
        }

        App.Run (dialog);

        return dialog.FilePaths.FirstOrDefault ();
    }

    private string? ShowDefaultSaveDialog ()
    {
        using SaveDialog dialog = new ()
        {
            AllowsMultipleSelection = false,
            OpenMode = OpenMode.File
        };

        if (App is null)
        {
            throw new InvalidOperationException ("ted must be running before showing the save dialog.");
        }

        App.Run (dialog);

        return dialog.FileName;
    }

    private void SetDocument (string text, string? filePath)
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
        if (Editor.Document is null)
        {
            throw new InvalidOperationException ("ted cannot save because the editor has no document.");
        }

        return Editor.Document.Text;
    }

    private void UpdateFileNameShortcut ()
    {
        _fileNameShortcut.Text = CurrentFilePath is null ? "<untitled>" : Path.GetFileName (CurrentFilePath);
        _fileNameShortcut.HelpText = CurrentFilePath ?? "No file";
        _fileNameShortcut.SetNeedsDraw ();
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

    private void Quit ()
    {
        // TODO: add logic for unsaved changes, confirm quit, etc.
        RequestStop ();
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
}

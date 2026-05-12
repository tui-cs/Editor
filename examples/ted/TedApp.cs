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
public sealed partial class TedApp : Window
{
    private readonly Shortcut _fileNameShortcut;
    private readonly Shortcut _locShortcut;

    /// <summary>Initializes a new <see cref="TedApp" />.</summary>
    public TedApp (bool readOnly = false)
    {
        Title = "ted — Terminal.Gui.Editor demo";
        BorderStyle = LineStyle.None;

        // Editor first so menu/status-bar shortcuts can pull their hotkeys directly from
        // Editor's KeyBindings (any commands the editor doesn't claim fall back to Application).
        Editor = new Editor
        {
            ShowLineNumbers = true,
            ConvertTabsToSpaces = true,
            ReadOnly = readOnly,

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
                new Shortcut
                    { Text = "Indent", CommandView = IndentationSizeUpDown, MouseHighlightStates = MouseState.None },
                new Shortcut { CommandView = ShowTabsCheckBox },
                _locShortcut = new Shortcut (Key.Empty, FormatLoc (1, 1), null)
                    { MouseHighlightStates = MouseState.None },
                _fileNameShortcut = new Shortcut (Key.Empty, "<untitled>", Open)
                {
                    MouseHighlightStates = MouseState.None
                }
            ])
            {
                AlignmentModes = AlignmentModes.IgnoreFirstOrLast
            };

        PopoverMenu editorContextMenu = new (CreateEditMenuItems ())
        {
            Target = new WeakReference<View> (Editor)
        };

        Editor.MouseEvent += (_, mouse) =>
        {
            if (!mouse.Flags.HasFlag (MouseFlags.RightButtonClicked))
            {
                return;
            }

            editorContextMenu.MakeVisible (mouse.ScreenPosition);
            mouse.Handled = true;
        };

        menu.Add (new MenuBarItem (Strings.menuFile,
            [
                new MenuItem { Command = Command.New, Action = New, Key = KeyFor (Command.New) },
                new MenuItem { Command = Command.Open, Action = Open, Key = KeyFor (Command.Open) },
                new MenuItem { Command = Command.Save, Action = Save, Key = KeyFor (Command.Save) },
                new MenuItem { Command = Command.SaveAs, Action = SaveAs, Key = KeyFor (Command.SaveAs) },
                new MenuItem { Command = Command.Quit, Action = Quit, Key = KeyFor (Command.Quit) }
            ]),
            new MenuBarItem (Strings.menuEdit, CreateEditMenuItems ()),
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
    ///     1-based. Updated whenever <see cref="Editor.CaretChanged" /> fires (user-driven movement
    ///     and document edits that shift the caret).
    /// </summary>
    public Shortcut LocShortcut => _locShortcut;

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

    private void UpdateFileNameShortcut ()
    {
        _fileNameShortcut.Title = CurrentFilePath is null ? "<untitled>" : Path.GetFileName (CurrentFilePath);
        _fileNameShortcut.SetNeedsDraw ();
    }

    private void UpdateLocShortcut ()
    {
        TextDocument? document = Editor.Document;

        if (document is null)
        {
            _locShortcut.Title = FormatLoc (1, 1);
        }
        else
        {
            DocumentLine line = document.GetLineByOffset (Editor.CaretOffset);
            _locShortcut.Title = FormatLoc (line.LineNumber, Editor.CaretOffset - line.Offset + 1);
        }

        _locShortcut.SetNeedsDraw ();
    }

    private static string FormatLoc (int line, int column)
    {
        return $"Ln: {line}, Ch: {column}";
    }
}

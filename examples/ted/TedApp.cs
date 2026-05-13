using Terminal.Gui.App;
using Terminal.Gui.Configuration;
using Terminal.Gui.Document;
using Terminal.Gui.Document.Folding;
using Terminal.Gui.Drawing;
using Terminal.Gui.Highlighting;
using Terminal.Gui.Input;
using Terminal.Gui.Resources;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Editor;
using Terminal.Gui.Views;

namespace Ted;

/// <summary>
///     Top-level <see cref="Window" /> for the <c>ted</c> demo. MenuBar at top,
///     <see cref="Editor" /> in the middle, StatusBar at the bottom. Single-file —
///     no tabs (compare to Terminal.Gui's Notepad scenario).
/// </summary>
public sealed partial class TedApp : Window
{
    private readonly BraceFoldingStrategy _braceFoldingStrategy;
    private readonly Shortcut _fileNameShortcut;

    /// <summary>Initializes a new <see cref="TedApp" />.</summary>
    public TedApp (bool readOnly = false)
    {
        Title = "ted — Terminal.Gui.Editor demo";
        BorderStyle = LineStyle.None;

        // Editor first so menu/status-bar shortcuts can pull their hotkeys directly from
        // Editor's KeyBindings (any commands the editor doesn't claim fall back to Application).
        Editor = new Editor
        {
            GutterOptions = GutterOptions.LineNumbers | GutterOptions.Folding,
            ConvertTabsToSpaces = true,
            ReadOnly = readOnly,

            ViewportSettings = ViewportSettingsFlags.HasScrollBars,

            // Default to C# highlighting from the built-in xshd definitions.
            HighlightingDefinition = HighlightingManager.Instance.GetDefinition ("C#")
        };

        // Enable brace-based folding. The strategy re-scans on each document change.
        _braceFoldingStrategy = new BraceFoldingStrategy ();
        InstallFolding ();

        ShowOpenDialog = ShowDefaultOpenDialog;
        ShowSaveDialog = ShowDefaultSaveDialog;
        ShowSaveChangesDialog = ShowDefaultSaveChangesDialog;

        MenuBar menu = new ()
        {
            AlignmentModes = AlignmentModes.IgnoreFirstOrLast
        };

        CheckBox lineNumbersCheckBox = new ()
        {
            AllowCheckStateNone = false,
            CanFocus = false,
            Text = "_Line Numbers",
            Value = Editor.GutterOptions.HasFlag (GutterOptions.LineNumbers) ? CheckState.Checked : CheckState.UnChecked
        };

        CheckBox foldIndicatorsCheckBox = new ()
        {
            AllowCheckStateNone = false,
            CanFocus = false,
            Text = "_Fold Indicators",
            Value = Editor.GutterOptions.HasFlag (GutterOptions.Folding) ? CheckState.Checked : CheckState.UnChecked
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

        CheckBox autoIndentCheckBox = new ()
        {
            AllowCheckStateNone = false,
            CanFocus = false,
            Text = "_Auto Indent",
            Value = Editor.IndentationStrategy is not null ? CheckState.Checked : CheckState.UnChecked
        };

        autoIndentCheckBox.ValueChanged += (_, e) =>
        {
            Editor.IndentationStrategy = e.NewValue == CheckState.Checked
                ? new Terminal.Gui.Text.Indentation.DefaultIndentationStrategy ()
                : null;
        };

        CheckBox useThemeBackgroundCheckBox = new ()
        {
            AllowCheckStateNone = false,
            CanFocus = false,
            Text = "Use _Theme Background",
            Value = Editor.UseThemeBackground ? CheckState.Checked : CheckState.UnChecked
        };

        LanguageShortcut = new Shortcut (Key.Empty, "C#", null) { MouseHighlightStates = MouseState.None };

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
                new Shortcut { Title = "Language", CommandView = LanguageShortcut },
                new Shortcut
                    { Text = "Indent", CommandView = IndentationSizeUpDown, MouseHighlightStates = MouseState.None },
                new Shortcut { CommandView = ShowTabsCheckBox },
                LocShortcut = new Shortcut (Key.Empty, FormatLoc (1, 1), null)
                    { MouseHighlightStates = MouseState.None }
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
                        if (lineNumbersCheckBox.Value == CheckState.Checked)
                        {
                            Editor.GutterOptions |= GutterOptions.LineNumbers;
                        }
                        else
                        {
                            Editor.GutterOptions &= ~GutterOptions.LineNumbers;
                        }

                        Editor.SetNeedsDraw ();
                    },
                    CommandView = lineNumbersCheckBox,
                    HelpText = "Show line numbers"
                },
                new MenuItem
                {
                    Action = () =>
                    {
                        if (foldIndicatorsCheckBox.Value == CheckState.Checked)
                        {
                            Editor.GutterOptions |= GutterOptions.Folding;
                        }
                        else
                        {
                            Editor.GutterOptions &= ~GutterOptions.Folding;
                        }

                        Editor.SetNeedsDraw ();
                    },
                    CommandView = foldIndicatorsCheckBox,
                    HelpText = "Show fold indicators in the gutter"
                },
                new MenuItem
                {
                    CommandView = convertTabsToSpacesCheckBox,
                    HelpText = "Insert spaces when Tab is pressed"
                },
                new MenuItem
                {
                    CommandView = autoIndentCheckBox,
                    HelpText = "Copy indentation from the previous line on Enter"
                },
                new MenuItem
                {
                    Action = () =>
                    {
                        Editor.UseThemeBackground = useThemeBackgroundCheckBox.Value == CheckState.Checked;
                    },
                    CommandView = useThemeBackgroundCheckBox,
                    HelpText = "Use theme background for highlighted text"
                }
            ]),
            new MenuBarItem (Strings.menuHelp,
                [new MenuItem ("_About", "About ted", ShowAboutDialog)]),
            _fileNameShortcut = new Shortcut (Key.Empty, "<untitled>", Open)
            {
                MouseHighlightStates = MouseState.None,
                SchemeName = SchemeManager.SchemesToSchemeName (Schemes.Dialog)
            }
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

    /// <summary>The status-bar shortcut that displays the current syntax-highlighting language name.</summary>
    public Shortcut LanguageShortcut { get; }

    /// <summary>The indentation-size selector shown in the status bar.</summary>
    public NumericUpDown<int> IndentationSizeUpDown { get; }

    /// <summary>The status-bar checkbox that toggles visible tab glyphs.</summary>
    public CheckBox ShowTabsCheckBox { get; }

    /// <summary>
    ///     The status-bar shortcut that mirrors the editor's caret position. Both line and column are
    ///     1-based. Updated whenever <see cref="Editor.CaretChanged" /> fires (user-driven movement
    ///     and document edits that shift the caret).
    /// </summary>
    public Shortcut LocShortcut { get; }

    /// <summary>
    ///     Resolves the key shortcut for <paramref name="command" /> by asking the <see cref="Editor" />'s
    ///     <see cref="View.KeyBindings" /> first; falls back to <see cref="Application.GetDefaultKey" /> for
    ///     commands the editor doesn't claim (Quit, Open/Save, clipboard, …).
    /// </summary>
    private Key KeyFor (Command command)
    {
        return Editor.KeyBindings.GetAllFromCommands (command).FirstOrDefault () ?? Application.GetDefaultKey (command);
    }

    private void ShowAboutDialog ()
    {
        Dialog dialog = new ()
        {
            Title = "About ted",
            Buttons = [new Button { Title = Strings.btnOk, IsDefault = true }]
        };

        dialog.Border.Settings &= ~BorderSettings.Title;

        Version? tgVersion = typeof (Application).Assembly.GetName ().Version;

        Label tagline = new ()
        {
            Text = $"A terminal text editor built with Terminal.Gui {tgVersion}",
            TextAlignment = Alignment.Center,
            X = Pos.Center (),
            Width = Dim.Auto (DimAutoStyle.Text),
            Height = Dim.Auto (DimAutoStyle.Text)
        };

        TedLogo logo = new () { X = Pos.Center (), Y = Pos.Bottom (tagline) + 1 };

        Version? editorVersion = typeof (Editor).Assembly.GetName ().Version;

        View version = new ()
        {
            Width = Dim.Auto (),
            Height = Dim.Auto (),
            Text = $"Terminal.Gui.Editor {editorVersion}",
            X = Pos.Center (),
            Y = Pos.Bottom (logo) + 1
        };

        Link link = new ()
        {
            Text = "https://github.com/gui-cs/Editor",
            Url = "https://github.com/gui-cs/Editor",
            X = Pos.Center (),
            Y = Pos.Bottom (version) + 1
        };

        dialog.Add (tagline, logo, version, link);
        dialog.Buttons.ElementAt (0).SetFocus ();
        App?.Run (dialog);
        dialog.Dispose ();
    }

    private void UpdateFileNameShortcut ()
    {
        _fileNameShortcut.Title = CurrentFilePath ?? "<untitled>";
        _fileNameShortcut.SetNeedsDraw ();
    }

    private void UpdateLocShortcut ()
    {
        TextDocument? document = Editor.Document;

        if (document is null)
        {
            LocShortcut.Title = FormatLoc (1, 1);
        }
        else
        {
            DocumentLine line = document.GetLineByOffset (Editor.CaretOffset);
            var loc = FormatLoc (line.LineNumber, Editor.CaretOffset - line.Offset + 1);

            if (Editor.HasMultipleCarets)
            {
                loc += $" ({Editor.AdditionalCaretOffsets.Count + 1} carets)";
            }

            LocShortcut.Title = loc;
        }

        LocShortcut.SetNeedsDraw ();
    }

    private static string FormatLoc (int line, int column)
    {
        return $"Ln: {line}, Ch: {column}";
    }

    /// <summary>
    ///     Creates a <see cref="FoldingManager" /> for the current document and wires up
    ///     automatic fold updates on document changes.
    /// </summary>
    private void InstallFolding ()
    {
        if (Editor.Document is null)
        {
            return;
        }

        FoldingManager fm = new (Editor.Document);
        Editor.FoldingManager = fm;
        _braceFoldingStrategy.UpdateFoldings (fm, Editor.Document);
        Editor.Document.Changed += (_, _) => UpdateFoldings ();
    }

    private void UpdateFoldings ()
    {
        if (Editor.FoldingManager is not null && Editor.Document is not null)
        {
            _braceFoldingStrategy.UpdateFoldings (Editor.FoldingManager, Editor.Document);
        }
    }
}

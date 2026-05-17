using System.Collections.Immutable;
using System.Collections.ObjectModel;
using Terminal.Gui.App;
using Terminal.Gui.Configuration;
using Terminal.Gui.Document;
using Terminal.Gui.Document.Folding;
using Terminal.Gui.Drawing;
using Terminal.Gui.Editor;
using Terminal.Gui.Input;
using Terminal.Gui.Resources;
using Terminal.Gui.Text.Indentation;
using Terminal.Gui.ViewBase;
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
    private readonly MenuItem _previewMarkdownMenuItem;

    /// <summary>Initializes a new <see cref="TedApp" />.</summary>
    public TedApp (bool readOnly = false)
    {
        Title = "ted — Terminal.Gui.Editor demo";
        BorderStyle = LineStyle.None;

        // Editor first so menu/status-bar shortcuts can pull their hotkeys directly from
        // Editor's KeyBindings (any commands the editor doesn't claim fall back to Application).
        Editor = new Editor
        {
            ConvertTabsToSpaces = EditorSettings.ConvertTabsToSpaces,
            IndentationSize = EditorSettings.IndentSize,
            WordWrap = EditorSettings.WordWrap,
            ShowTabs = EditorSettings.ShowTabs,
            ReadOnly = readOnly,
            CompletionProvider = EditorSettings.AutoComplete ? new WordCompletionProvider () : null,
            ViewportSettings = ViewportSettingsFlags.HasScrollBars
        };

        GutterOptions initialGutter = GutterOptions.None;

        if (EditorSettings.LineNumbers)
        {
            initialGutter |= GutterOptions.LineNumbers;
        }

        if (EditorSettings.FoldIndicators)
        {
            initialGutter |= GutterOptions.Folding;
        }

        Editor.GutterOptions = initialGutter;
        Editor.IndentationStrategy = EditorSettings.AutoIndent ? new DefaultIndentationStrategy () : null;

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

        CheckBox wordWrapCheckBox = new ()
        {
            AllowCheckStateNone = false,
            CanFocus = false,
            Text = "_Word Wrap",
            Value = Editor.WordWrap ? CheckState.Checked : CheckState.UnChecked
        };
        _previewMarkdownMenuItem = new MenuItem
        {
            Title = ToggleTitle (false, "_Preview Markdown"),
            Enabled = false
        };
        _previewMarkdownMenuItem.Action = () =>
        {
            if (PreviewCheckBox.Visible)
            {
                PreviewCheckBox.Value = PreviewCheckBox.Value == CheckState.Checked
                    ? CheckState.UnChecked
                    : CheckState.Checked;
            }
        };

        LanguageShortcut = new Shortcut (Key.Empty, "Plain Text", null) { MouseHighlightStates = MouseState.None };
        ImmutableList<string> themeNames = ThemeManager.GetThemeNames ();

        ThemeDropDown = new DropDownList
        {
            Source = new ListWrapper<string> (new ObservableCollection<string> (themeNames)),
            Text = ThemeManager.Theme,
            ReadOnly = true
        };

        ThemeDropDown.ValueChanged += (_, _) =>
        {
            var selected = ThemeDropDown.Text;

            if (!string.IsNullOrEmpty (selected) && selected != ThemeManager.Theme)
            {
                ThemeManager.Theme = selected;
            }
        };
        ShowTabsCheckBox.Value = Editor.ShowTabs ? CheckState.Checked : CheckState.UnChecked;
        PreviewCheckBox.ValueChanged += (_, e) =>
        {
            ToggleMarkdownPreview ();
            _previewMarkdownMenuItem.Title = ToggleTitle (e.NewValue == CheckState.Checked, "_Preview Markdown");
        };

        StatusBar statusBar =
            new ([
                new Shortcut { Title = "Language", CommandView = LanguageShortcut },
                new Shortcut { Title = "Theme", CommandView = ThemeDropDown },
                OverwriteShortcut = new Shortcut (Key.Empty, "INS", null)
                    { MouseHighlightStates = MouseState.None },
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
            new MenuBarItem ("_View",
            [
                new MenuItem
                {
                    Action = () =>
                    {
                        var shouldEnableLineNumbers = !Editor.GutterOptions.HasFlag (GutterOptions.LineNumbers);

                        if (shouldEnableLineNumbers)
                        {
                            Editor.GutterOptions |= GutterOptions.LineNumbers;
                        }
                        else
                        {
                            Editor.GutterOptions &= ~GutterOptions.LineNumbers;
                        }

                        lineNumbersCheckBox.Value = shouldEnableLineNumbers ? CheckState.Checked : CheckState.UnChecked;
                        Editor.SetNeedsDraw ();
                        SaveViewSettings ();
                    },
                    CommandView = lineNumbersCheckBox,
                    HelpText = "Show line numbers"
                },
                new MenuItem
                {
                    Action = () =>
                    {
                        var shouldEnableFoldIndicators = !Editor.GutterOptions.HasFlag (GutterOptions.Folding);

                        if (shouldEnableFoldIndicators)
                        {
                            Editor.GutterOptions |= GutterOptions.Folding;
                        }
                        else
                        {
                            Editor.GutterOptions &= ~GutterOptions.Folding;
                        }

                        foldIndicatorsCheckBox.Value =
                            shouldEnableFoldIndicators ? CheckState.Checked : CheckState.UnChecked;
                        Editor.SetNeedsDraw ();
                        SaveViewSettings ();
                    },
                    CommandView = foldIndicatorsCheckBox,
                    HelpText = "Show fold indicators in the gutter"
                },
                new MenuItem
                {
                    Action = () =>
                    {
                        var wordWrapEnabled = !Editor.WordWrap;
                        Editor.WordWrap = wordWrapEnabled;
                        wordWrapCheckBox.Value = wordWrapEnabled ? CheckState.Checked : CheckState.UnChecked;
                        SaveViewSettings ();
                    },
                    CommandView = wordWrapCheckBox,
                    HelpText = "Soft-wrap long lines at viewport edge"
                },
                new MenuItem
                {
                    Action = () =>
                    {
                        var showTabsEnabled = !Editor.ShowTabs;
                        Editor.ShowTabs = showTabsEnabled;
                        ShowTabsCheckBox.Value = showTabsEnabled ? CheckState.Checked : CheckState.UnChecked;
                        SaveViewSettings ();
                    },
                    CommandView = ShowTabsCheckBox,
                    HelpText = "Show tab glyphs"
                },
                _previewMarkdownMenuItem
            ]),
            new MenuBarItem ("_Options",
                [new MenuItem ("_Settings...", string.Empty, ShowSettingsDialog)]),
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
        Editor.OverwriteModeChanged += (_, _) => UpdateOverwriteShortcut ();
        Editor.FindRequested += (_, _) => ShowFindReplaceDialog (false);
        Editor.ReplaceRequested += (_, _) => ShowFindReplaceDialog (true);
        UpdateLocShortcut ();
    }

    /// <summary>The editor View at the centre of the app. Exposed for tests and future commands.</summary>
    public Editor Editor { get; }

    /// <summary>The status-bar shortcut that displays the current syntax-highlighting language name.</summary>
    public Shortcut LanguageShortcut { get; }

    /// <summary>The status-bar dropdown that selects <see cref="ThemeManager.Theme" />.</summary>
    public DropDownList ThemeDropDown { get; }

    /// <summary>The settings checkbox state for visible tab glyphs.</summary>
    public CheckBox ShowTabsCheckBox { get; } = new ()
    {
        AllowCheckStateNone = false,
        CanFocus = false,
        Title = "Show _Tabs"
    };

    /// <summary>
    ///     The status-bar shortcut that mirrors the editor's caret position. Both line and column are
    ///     1-based. Updated whenever <see cref="Editor.CaretChanged" /> fires (user-driven movement
    ///     and document edits that shift the caret).
    /// </summary>
    public Shortcut LocShortcut { get; }

    /// <summary>
    ///     The status-bar shortcut that shows whether the editor is in insert (INS) or overwrite (OVR)
    ///     mode. Updated whenever <see cref="Editor.OverwriteModeChanged" /> fires.
    /// </summary>
    public Shortcut OverwriteShortcut { get; }

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

    private void UpdateOverwriteShortcut ()
    {
        OverwriteShortcut.Title = Editor.OverwriteMode ? "OVR" : "INS";
        OverwriteShortcut.SetNeedsDraw ();
    }

    private static string FormatLoc (int line, int column)
    {
        return $"Ln {line}, Col {column}";
    }

    private static string ToggleTitle (bool on, string label)
    {
        return on ? $"✓ {label}" : $"  {label}";
    }

    private void SaveViewSettings ()
    {
        EditorSettings.LineNumbers = Editor.GutterOptions.HasFlag (GutterOptions.LineNumbers);
        EditorSettings.FoldIndicators = Editor.GutterOptions.HasFlag (GutterOptions.Folding);
        EditorSettings.WordWrap = Editor.WordWrap;
        EditorSettings.ShowTabs = Editor.ShowTabs;
        EditorSettings.IndentSize = Editor.IndentationSize;
        EditorSettings.ConvertTabsToSpaces = Editor.ConvertTabsToSpaces;
        EditorSettings.AutoIndent = Editor.IndentationStrategy is not null;
        EditorSettings.AutoComplete = Editor.CompletionProvider is not null;
        EditorSettings.Save ();
    }

    private void ShowSettingsDialog ()
    {
        if (App is null)
        {
            throw new InvalidOperationException ("ted must be running before showing settings.");
        }

        EditorSettingsDialog dialog = new (Editor);
        App.Run (dialog);

        if (dialog.WasAccepted)
        {
            dialog.ApplyTo (Editor);
            SaveViewSettings ();
        }

        dialog.Dispose ();
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

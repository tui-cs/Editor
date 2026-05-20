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
    private const int MaximumAutomaticFoldingDocumentLength = 1_000_000;

    private readonly BraceFoldingStrategy _braceFoldingStrategy;

    // Per-instance config path. Defaults to the real ~/.tui location; tests inject a temp path so they
    // never touch the developer's real config (and stay parallel-safe — no env/static mutation).
    private readonly string _configPath;
    private readonly Shortcut _fileNameShortcut;
    private readonly MenuItem _previewMarkdownMenuItem;
    private TextDocument? _foldingDocument;
    private bool _foldingUpdateNeeded;

    /// <summary>Initializes a new <see cref="TedApp" />.</summary>
    /// <param name="readOnly">Opens the editor read-only.</param>
    /// <param name="configPath">
    ///     Overrides where view settings persist. <see langword="null" /> uses
    ///     <see cref="EditorSettings.GetConfigPath" /> (the real <c>~/.tui/ted.config.json</c>).
    /// </param>
    public TedApp (bool readOnly = false, string? configPath = null)
    {
        _configPath = configPath ?? EditorSettings.GetConfigPath ();

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
            ViewportSettings = EditorSettings.Scrollbars
                ? ViewportSettingsFlags.HasScrollBars
                : ViewportSettingsFlags.None
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
        Editor.IndentationStrategy =
            EditorSettings.AutoIndent ? new DefaultIndentationStrategy () : null;

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
            Value = Editor.GutterOptions.HasFlag (GutterOptions.LineNumbers)
                ? CheckState.Checked
                : CheckState.UnChecked
        };

        CheckBox foldIndicatorsCheckBox = new ()
        {
            AllowCheckStateNone = false,
            CanFocus = false,
            Text = "_Fold Indicators",
            Value = Editor.GutterOptions.HasFlag (GutterOptions.Folding)
                ? CheckState.Checked
                : CheckState.UnChecked
        };

        CheckBox wordWrapCheckBox = new ()
        {
            AllowCheckStateNone = false,
            CanFocus = false,
            Text = "_Word Wrap",
            Value = Editor.WordWrap ? CheckState.Checked : CheckState.UnChecked
        };

        CheckBox scrollbarsCheckBox = new ()
        {
            AllowCheckStateNone = false,
            CanFocus = false,
            Text = "_Scrollbars",
            Value = EditorSettings.Scrollbars ? CheckState.Checked : CheckState.UnChecked
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
        OverwriteShortcut = new Shortcut (Key.Empty, "INS", null) { MouseHighlightStates = MouseState.None };
        LocShortcut = new Shortcut (Key.Empty, FormatLoc (1, 1), null) { MouseHighlightStates = MouseState.None };
        LoadStatusSpinner = new SpinnerView
        {
            Style = new SpinnerStyle.Aesthetic (),
            Width = 8,
            AutoSpin = false,
            Visible = false
        };
        LoadSpinnerShortcut = new Shortcut
        {
            CommandView = LoadStatusSpinner,
            Title = string.Empty,
            MouseHighlightStates = MouseState.None
        };
        PreviewCheckBox.ValueChanged += (_, e) =>
        {
            ToggleMarkdownPreview ();
            _previewMarkdownMenuItem.Title = ToggleTitle (e.NewValue == CheckState.Checked, "_Preview Markdown");
        };

        StatusBar statusBar =
            new ([
                new Shortcut { Title = "Language", CommandView = LanguageShortcut },
                new Shortcut { Title = "Theme", CommandView = ThemeDropDown },
                LoadSpinnerShortcut,
                OverwriteShortcut,
                LocShortcut
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
            new MenuBarItem (Strings.menuEdit, CreateEditMenuItems ()),
            new MenuBarItem ("_View",
            [
                new MenuItem
                {
                    Action = () =>
                    {
                        var shouldEnableLineNumbers =
                            !Editor.GutterOptions.HasFlag (GutterOptions.LineNumbers);

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
                        var shouldEnableFoldIndicators =
                            !Editor.GutterOptions.HasFlag (GutterOptions.Folding);

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
                new MenuItem
                {
                    Action = () =>
                    {
                        var shouldEnableScrollbars =
                            !Editor.ViewportSettings.HasFlag (ViewportSettingsFlags.HasScrollBars);

                        if (shouldEnableScrollbars)
                        {
                            Editor.ViewportSettings |= ViewportSettingsFlags.HasScrollBars;
                        }
                        else
                        {
                            Editor.ViewportSettings &= ~ViewportSettingsFlags.HasScrollBars;
                        }

                        scrollbarsCheckBox.Value = shouldEnableScrollbars ? CheckState.Checked : CheckState.UnChecked;
                        Editor.SetNeedsDraw ();
                        SaveViewSettings ();
                    },
                    CommandView = scrollbarsCheckBox,
                    HelpText = "Show scrollbars"
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

    /// <summary>The spinner view shown while streaming file load/save is running.</summary>
    public SpinnerView LoadStatusSpinner { get; }

    /// <summary>The status-bar shortcut that hosts <see cref="LoadStatusSpinner" />.</summary>
    public Shortcut LoadSpinnerShortcut { get; }

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
        return Editor.KeyBindings.GetAllFromCommands (command).FirstOrDefault () ??
               Application.GetDefaultKey (command);
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
        EditorSettings.Scrollbars =
            Editor.ViewportSettings.HasFlag (ViewportSettingsFlags.HasScrollBars);
        EditorSettings.Save (_configPath);
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
            SetFoldingDocument (null);

            return;
        }

        if (Editor.Document.TextLength > MaximumAutomaticFoldingDocumentLength)
        {
            SetFoldingDocument (null);
            Editor.FoldingManager = null;

            return;
        }

        FoldingManager fm = new (Editor.Document);
        Editor.FoldingManager = fm;
        _braceFoldingStrategy.UpdateFoldings (fm, Editor.Document);
        SetFoldingDocument (Editor.Document);
    }

    private void UpdateFoldings ()
    {
        if (Editor.FoldingManager is null || Editor.Document is null)
        {
            return;
        }

        if (Editor.Document.TextLength > MaximumAutomaticFoldingDocumentLength)
        {
            SetFoldingDocument (null);
            Editor.FoldingManager = null;

            return;
        }

        _braceFoldingStrategy.UpdateFoldings (Editor.FoldingManager,
            Editor.Document);
    }

    private void SetFoldingDocument (TextDocument? document)
    {
        if (ReferenceEquals (_foldingDocument, document))
        {
            return;
        }

        if (_foldingDocument is not null)
        {
            _foldingDocument.Changed -= OnFoldingDocumentChanged;
            _foldingDocument.UpdateFinished -= OnFoldingDocumentUpdateFinished;
        }

        _foldingDocument = document;
        _foldingUpdateNeeded = false;

        if (_foldingDocument is not null)
        {
            _foldingDocument.Changed += OnFoldingDocumentChanged;
            _foldingDocument.UpdateFinished += OnFoldingDocumentUpdateFinished;
        }
    }

    private void OnFoldingDocumentChanged (object? sender, DocumentChangeEventArgs e)
    {
        _foldingUpdateNeeded |= FoldingChangeMayAffectStructure (e);
    }

    private void OnFoldingDocumentUpdateFinished (object? sender, EventArgs e)
    {
        if (!_foldingUpdateNeeded)
        {
            return;
        }

        _foldingUpdateNeeded = false;
        UpdateFoldings ();
    }

    private bool FoldingChangeMayAffectStructure (DocumentChangeEventArgs e)
    {
        if (TryGetMappedStructuralChange (e, out var mappedStructuralChange))
        {
            return mappedStructuralChange;
        }

        return ContainsFoldingStructuralCharacter (e.InsertedText)
               || ContainsFoldingStructuralCharacter (e.RemovedText);
    }

    private bool TryGetMappedStructuralChange (DocumentChangeEventArgs e, out bool structuralChange)
    {
        structuralChange = false;
        OffsetChangeMap map = e.OffsetChangeMap;
        var hasInsertion = false;
        var hasRemoval = false;

        if (map.Count == 0)
        {
            return false;
        }

        foreach (OffsetChangeMapEntry entry in map)
        {
            hasInsertion |= entry.InsertionLength > 0;
            hasRemoval |= entry.RemovalLength > 0;

            if (entry.InsertionLength > 0 && entry.RemovalLength > 0)
            {
                return false;
            }
        }

        if (!hasInsertion && !hasRemoval)
        {
            return false;
        }

        if (hasInsertion && hasRemoval)
        {
            return false;
        }

        structuralChange = hasInsertion
            ? MappedInsertionsContainFoldingStructuralCharacter (map, e.InsertedText, e.Offset)
            : MappedRemovalsContainFoldingStructuralCharacter (map, e.RemovedText, e.Offset, e.RemovalLength);

        return true;
    }

    private bool MappedInsertionsContainFoldingStructuralCharacter (OffsetChangeMap map, ITextSource text,
        int baseOffset)
    {
        if (InsertionEntriesUseInsertedTextCoordinates (map, baseOffset, text.TextLength))
        {
            return MappedInsertionsContainFoldingStructuralCharacterWithoutShift (map, text, baseOffset);
        }

        var insertedShift = 0;

        foreach (OffsetChangeMapEntry entry in map)
        {
            if (entry.InsertionLength == 0)
            {
                continue;
            }

            var relativeOffset = entry.Offset - baseOffset + insertedShift;

            if (ContainsFoldingStructuralCharacter (text, relativeOffset, entry.InsertionLength))
            {
                return true;
            }

            insertedShift += entry.InsertionLength;
        }

        return false;
    }

    private bool MappedInsertionsContainFoldingStructuralCharacterWithoutShift (
        OffsetChangeMap map,
        ITextSource text,
        int baseOffset)
    {
        foreach (OffsetChangeMapEntry entry in map)
        {
            if (entry.InsertionLength == 0)
            {
                continue;
            }

            var relativeOffset = entry.Offset - baseOffset;

            if (ContainsFoldingStructuralCharacter (text, relativeOffset, entry.InsertionLength))
            {
                return true;
            }
        }

        return false;
    }

    private bool MappedRemovalsContainFoldingStructuralCharacter (
        OffsetChangeMap map,
        ITextSource text,
        int baseOffset,
        int removalLength)
    {
        if (!RemovalEntriesUseRemovedTextCoordinates (map, baseOffset, removalLength))
        {
            return MappedInsertionsContainFoldingStructuralCharacter (map.Invert (), text, baseOffset);
        }

        foreach (OffsetChangeMapEntry entry in map)
        {
            if (entry.RemovalLength == 0)
            {
                continue;
            }

            var relativeOffset = entry.Offset - baseOffset;

            if (ContainsFoldingStructuralCharacter (text, relativeOffset, entry.RemovalLength))
            {
                return true;
            }
        }

        return false;
    }

    private static bool RemovalEntriesUseRemovedTextCoordinates (OffsetChangeMap map, int baseOffset, int removalLength)
    {
        return map.Count > 0
               && map[0].RemovalLength > 0
               && map[0].Offset + map[0].RemovalLength == baseOffset + removalLength;
    }

    private static bool InsertionEntriesUseInsertedTextCoordinates (OffsetChangeMap map, int baseOffset,
        int insertionLength)
    {
        return map.Count > 0
               && map[^1].InsertionLength > 0
               && map[^1].Offset + map[^1].InsertionLength == baseOffset + insertionLength;
    }

    private bool ContainsFoldingStructuralCharacter (ITextSource text)
    {
        return ContainsFoldingStructuralCharacter (text, 0, text.TextLength);
    }

    private bool ContainsFoldingStructuralCharacter (ITextSource text, int offset, int length)
    {
        for (var i = offset; i < offset + length; i++)
        {
            var ch = text.GetCharAt (i);

            if (ch == _braceFoldingStrategy.OpeningBrace
                || ch == _braceFoldingStrategy.ClosingBrace
                || ch is '\r' or '\n')
            {
                return true;
            }
        }

        return false;
    }
}

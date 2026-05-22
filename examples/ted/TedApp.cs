// This is a demo of ted, the Terminal.Gui.Editor example.

using System.Text;
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
    // Per-instance config path. Defaults to the real ~/.tui location; tests inject a temp path so they
    // never touch the developer's real config (and stay parallel-safe — no env/static mutation).
    private readonly string _configPath;
    private readonly Shortcut _fileNameShortcut;
    private readonly MenuItem _previewMarkdownMenuItem;

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

        // Enable brace-based folding. The editor handles the full lifecycle automatically.
        Editor.FoldingStrategy = new BraceFoldingStrategy ();

        ShowOpenDialog = ShowDefaultOpenDialog;
        ShowSaveDialog = ShowDefaultSaveDialog;
        ShowSaveChangesDialog = ShowDefaultSaveChangesDialog;

        // --- EditorMenuBar: pre-wired File/Edit/View menus ---
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

        PreviewCheckBox.ValueChanged += (_, e) =>
        {
            ToggleMarkdownPreview ();
            _previewMarkdownMenuItem.Title = ToggleTitle (e.NewValue == CheckState.Checked, "_Preview Markdown");
        };

        Menu = new EditorMenuBar (Editor)
        {
            ShowOpenDialog = () => ShowOpenDialog (),
            ShowSaveDialog = () => ShowSaveDialog ()
        };

        Menu.NewRequested += (_, _) => New ();
        Menu.Opening += (_, e) =>
        {
            if (!ConfirmSaveChanges ())
            {
                e.Cancel = true;
            }
        };
        Menu.OpenRequested += (_, e) =>
        {
            CurrentLoadTask = OpenFileAsync (e.FilePath, true);
        };
        Menu.SaveRequested += (_, _) => Save ();
        Menu.SaveAsRequested += (_, e) => { _ = SaveFileAsAsync (e.FilePath, true); };
        Menu.QuitRequested += (_, _) => Quit ();
        Menu.ViewSettingsChanged += (_, _) => SaveViewSettings ();

        // Ted-specific extra menus: View extras, Options, Help, file-name shortcut
        Menu.ViewMenu.PopoverMenu!.Root!.Add (_previewMarkdownMenuItem);
        Menu.Add (new MenuBarItem ("_Options",
            [new MenuItem ("_Settings...", string.Empty, ShowSettingsDialog)]));
        Menu.Add (new MenuBarItem (Strings.menuHelp,
            [new MenuItem ("_About", "About ted", ShowAboutDialog)]));
        _fileNameShortcut = new Shortcut (Key.Empty, "<untitled>", Open)
        {
            MouseHighlightStates = MouseState.None,
            SchemeName = SchemeManager.SchemesToSchemeName (Schemes.Dialog)
        };
        Menu.Add (_fileNameShortcut);

        // --- EditorStatusBar: pre-wired indicators ---
        StatusBar = new EditorStatusBar (Editor);

        Editor.Y = Pos.Bottom (Menu);
        Editor.Width = Dim.Fill ();
        Editor.Height = Dim.Fill (StatusBar);

        Add (Menu, Editor, StatusBar);

        // Ted-specific event handlers
        Editor.ModifiedChanged += (_, _) => UpdateModifiedStatus ();
        Editor.ContentChanged += (_, e) => UpdateContentSizeStatus (e);
        Editor.FindRequested += (_, _) => ShowFindReplaceDialog (false);
        Editor.ReplaceRequested += (_, _) => ShowFindReplaceDialog (true);
    }

    /// <summary>The editor View at the centre of the app. Exposed for tests and future commands.</summary>
    public Editor Editor { get; }

    /// <summary>The pre-wired menu bar. Exposed for tests.</summary>
    public EditorMenuBar Menu { get; }

    /// <summary>The pre-wired status bar. Exposed for tests.</summary>
    public EditorStatusBar StatusBar { get; }

    /// <summary>The status-bar shortcut that displays the current syntax-highlighting language name.</summary>
    public Shortcut LanguageShortcut => StatusBar.LanguageShortcut;

    /// <summary>The status-bar dropdown that selects <see cref="ThemeManager.Theme" />.</summary>
    public DropDownList ThemeDropDown => StatusBar.ThemeDropDown;

    /// <summary>The spinner view shown while streaming file load/save is running.</summary>
    public SpinnerView LoadStatusSpinner => StatusBar.LoadStatusSpinner;

    /// <summary>The status-bar shortcut that hosts <see cref="LoadStatusSpinner" />.</summary>
    public Shortcut LoadSpinnerShortcut => StatusBar.LoadSpinnerShortcut;

    /// <summary>
    ///     The status-bar shortcut that mirrors the editor's caret position. Both line and column are
    ///     1-based. Updated whenever <see cref="Editor.CaretChanged" /> fires (user-driven movement
    ///     and document edits that shift the caret).
    /// </summary>
    public Shortcut LocShortcut => StatusBar.LocShortcut;

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


    private void UpdateModifiedStatus ()
    {
        // Don't override the status while a streaming load/save is in progress --
        // the streaming operation owns the spinner and operation-id sequence.
        if (LoadStatusSpinner.AutoSpin)
        {
            return;
        }

        var verb = Editor.IsModified ? "Modified" : _lastStatusVerb;
        var status = FormatCompletedProgress (verb, _lastFileByteSize);

        // Set directly rather than going through CompleteStreamingStatus, which would
        // bump the operation-id and potentially invalidate a pending streaming completion.
        // ModifiedChanged always fires on the UI thread, so no marshalling is needed.
        LoadSpinnerShortcut.Title = status;
        LoadSpinnerShortcut.HelpText = status;
        LoadSpinnerShortcut.SetNeedsDraw ();
    }

    private void UpdateContentSizeStatus (DocumentChangeEventArgs e)
    {
        if (LoadStatusSpinner.AutoSpin)
        {
            return;
        }

        if (_lastFileByteSize is not { } currentSize)
        {
            return;
        }

        Encoding encoding = Editor.Document?.Encoding ?? Encoding.UTF8;
        long insertedBytes = encoding.GetByteCount (e.InsertedText.Text);
        long removedBytes = encoding.GetByteCount (e.RemovedText.Text);
        _lastFileByteSize = Math.Max (0, currentSize + insertedBytes - removedBytes);

        UpdateModifiedStatus ();
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
}

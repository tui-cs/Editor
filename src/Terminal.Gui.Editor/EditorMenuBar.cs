using Terminal.Gui.App;
using Terminal.Gui.Input;
using Terminal.Gui.Resources;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace Terminal.Gui.Editor;

/// <summary>
///     A pre-wired <see cref="MenuBar" /> that provides standard File, Edit, and View menus
///     bound to an <see cref="Editor" /> instance. Consumers can append custom menus via
///     <see cref="ExtraMenuItems" /> and customize file-dialog behavior with
///     <see cref="ShowOpenDialog" /> / <see cref="ShowSaveDialog" />.
/// </summary>
/// <remarks>
///     <para>
///         This control does <b>not</b> perform file I/O or persist settings — it raises events
///         (<see cref="NewRequested" />, <see cref="OpenRequested" />, <see cref="SaveRequested" />,
///         <see cref="SaveAsRequested" />, <see cref="QuitRequested" />) and the consumer provides
///         the implementation.
///     </para>
///     <para>
///         For multi-file scenarios (TabView, split panes) use the <see cref="EditorMenuBar(Func{Editor})" />
///         constructor. The delegate is evaluated on every command dispatch, so focus changes are tracked
///         automatically.
///     </para>
/// </remarks>
public class EditorMenuBar : MenuBar
{
    private readonly Func<Editor> _editorProvider;

    private readonly CheckBox _lineNumbersCheckBox;
    private readonly CheckBox _foldIndicatorsCheckBox;
    private readonly CheckBox _wordWrapCheckBox;
    private readonly CheckBox _showTabsCheckBox;
    private readonly CheckBox _scrollbarsCheckBox;

    /// <summary>Initializes a new <see cref="EditorMenuBar" /> wired to a single <see cref="Editor" />.</summary>
    /// <param name="editor">The editor instance whose commands and properties are controlled.</param>
    public EditorMenuBar (Editor editor) : this (() => editor)
    {
        ArgumentNullException.ThrowIfNull (editor);
    }

    /// <summary>
    ///     Initializes a new <see cref="EditorMenuBar" /> for multi-file scenarios where the active
    ///     editor can change (e.g. TabView, split panes).
    /// </summary>
    /// <param name="activeEditorProvider">
    ///     A delegate that returns the currently active <see cref="Editor" />. Evaluated on every
    ///     command dispatch.
    /// </param>
    public EditorMenuBar (Func<Editor> activeEditorProvider)
    {
        ArgumentNullException.ThrowIfNull (activeEditorProvider);
        _editorProvider = activeEditorProvider;

        AlignmentModes = AlignmentModes.IgnoreFirstOrLast;

        // --- View menu checkboxes ---
        _lineNumbersCheckBox = new CheckBox
        {
            AllowCheckStateNone = false,
            CanFocus = false,
            Text = "_Line Numbers",
            Value = ActiveEditor.GutterOptions.HasFlag (GutterOptions.LineNumbers)
                ? CheckState.Checked
                : CheckState.UnChecked
        };

        _foldIndicatorsCheckBox = new CheckBox
        {
            AllowCheckStateNone = false,
            CanFocus = false,
            Text = "_Fold Indicators",
            Value = ActiveEditor.GutterOptions.HasFlag (GutterOptions.Folding)
                ? CheckState.Checked
                : CheckState.UnChecked
        };

        _wordWrapCheckBox = new CheckBox
        {
            AllowCheckStateNone = false,
            CanFocus = false,
            Text = "_Word Wrap",
            Value = ActiveEditor.WordWrap ? CheckState.Checked : CheckState.UnChecked
        };

        _showTabsCheckBox = new CheckBox
        {
            AllowCheckStateNone = false,
            CanFocus = false,
            Text = "Show _Tabs",
            Value = ActiveEditor.ShowTabs ? CheckState.Checked : CheckState.UnChecked
        };

        _scrollbarsCheckBox = new CheckBox
        {
            AllowCheckStateNone = false,
            CanFocus = false,
            Text = "_Scrollbars",
            Value = ActiveEditor.ViewportSettings.HasFlag (ViewportSettingsFlags.HasScrollBars)
                ? CheckState.Checked
                : CheckState.UnChecked
        };
    }

    /// <summary>
    ///     Additional <see cref="MenuBarItem" />s appended after the built-in menus. Add custom menus
    ///     here (e.g. Options, Help) and call <see cref="RebuildMenus" /> to apply.
    /// </summary>
    public IList<MenuBarItem> ExtraMenuItems { get; } = new List<MenuBarItem> ();

    /// <summary>
    ///     Additional <see cref="View" />s appended to the built-in View menu. Add custom menu items
    ///     (e.g. Preview toggles) here and call <see cref="RebuildMenus" /> to apply.
    /// </summary>
    public IList<View> ExtraViewMenuItems { get; } = new List<View> ();

    /// <summary>
    ///     Additional <see cref="View" />s added directly to the menu bar as siblings of the
    ///     <see cref="MenuBarItem" />s (e.g. a file-name shortcut on the right). Added after all
    ///     menu items. Call <see cref="RebuildMenus" /> to apply.
    /// </summary>
    public IList<View> ExtraBarItems { get; } = new List<View> ();

    /// <summary>
    ///     Delegate invoked when the user selects File → Open. Should present a file-open dialog
    ///     and return the chosen path, or <see langword="null" /> to cancel.
    /// </summary>
    public Func<string?>? ShowOpenDialog { get; set; }

    /// <summary>
    ///     Delegate invoked when the user selects File → Save As. Should present a file-save dialog
    ///     and return the chosen path, or <see langword="null" /> to cancel.
    /// </summary>
    public Func<string?>? ShowSaveDialog { get; set; }

    /// <summary>Raised when the user selects File → New.</summary>
    public event EventHandler? NewRequested;

    /// <summary>Raised when the user selects File → Open (after <see cref="ShowOpenDialog" /> returns a path).</summary>
    public event EventHandler<FilePathEventArgs>? OpenRequested;

    /// <summary>Raised when the user selects File → Save.</summary>
    public event EventHandler? SaveRequested;

    /// <summary>Raised when the user selects File → Save As (after <see cref="ShowSaveDialog" /> returns a path).</summary>
    public event EventHandler<FilePathEventArgs>? SaveAsRequested;

    /// <summary>Raised when the user selects File → Quit.</summary>
    public event EventHandler? QuitRequested;

    /// <summary>Raised after any View-menu toggle changes an <see cref="Editor" /> property.</summary>
    public event EventHandler? ViewSettingsChanged;

    /// <summary>Gets the currently active <see cref="Editor" /> from the provider delegate.</summary>
    public Editor ActiveEditor => _editorProvider ();

    /// <summary>
    ///     Builds the menu bar contents. Call once after populating <see cref="ExtraMenuItems" />,
    ///     <see cref="ExtraViewMenuItems" />, and <see cref="ExtraBarItems" />.
    /// </summary>
    public void RebuildMenus ()
    {
        BuildMenus ();
    }

    /// <summary>
    ///     Synchronizes the View-menu checkbox states with the current <see cref="ActiveEditor" /> property values.
    ///     Call when switching the active editor in a multi-file scenario.
    /// </summary>
    public void SyncCheckboxes ()
    {
        Editor editor = ActiveEditor;
        _lineNumbersCheckBox.Value = editor.GutterOptions.HasFlag (GutterOptions.LineNumbers)
            ? CheckState.Checked
            : CheckState.UnChecked;
        _foldIndicatorsCheckBox.Value = editor.GutterOptions.HasFlag (GutterOptions.Folding)
            ? CheckState.Checked
            : CheckState.UnChecked;
        _wordWrapCheckBox.Value = editor.WordWrap ? CheckState.Checked : CheckState.UnChecked;
        _showTabsCheckBox.Value = editor.ShowTabs ? CheckState.Checked : CheckState.UnChecked;
        _scrollbarsCheckBox.Value = editor.ViewportSettings.HasFlag (ViewportSettingsFlags.HasScrollBars)
            ? CheckState.Checked
            : CheckState.UnChecked;
    }

    private void BuildMenus ()
    {
        RemoveAll ();

        MenuBarItem fileMenu = new (Strings.menuFile, CreateFileMenuItems ());
        MenuBarItem editMenu = new (Strings.menuEdit, CreateEditMenuItems ());
        MenuBarItem viewMenu = new ("_View", CreateViewMenuItems ());

        List<View> allItems = [fileMenu, editMenu, viewMenu];

        foreach (MenuBarItem extra in ExtraMenuItems)
        {
            allItems.Add (extra);
        }

        foreach (View extra in ExtraBarItems)
        {
            allItems.Add (extra);
        }

        Add (allItems.ToArray ());
    }

    private View[] CreateFileMenuItems ()
    {
        return
        [
            new MenuItem
            {
                Title = "_New",
                HelpText = "New file",
                Key = Key.N.WithCtrl,
                Action = OnNew
            },
            new MenuItem
            {
                Title = "_Open...",
                HelpText = "Open file",
                Key = Key.O.WithCtrl,
                Action = OnOpen
            },
            new MenuItem
            {
                Title = "_Save",
                HelpText = "Save file",
                Key = Key.S.WithCtrl,
                Action = OnSave
            },
            new MenuItem
            {
                Title = "Save _As...",
                HelpText = "Save file as",
                Key = Key.S.WithCtrl.WithShift,
                Action = OnSaveAs
            },
            new Line (),
            new MenuItem
            {
                Title = "_Quit",
                HelpText = "Quit",
                Key = Application.GetDefaultKey (Command.Quit),
                Action = OnQuit
            }
        ];
    }

    private View[] CreateEditMenuItems ()
    {
        return
        [
            new MenuItem
            {
                Title = "_Find...",
                HelpText = "Find text in the current document",
                Key = KeyFor (Command.Find),
                Action = () => ActiveEditor.InvokeCommand (Command.Find)
            },
            new MenuItem
            {
                Title = "_Replace...",
                HelpText = "Find and replace text in the current document",
                Key = KeyFor (Command.Replace),
                Action = () => ActiveEditor.InvokeCommand (Command.Replace)
            },
            new Line (),
            new MenuItem (ActiveEditor, Command.Undo) { Key = KeyFor (Command.Undo) },
            new MenuItem (ActiveEditor, Command.Redo) { Key = KeyFor (Command.Redo) },
            new Line (),
            new MenuItem (ActiveEditor, Command.Cut) { Key = KeyFor (Command.Cut) },
            new MenuItem (ActiveEditor, Command.Copy) { Key = KeyFor (Command.Copy) },
            new MenuItem (ActiveEditor, Command.Paste) { Key = KeyFor (Command.Paste) },
            new MenuItem (ActiveEditor, Command.SelectAll) { Key = KeyFor (Command.SelectAll) }
        ];
    }

    private View[] CreateViewMenuItems ()
    {
        List<View> items =
        [
            new MenuItem
            {
                Action = ToggleLineNumbers,
                CommandView = _lineNumbersCheckBox
            },
            new MenuItem
            {
                Action = ToggleFoldIndicators,
                CommandView = _foldIndicatorsCheckBox
            },
            new MenuItem
            {
                Action = ToggleWordWrap,
                CommandView = _wordWrapCheckBox
            },
            new MenuItem
            {
                Action = ToggleShowTabs,
                CommandView = _showTabsCheckBox
            },
            new MenuItem
            {
                Action = ToggleScrollbars,
                CommandView = _scrollbarsCheckBox
            }
        ];

        foreach (View extra in ExtraViewMenuItems)
        {
            items.Add (extra);
        }

        return items.ToArray ();
    }

    private Key KeyFor (Command command)
    {
        return ActiveEditor.KeyBindings.GetAllFromCommands (command).FirstOrDefault () ??
               Application.GetDefaultKey (command);
    }

    // --- File command handlers ---

    private void OnNew ()
    {
        NewRequested?.Invoke (this, EventArgs.Empty);
    }

    private void OnOpen ()
    {
        var path = ShowOpenDialog?.Invoke ();

        if (!string.IsNullOrWhiteSpace (path))
        {
            OpenRequested?.Invoke (this, new FilePathEventArgs (path));
        }
    }

    private void OnSave ()
    {
        SaveRequested?.Invoke (this, EventArgs.Empty);
    }

    private void OnSaveAs ()
    {
        var path = ShowSaveDialog?.Invoke ();

        if (!string.IsNullOrWhiteSpace (path))
        {
            SaveAsRequested?.Invoke (this, new FilePathEventArgs (path));
        }
    }

    private void OnQuit ()
    {
        QuitRequested?.Invoke (this, EventArgs.Empty);
    }

    // --- View toggle handlers ---

    private void ToggleLineNumbers ()
    {
        Editor editor = ActiveEditor;
        var shouldEnable = !editor.GutterOptions.HasFlag (GutterOptions.LineNumbers);

        if (shouldEnable)
        {
            editor.GutterOptions |= GutterOptions.LineNumbers;
        }
        else
        {
            editor.GutterOptions &= ~GutterOptions.LineNumbers;
        }

        _lineNumbersCheckBox.Value = shouldEnable ? CheckState.Checked : CheckState.UnChecked;
        editor.SetNeedsDraw ();
        ViewSettingsChanged?.Invoke (this, EventArgs.Empty);
    }

    private void ToggleFoldIndicators ()
    {
        Editor editor = ActiveEditor;
        var shouldEnable = !editor.GutterOptions.HasFlag (GutterOptions.Folding);

        if (shouldEnable)
        {
            editor.GutterOptions |= GutterOptions.Folding;
        }
        else
        {
            editor.GutterOptions &= ~GutterOptions.Folding;
        }

        _foldIndicatorsCheckBox.Value = shouldEnable ? CheckState.Checked : CheckState.UnChecked;
        editor.SetNeedsDraw ();
        ViewSettingsChanged?.Invoke (this, EventArgs.Empty);
    }

    private void ToggleWordWrap ()
    {
        Editor editor = ActiveEditor;
        var wordWrapEnabled = !editor.WordWrap;
        editor.WordWrap = wordWrapEnabled;
        _wordWrapCheckBox.Value = wordWrapEnabled ? CheckState.Checked : CheckState.UnChecked;
        ViewSettingsChanged?.Invoke (this, EventArgs.Empty);
    }

    private void ToggleShowTabs ()
    {
        Editor editor = ActiveEditor;
        var showTabsEnabled = !editor.ShowTabs;
        editor.ShowTabs = showTabsEnabled;
        _showTabsCheckBox.Value = showTabsEnabled ? CheckState.Checked : CheckState.UnChecked;
        ViewSettingsChanged?.Invoke (this, EventArgs.Empty);
    }

    private void ToggleScrollbars ()
    {
        Editor editor = ActiveEditor;
        var shouldEnable = !editor.ViewportSettings.HasFlag (ViewportSettingsFlags.HasScrollBars);

        if (shouldEnable)
        {
            editor.ViewportSettings |= ViewportSettingsFlags.HasScrollBars;
        }
        else
        {
            editor.ViewportSettings &= ~ViewportSettingsFlags.HasScrollBars;
        }

        _scrollbarsCheckBox.Value = shouldEnable ? CheckState.Checked : CheckState.UnChecked;
        editor.SetNeedsDraw ();
        ViewSettingsChanged?.Invoke (this, EventArgs.Empty);
    }
}

/// <summary>Event arguments carrying a file path.</summary>
public class FilePathEventArgs : EventArgs
{
    /// <summary>Initializes a new <see cref="FilePathEventArgs" />.</summary>
    /// <param name="filePath">The file path.</param>
    public FilePathEventArgs (string filePath)
    {
        FilePath = filePath;
    }

    /// <summary>Gets the file path.</summary>
    public string FilePath { get; }
}

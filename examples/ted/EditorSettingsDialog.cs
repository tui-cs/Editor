using Terminal.Gui.Editor;
using Terminal.Gui.Resources;
using Terminal.Gui.Text.Indentation;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace Ted;

internal sealed class EditorSettingsDialog : Dialog
{
    private readonly CheckBox _autoCompleteCheck;
    private readonly NumericUpDown<int> _indentSize;
    private readonly CheckBox _convertTabsCheck;
    private readonly CheckBox _autoIndentCheck;

    internal EditorSettingsDialog (Editor editor)
    {
        Title = "Settings";
        Width = Dim.Percent (60);
        Height = 13;

        // --- Tab Settings tab ---
        View tabSettingsTab = new ()
        {
            Title = "_Tab Settings"
        };

        View label = new Label { Text = "_Indent size:" };
        _indentSize = new NumericUpDown<int>
        {
            X = Pos.Right (label) + 1,
            Value = editor.IndentationSize
        };
        _indentSize.ValueChanging += (_, e) =>
        {
            if (e.NewValue < 1)
            {
                e.Handled = true;
            }
        };

        _convertTabsCheck = new CheckBox
        {
            Y = Pos.Bottom (_indentSize),
            Title = "Con_vert Tabs to Spaces",
            Value = editor.ConvertTabsToSpaces ? CheckState.Checked : CheckState.UnChecked
        };

        _autoIndentCheck = new CheckBox
        {
            Y = Pos.Bottom (_convertTabsCheck),
            Title = "_Auto Indent",
            Value = editor.IndentationStrategy is not null ? CheckState.Checked : CheckState.UnChecked
        };

        tabSettingsTab.Add (
            label,
            _indentSize,
            _convertTabsCheck,
            _autoIndentCheck);

        _autoCompleteCheck = new CheckBox
        {
            Title = "Auto _Complete",
            Value = editor.CompletionProvider is not null ? CheckState.Checked : CheckState.UnChecked
        };

        // --- Config tab ---
        View configTab = new ()
        {
            Title = "_Config"
        };

        configTab.Add (_autoCompleteCheck);

        // --- Tabs ---
        Tabs tabs = new ();

        tabs.InsertTab (0, configTab);
        tabs.InsertTab (1, tabSettingsTab);

        Button okBtn = new ()
        {
            Text = Strings.btnOk
        };

        Button cancelBtn = new ()
        {
            Text = Strings.btnCancel
        };

        okBtn.Accepting += (_, _) =>
        {
            WasAccepted = true;
            RequestStop ();
        };

        cancelBtn.Accepting += (_, _) => RequestStop ();

        AddButton (cancelBtn);
        AddButton (okBtn);
        Add (tabs);
    }

    internal bool WasAccepted { get; private set; }

    internal void ApplyTo (Editor editor)
    {
        editor.IndentationSize = Math.Max (1, _indentSize.Value);
        editor.ConvertTabsToSpaces = _convertTabsCheck.Value == CheckState.Checked;
        editor.IndentationStrategy = _autoIndentCheck.Value == CheckState.Checked
            ? new DefaultIndentationStrategy ()
            : null;
        editor.CompletionProvider = _autoCompleteCheck.Value == CheckState.Checked
            ? new WordCompletionProvider ()
            : null;
    }
}

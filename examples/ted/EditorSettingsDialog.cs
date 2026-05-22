using Terminal.Gui.Editor;
using Terminal.Gui.Resources;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace Ted;

/// <summary>
///     Settings dialog for the editor clet. Provides tabs for Config and Tab Settings.
/// </summary>
internal sealed class EditorSettingsDialog : Dialog
{
    private readonly CheckBox _autoCompleteCheck;
    private readonly EditorTabSettingsTab _tabSettingsTab;

    internal EditorSettingsDialog (Editor editor)
    {
        Title = "Settings";
        Width = Dim.Percent (60);
        Height = 13;

        // --- Tab Settings tab ---
        _tabSettingsTab = new EditorTabSettingsTab (editor);

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
        tabs.InsertTab (1, _tabSettingsTab);

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

    /// <summary>
    ///     Applies the accepted settings to the editor. Call only when <see cref="WasAccepted" /> is true.
    /// </summary>
    internal void ApplyTo (Editor editor)
    {
        _tabSettingsTab.ApplyTo (editor);
        editor.CompletionProvider = _autoCompleteCheck.Value == CheckState.Checked
            ? new WordCompletionProvider ()
            : null;
    }
}

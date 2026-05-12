using Terminal.Gui.Editor.Search;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace Ted;

internal sealed class FindReplaceDialog : Dialog
{
    private readonly TextField _findTextField;
    private readonly TextField _replaceFindTextField;
    private readonly TextField _replaceWithTextField;
    private readonly CheckBox _matchCaseCheckBox;
    private readonly CheckBox _wholeWordCheckBox;
    private readonly CheckBox _regexCheckBox;
    private readonly Label _statusLabel;

    public FindReplaceDialog (Editor editor, bool selectReplaceTab)
    {
        ArgumentNullException.ThrowIfNull (editor);

        Title = "_Find / Replace";
        Width = Dim.Percent (70);
        Height = Dim.Percent (50);

        var initialSearchText = string.Empty;

        if (editor is { HasSelection: true, Document: { } document })
        {
            var start = Math.Clamp (editor.SelectionStart, 0, document.TextLength);
            var end = Math.Clamp (editor.SelectionEnd, start, document.TextLength);
            initialSearchText = document.Text.Substring (start, end - start);
        }

        _findTextField = new TextField { X = 11, Y = 1, Width = Dim.Fill (2), Text = initialSearchText };
        _replaceFindTextField = new TextField { X = 11, Y = 1, Width = Dim.Fill (2), Text = initialSearchText };
        _replaceWithTextField = new TextField { X = 11, Y = 3, Width = Dim.Fill (2) };

        _matchCaseCheckBox = new CheckBox { X = 1, Y = 0, Title = "Match _case" };
        _wholeWordCheckBox = new CheckBox { X = Pos.Right (_matchCaseCheckBox) + 2, Y = 0, Title = "_Whole word" };
        _regexCheckBox = new CheckBox { X = Pos.Right (_wholeWordCheckBox) + 2, Y = 0, Title = "Reg_ex" };
        _statusLabel = new Label { X = 1, Y = 1, Width = Dim.Fill (1) };

        Tabs tabs = new () { X = 0, Y = 3, Width = Dim.Fill (), Height = Dim.Fill () };
        View findTab = BuildFindTab (editor);
        View replaceTab = BuildReplaceTab (editor);

        tabs.Add (findTab);
        tabs.Add (replaceTab);
        tabs.Value = selectReplaceTab ? replaceTab : findTab;

        AddButton (new Button { Text = "_Close" });
        Add (_matchCaseCheckBox, _wholeWordCheckBox, _regexCheckBox, _statusLabel, tabs);

        if (selectReplaceTab)
        {
            _replaceFindTextField.SetFocus ();

            return;
        }

        _findTextField.SetFocus ();
    }

    private View BuildFindTab (Editor editor)
    {
        View tab = new () { Title = "_Find" };
        Button findNextButton = new () { X = 1, Y = 3, Text = "Find _Next" };
        Button findPreviousButton = new () { X = Pos.Right (findNextButton) + 1, Y = 3, Text = "Find _Previous" };

        tab.Add (
                 new Label { X = 1, Y = 1, Text = "_Find:" },
                 _findTextField,
                 findNextButton,
                 findPreviousButton);

        findNextButton.Accepted += (_, _) => RunFind (editor, _findTextField.Text, forward: true);
        findPreviousButton.Accepted += (_, _) => RunFind (editor, _findTextField.Text, forward: false);

        return tab;
    }

    private View BuildReplaceTab (Editor editor)
    {
        View tab = new () { Title = "_Replace" };

        tab.Add (
                 new Label { X = 1, Y = 1, Text = "_Find:" },
                 _replaceFindTextField,
                 new Label { X = 1, Y = 3, Text = "_Replace:" },
                 _replaceWithTextField);

        Button findNextButton = new () { X = 1, Y = 5, Text = "Find _Next" };
        Button replaceButton = new () { X = Pos.Right (findNextButton) + 1, Y = 5, Text = "_Replace" };
        Button replaceAllButton = new () { X = Pos.Right (replaceButton) + 1, Y = 5, Text = "Replace _All" };

        findNextButton.Accepted += (_, _) => RunFind (editor, _replaceFindTextField.Text, forward: true);
        replaceButton.Accepted += (_, _) => RunReplaceNext (editor);
        replaceAllButton.Accepted += (_, _) => RunReplaceAll (editor);

        tab.Add (findNextButton, replaceButton, replaceAllButton);

        return tab;
    }

    private void RunFind (Editor editor, string searchText, bool forward)
    {
        if (!TryBuildStrategy (editor, searchText))
        {
            return;
        }

        bool found = forward ? editor.FindNext () : editor.FindPrevious ();
        _statusLabel.Text = found ? string.Empty : $"No match for \"{searchText}\".";
    }

    private void RunReplaceNext (Editor editor)
    {
        if (!TryBuildStrategy (editor, _replaceFindTextField.Text))
        {
            return;
        }

        bool replaced = editor.ReplaceNext (_replaceWithTextField.Text);
        _statusLabel.Text = replaced ? string.Empty : $"No match for \"{_replaceFindTextField.Text}\".";
    }

    private void RunReplaceAll (Editor editor)
    {
        if (!TryBuildStrategy (editor, _replaceFindTextField.Text))
        {
            return;
        }

        int count = editor.ReplaceAll (_replaceWithTextField.Text);
        _statusLabel.Text = count == 0 ? $"No match for \"{_replaceFindTextField.Text}\"." : $"Replaced {count} occurrence{(count == 1 ? "" : "s")}.";
    }

    /// <summary>
    ///     Builds an <see cref="ISearchStrategy" /> from the dialog's checkbox state and assigns it to
    ///     <see cref="Editor.SearchStrategy" />. Returns <see langword="false" /> (and surfaces the error on the
    ///     status label) when the search text is empty or — in regex mode — fails to compile.
    /// </summary>
    private bool TryBuildStrategy (Editor editor, string searchText)
    {
        if (string.IsNullOrEmpty (searchText))
        {
            _statusLabel.Text = string.Empty;

            return false;
        }

        SearchMode mode = _regexCheckBox.Value == CheckState.Checked ? SearchMode.RegEx : SearchMode.Normal;
        bool ignoreCase = _matchCaseCheckBox.Value != CheckState.Checked;
        bool wholeWord = _wholeWordCheckBox.Value == CheckState.Checked;

        try
        {
            editor.SearchStrategy = SearchStrategyFactory.Create (searchText, ignoreCase, wholeWord, mode);
            _statusLabel.Text = string.Empty;

            return true;
        }
        catch (SearchPatternException ex)
        {
            _statusLabel.Text = $"Regex error: {ex.Message}";

            return false;
        }
    }
}

using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace Ted;

internal sealed class FindReplaceDialog : Dialog
{
    private readonly TextField _findTextField;
    private readonly TextField _replaceFindTextField;
    private readonly TextField _replaceWithTextField;

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

        _findTextField = new () { X = 11, Y = 1, Width = Dim.Fill (2), Text = initialSearchText };
        _replaceFindTextField = new () { X = 11, Y = 1, Width = Dim.Fill (2), Text = initialSearchText };
        _replaceWithTextField = new () { X = 11, Y = 3, Width = Dim.Fill (2) };

        Tabs tabs = new () { Width = Dim.Fill (), Height = Dim.Fill () };
        View findTab = BuildFindTab (editor);
        View replaceTab = BuildReplaceTab (editor);

        tabs.Add (findTab);
        tabs.Add (replaceTab);
        tabs.Value = selectReplaceTab ? replaceTab : findTab;

        AddButton (new Button { Text = "_Close" });
        Add (tabs);

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

        findNextButton.Accepting += (_, _) => editor.FindNext (_findTextField.Text);
        findPreviousButton.Accepting += (_, _) => editor.FindPrevious (_findTextField.Text);

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

        findNextButton.Accepting += (_, _) => editor.FindNext (_replaceFindTextField.Text);
        replaceButton.Accepting += (_, _) => editor.ReplaceNext (
            _replaceFindTextField.Text,
            _replaceWithTextField.Text);
        replaceAllButton.Accepting += (_, _) => editor.ReplaceAll (
            _replaceFindTextField.Text,
            _replaceWithTextField.Text);

        tab.Add (findNextButton, replaceButton, replaceAllButton);

        return tab;
    }
}

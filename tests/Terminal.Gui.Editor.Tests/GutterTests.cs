// Claude - claude-opus-4-7

using Terminal.Gui.Document;
using Terminal.Gui.Document.Folding;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;
using Xunit;

namespace Terminal.Gui.Editor.Tests;

/// <summary>
///     Tests for <see cref="Gutter" /> life-cycle and the <see cref="Views.Editor" />
///     wiring that hosts it as a SubView of <see cref="Padding" />. Rendering / clipping behavior
///     that needs a driver lives in the IntegrationTests project.
/// </summary>
public class GutterTests
{
    [Fact]
    public void Disabled_By_Default ()
    {
        Views.Editor editor = new ();

        Assert.Equal (0, editor.Padding.Thickness.Left);
        Assert.Empty (PaddingSubViewsOf (editor));
    }

    [Fact]
    public void Enabled_Adds_Gutter_As_PaddingSubView ()
    {
        Views.Editor editor = new () { Document = new TextDocument ("a\nb") };

        editor.GutterOptions = GutterOptions.LineNumbers;

        IReadOnlyCollection<View> subViews = PaddingSubViewsOf (editor);
        Assert.Single (subViews);
        Assert.IsType<Gutter> (subViews.Single ());
    }

    [Fact]
    public void Disabling_Removes_Gutter_From_Padding ()
    {
        Views.Editor editor = new () { Document = new TextDocument ("a\nb") };
        editor.GutterOptions = GutterOptions.LineNumbers;
        Assert.Single (PaddingSubViewsOf (editor));

        editor.GutterOptions = GutterOptions.None;

        Assert.Empty (PaddingSubViewsOf (editor));
    }

    [Fact]
    public void Toggling_Repeatedly_Does_Not_Leak_SubViews ()
    {
        Views.Editor editor = new () { Document = new TextDocument ("a\nb") };

        for (var i = 0; i < 5; i++)
        {
            editor.GutterOptions = GutterOptions.LineNumbers;
            editor.GutterOptions = GutterOptions.None;
        }

        editor.GutterOptions = GutterOptions.LineNumbers;

        Assert.Single (PaddingSubViewsOf (editor));
    }

    [Fact]
    public void Width_Tracks_Padding_Thickness_When_LineCount_Grows ()
    {
        Views.Editor editor = new () { Document = new TextDocument (string.Join ('\n', Enumerable.Range (1, 9))) };
        editor.GutterOptions = GutterOptions.LineNumbers;

        Gutter view = (Gutter)PaddingSubViewsOf (editor).Single ();
        Assert.Equal (Dim.Absolute (2), view.Width);

        editor.Document!.Insert (editor.Document.TextLength, "\n10");

        // Padding widened from 2 to 3; the Gutter's width should match.
        Assert.Equal (3, editor.Padding.Thickness.Left);
        Assert.Equal (Dim.Absolute (3), view.Width);
    }

    [Fact]
    public void Constructor_Throws_On_Null_Editor ()
    {
        Assert.Throws<ArgumentNullException> (() => new Gutter (null!));
    }

    [Fact]
    public void Gutter_CanFocus_Is_False ()
    {
        Views.Editor editor = new () { Document = new TextDocument ("a") };
        Gutter view = new (editor);

        Assert.False (view.CanFocus);
    }

    [Fact]
    public void Gutter_Has_LineNumberGutter_SubView_Without_Folding ()
    {
        Views.Editor editor = new () { Document = new TextDocument ("a\nb\nc") };
        editor.GutterOptions = GutterOptions.LineNumbers;

        Gutter gutter = (Gutter)PaddingSubViewsOf (editor).Single ();

        // Should have exactly one subview: LineNumberGutter.
        Assert.Single (gutter.SubViews);
        Assert.IsType<LineNumberGutter> (gutter.SubViews.First ());
    }

    [Fact]
    public void Gutter_Has_Both_SubViews_With_Folding ()
    {
        Views.Editor editor = new () { Document = new TextDocument ("{\n  a\n}\n") };
        editor.GutterOptions = GutterOptions.LineNumbers | GutterOptions.Folding;
        editor.FoldingManager = new FoldingManager (editor.Document!);

        Gutter gutter = (Gutter)PaddingSubViewsOf (editor).Single ();

        // Should have two subviews: FoldingGutter and LineNumberGutter.
        Assert.Equal (2, gutter.SubViews.Count);
        Assert.Contains (gutter.SubViews, v => v is FoldingGutter);
        Assert.Contains (gutter.SubViews, v => v is LineNumberGutter);
    }

    [Fact]
    public void LineNumberGutter_Width_Is_Fill_Not_Zero ()
    {
        Views.Editor editor = new () { Document = new TextDocument ("{\n  a\n}\n") };
        editor.GutterOptions = GutterOptions.LineNumbers | GutterOptions.Folding;
        editor.FoldingManager = new FoldingManager (editor.Document!);

        Gutter gutter = (Gutter)PaddingSubViewsOf (editor).Single ();
        View lineNumbers = gutter.SubViews.First (v => v is LineNumberGutter);

        // LineNumberGutter.Width must be Dim.Fill(2) to leave room for the 2-col FoldingGutter.
        Assert.Equal (Dim.Fill (2), lineNumbers.Width);
    }

    [Fact]
    public void FoldingGutter_Has_Toggle_MouseBinding ()
    {
        Views.Editor editor = new () { Document = new TextDocument ("{\n  a\n}\n") };
        editor.GutterOptions = GutterOptions.LineNumbers | GutterOptions.Folding;
        editor.FoldingManager = new FoldingManager (editor.Document!);

        Gutter gutter = (Gutter)PaddingSubViewsOf (editor).Single ();
        View foldingGutter = gutter.SubViews.First (v => v is FoldingGutter);

        // FoldingGutter should have a mouse binding for LeftButtonClicked → Toggle.
        IEnumerable<MouseFlags> bindings = foldingGutter.MouseBindings.GetAllFromCommands (Command.Toggle);
        Assert.NotEmpty (bindings);
    }

    [Fact]
    public void Folding_Only_Shows_FoldingGutter_Without_LineNumbers ()
    {
        Views.Editor editor = new () { Document = new TextDocument ("{\n  a\n}\n") };
        editor.GutterOptions = GutterOptions.Folding;
        editor.FoldingManager = new FoldingManager (editor.Document!);

        Assert.Equal (2, editor.Padding.Thickness.Left);

        Gutter gutter = (Gutter)PaddingSubViewsOf (editor).Single ();
        Assert.Single (gutter.SubViews);
        Assert.IsType<FoldingGutter> (gutter.SubViews.First ());
    }

    [Fact]
    public void Folding_Flag_Without_FoldingManager_Has_No_Effect ()
    {
        Views.Editor editor = new () { Document = new TextDocument ("a\nb") };
        editor.GutterOptions = GutterOptions.Folding;

        // No FoldingManager set — padding should remain 0 (folding flag alone is not enough).
        Assert.Equal (0, editor.Padding.Thickness.Left);
        Assert.Empty (PaddingSubViewsOf (editor));
    }

    [Fact]
    public void Gutter_Has_Scroll_MouseBindings ()
    {
        Views.Editor editor = new () { Document = new TextDocument ("a\nb\nc") };
        editor.GutterOptions = GutterOptions.LineNumbers;

        Gutter gutter = (Gutter)PaddingSubViewsOf (editor).Single ();

        // Gutter itself should have wheel bindings.
        Assert.NotEmpty (gutter.MouseBindings.GetAllFromCommands (Command.ScrollUp));
        Assert.NotEmpty (gutter.MouseBindings.GetAllFromCommands (Command.ScrollDown));

        // LineNumberGutter subview should also have wheel bindings.
        View lineNumbers = gutter.SubViews.First (v => v is LineNumberGutter);
        Assert.NotEmpty (lineNumbers.MouseBindings.GetAllFromCommands (Command.ScrollUp));
        Assert.NotEmpty (lineNumbers.MouseBindings.GetAllFromCommands (Command.ScrollDown));
    }

    [Fact]
    public void Editor_Has_ScrollCommands_In_CommandsToBubbleUp ()
    {
        Views.Editor editor = new ();

        Assert.Contains (Command.ScrollUp, editor.CommandsToBubbleUp);
        Assert.Contains (Command.ScrollDown, editor.CommandsToBubbleUp);
    }

    private static IReadOnlyCollection<View> PaddingSubViewsOf (Views.Editor editor)
    {
        // Avoid forcing AdornmentView allocation in the "no padding" case so we can also assert
        // that subviews are absent without observing GetOrCreateView side-effects.
        return editor.Padding.View?.SubViews ?? [];
    }
}

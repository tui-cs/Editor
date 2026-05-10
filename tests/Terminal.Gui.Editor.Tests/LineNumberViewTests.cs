// Claude - claude-opus-4-7

using Terminal.Gui.Text.Document;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;
using Xunit;

namespace Terminal.Gui.Editor.Tests;

/// <summary>
///     Tests for <see cref="LineNumberView" /> life-cycle and the <see cref="Views.Editor" />
///     wiring that hosts it as a SubView of <see cref="Padding" />. Rendering / clipping behavior
///     that needs a driver lives in the IntegrationTests project.
/// </summary>
public class LineNumberViewTests
{
    [Fact]
    public void Disabled_By_Default ()
    {
        Views.Editor editor = new ();

        Assert.Equal (0, editor.Padding.Thickness.Left);
        Assert.Empty (PaddingSubViewsOf (editor));
    }

    [Fact]
    public void Enabled_Adds_LineNumberView_As_PaddingSubView ()
    {
        Views.Editor editor = new () { Document = new TextDocument ("a\nb") };

        editor.ShowLineNumbers = true;

        IReadOnlyCollection<View> subViews = PaddingSubViewsOf (editor);
        Assert.Single (subViews);
        Assert.IsType<LineNumberView> (subViews.Single ());
    }

    [Fact]
    public void Disabling_Removes_LineNumberView_From_Padding ()
    {
        Views.Editor editor = new () { Document = new TextDocument ("a\nb") };
        editor.ShowLineNumbers = true;
        Assert.Single (PaddingSubViewsOf (editor));

        editor.ShowLineNumbers = false;

        Assert.Empty (PaddingSubViewsOf (editor));
    }

    [Fact]
    public void Toggling_Repeatedly_Does_Not_Leak_SubViews ()
    {
        Views.Editor editor = new () { Document = new TextDocument ("a\nb") };

        for (var i = 0; i < 5; i++)
        {
            editor.ShowLineNumbers = true;
            editor.ShowLineNumbers = false;
        }

        editor.ShowLineNumbers = true;

        Assert.Single (PaddingSubViewsOf (editor));
    }

    [Fact]
    public void Width_Tracks_Padding_Thickness_When_LineCount_Grows ()
    {
        Views.Editor editor = new () { Document = new TextDocument (string.Join ('\n', Enumerable.Range (1, 9))) };
        editor.ShowLineNumbers = true;

        LineNumberView view = (LineNumberView)PaddingSubViewsOf (editor).Single ();
        Assert.Equal (Dim.Absolute (2), view.Width);

        editor.Document!.Insert (editor.Document.TextLength, "\n10");

        // Padding widened from 2 to 3; the LineNumberView's width should match.
        Assert.Equal (3, editor.Padding.Thickness.Left);
        Assert.Equal (Dim.Absolute (3), view.Width);
    }

    [Fact]
    public void Constructor_Throws_On_Null_Editor ()
    {
        Assert.Throws<ArgumentNullException> (() => new LineNumberView (null!));
    }

    [Fact]
    public void LineNumberView_CanFocus_Is_False ()
    {
        Views.Editor editor = new () { Document = new TextDocument ("a") };
        LineNumberView view = new (editor);

        Assert.False (view.CanFocus);
    }

    private static IReadOnlyCollection<View> PaddingSubViewsOf (Views.Editor editor)
    {
        // Avoid forcing AdornmentView allocation in the "no padding" case so we can also assert
        // that subviews are absent without observing GetOrCreateView side-effects.
        return editor.Padding.View?.SubViews ?? [];
    }
}

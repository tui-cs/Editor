// Adapted for Terminal.Gui from AvaloniaEdit d7a6b63
// Copyright (c) 2014 AlphaSierraPapa for the SharpDevelop Team
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy of this
// software and associated documentation files (the "Software"), to deal in the Software
// without restriction, including without limitation the rights to use, copy, modify, merge,
// publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons
// to whom the Software is furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in all copies or
// substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED,
// INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR
// PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE
// FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR
// OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER
// DEALINGS IN THE SOFTWARE.


using System.Text;
using Terminal.Gui.Editor.Document.Utils;
using Xunit;

namespace Terminal.Gui.Editor.Tests.Utils;

public class RopeTests
{
    [Fact]
    public void EmptyRope ()
    {
        Rope<char> empty = new ();
        Assert.Equal (0, empty.Length);
        Assert.Equal ("", empty.ToString ());
    }

    [Fact]
    public void EmptyRopeFromString ()
    {
        Rope<char> empty = new (string.Empty);
        Assert.Equal (0, empty.Length);
        Assert.Equal ("", empty.ToString ());
    }

    [Fact]
    public void InitializeRopeFromShortString ()
    {
        Rope<char> rope = new ("Hello, World");
        Assert.Equal (12, rope.Length);
        Assert.Equal ("Hello, World", rope.ToString ());
    }

    private static string BuildLongString (int lines)
    {
        using StringWriter w = new ();
        w.NewLine = "\n";
        for (var i = 1; i <= lines; i++)
        {
            w.WriteLine (i.ToString ());
        }

        return w.ToString ();
    }

    [Fact]
    public void InitializeRopeFromLongString ()
    {
        var text = BuildLongString (1000);
        Rope<char> rope = new (text);
        Assert.Equal (text.Length, rope.Length);
        Assert.Equal (text, rope.ToString ());
        Assert.Equal (text.ToCharArray (), rope.ToArray ());
    }

    [Fact]
    public void TestToArrayAndToStringWithParts ()
    {
        var text = BuildLongString (1000);
        Rope<char> rope = new (text);

        var textPart = text.Substring (1200, 600);
        var arrayPart = textPart.ToCharArray ();
        Assert.Equal (textPart, rope.ToString (1200, 600));
        Assert.Equal (arrayPart, rope.ToArray (1200, 600));

        Rope<char> partialRope = rope.GetRange (1200, 600);
        Assert.Equal (textPart, partialRope.ToString ());
        Assert.Equal (arrayPart, partialRope.ToArray ());
    }

    [Fact]
    public void ConcatenateStringToRope ()
    {
        StringBuilder b = new ();
        Rope<char> rope = new ();
        for (var i = 1; i <= 1000; i++)
        {
            b.Append (i.ToString ());
            rope.AddText (i.ToString ());
            b.Append (' ');
            rope.Add (' ');
        }

        Assert.Equal (b.ToString (), rope.ToString ());
    }

    [Fact]
    public void ConcatenateSmallRopesToRope ()
    {
        StringBuilder b = new ();
        Rope<char> rope = new ();
        for (var i = 1; i <= 1000; i++)
        {
            b.Append (i.ToString ());
            b.Append (' ');
            rope.AddRange (CharRope.Create (i + " "));
        }

        Assert.Equal (b.ToString (), rope.ToString ());
    }

    [Fact]
    public void AppendLongTextToEmptyRope ()
    {
        var text = BuildLongString (1000);
        Rope<char> rope = new ();
        rope.AddText (text);
        Assert.Equal (text, rope.ToString ());
    }

    [Fact]
    public void ConcatenateStringToRopeBackwards ()
    {
        StringBuilder b = new ();
        Rope<char> rope = new ();
        for (var i = 1; i <= 1000; i++)
        {
            b.Append (i.ToString ());
            b.Append (' ');
        }

        for (var i = 1000; i >= 1; i--)
        {
            rope.Insert (0, ' ');
            rope.InsertText (0, i.ToString ());
        }

        Assert.Equal (b.ToString (), rope.ToString ());
    }

    [Fact]
    public void ConcatenateSmallRopesToRopeBackwards ()
    {
        StringBuilder b = new ();
        Rope<char> rope = new ();
        for (var i = 1; i <= 1000; i++)
        {
            b.Append (i.ToString ());
            b.Append (' ');
        }

        for (var i = 1000; i >= 1; i--)
        {
            rope.InsertRange (0, CharRope.Create (i + " "));
        }

        Assert.Equal (b.ToString (), rope.ToString ());
    }

    [Fact]
    public void ConcatenateStringToRopeByInsertionInMiddle ()
    {
        StringBuilder b = new ();
        Rope<char> rope = new ();
        for (var i = 1; i <= 998; i++)
        {
            b.Append (i.ToString ("d3"));
            b.Append (' ');
        }

        var middle = 0;
        for (var i = 1; i <= 499; i++)
        {
            rope.InsertText (middle, i.ToString ("d3"));
            middle += 3;
            rope.Insert (middle, ' ');
            middle++;
            rope.InsertText (middle, (999 - i).ToString ("d3"));
            rope.Insert (middle + 3, ' ');
        }

        Assert.Equal (b.ToString (), rope.ToString ());
    }

    [Fact]
    public void ConcatenateSmallRopesByInsertionInMiddle ()
    {
        StringBuilder b = new ();
        Rope<char> rope = new ();
        for (var i = 1; i <= 1000; i++)
        {
            b.Append (i.ToString ("d3"));
            b.Append (' ');
        }

        var middle = 0;
        for (var i = 1; i <= 500; i++)
        {
            rope.InsertRange (middle, CharRope.Create (i.ToString ("d3") + " "));
            middle += 4;
            rope.InsertRange (middle, CharRope.Create ((1001 - i).ToString ("d3") + " "));
        }

        Assert.Equal (b.ToString (), rope.ToString ());
    }
}

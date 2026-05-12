// Adapted for Terminal.Gui from AvaloniaEdit d7a6b63
﻿// Copyright (c) 2014 AlphaSierraPapa for the SharpDevelop Team
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


using Terminal.Gui.Document;
using Xunit;

namespace Terminal.Gui.Editor.Tests.Document;

public class ChangeTrackingTest
{
    [Fact]
    public void NoChanges ()
    {
        TextDocument document = new ("initial text");
        ITextSource snapshot1 = document.CreateSnapshot ();
        ITextSource snapshot2 = document.CreateSnapshot ();
        Assert.Equal (0, snapshot1.Version.CompareAge (snapshot2.Version));
        Assert.Equal (0, snapshot1.Version.GetChangesTo (snapshot2.Version).Count ());
        Assert.Equal (document.Text, snapshot1.Text);
        Assert.Equal (document.Text, snapshot2.Text);
    }

    [Fact]
    public void ForwardChanges ()
    {
        TextDocument document = new ("initial text");
        ITextSource snapshot1 = document.CreateSnapshot ();
        document.Replace (0, 7, "nw");
        document.Insert (1, "e");
        ITextSource snapshot2 = document.CreateSnapshot ();
        Assert.Equal (-1, snapshot1.Version.CompareAge (snapshot2.Version));
        TextChangeEventArgs[] arr = snapshot1.Version.GetChangesTo (snapshot2.Version).ToArray ();
        Assert.Equal (2, arr.Length);
        Assert.Equal ("nw", arr[0].InsertedText.Text);
        Assert.Equal ("e", arr[1].InsertedText.Text);

        Assert.Equal ("initial text", snapshot1.Text);
        Assert.Equal ("new text", snapshot2.Text);
    }

    [Fact]
    public void BackwardChanges ()
    {
        TextDocument document = new ("initial text");
        ITextSource snapshot1 = document.CreateSnapshot ();
        document.Replace (0, 7, "nw");
        document.Insert (1, "e");
        ITextSource snapshot2 = document.CreateSnapshot ();
        Assert.Equal (1, snapshot2.Version.CompareAge (snapshot1.Version));
        TextChangeEventArgs[] arr = snapshot2.Version.GetChangesTo (snapshot1.Version).ToArray ();
        Assert.Equal (2, arr.Length);
        Assert.Equal ("", arr[0].InsertedText.Text);
        Assert.Equal ("initial", arr[1].InsertedText.Text);

        Assert.Equal ("initial text", snapshot1.Text);
        Assert.Equal ("new text", snapshot2.Text);
    }
}

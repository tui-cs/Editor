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


using Terminal.Gui.Editor.Document;
using Xunit;

namespace Terminal.Gui.Editor.Tests.Document;

public class TextSegmentTreeTest
{
    private readonly List<TestTextSegment> expectedSegments;
    private readonly Random rnd;

    private readonly TextSegmentCollection<TestTextSegment> tree;


    public TextSegmentTreeTest ()
    {
        tree = new TextSegmentCollection<TestTextSegment> ();
        expectedSegments = new List<TestTextSegment> ();
        var seed = Environment.TickCount;
        Console.WriteLine ("TextSegmentTreeTest Seed: " + seed);
        rnd = new Random (seed);
    }

    [Fact]
    public void FindInEmptyTree ()
    {
        Assert.Same (null, tree.FindFirstSegmentWithStartAfter (0));
        Assert.Equal (0, tree.FindSegmentsContaining (0).Count);
        Assert.Equal (0, tree.FindOverlappingSegments (10, 20).Count);
    }

    [Fact]
    public void FindFirstSegmentWithStartAfter ()
    {
        TestTextSegment s1 = new (5, 10);
        TestTextSegment s2 = new (10, 10);
        tree.Add (s1);
        tree.Add (s2);
        Assert.Same (s1, tree.FindFirstSegmentWithStartAfter (-100));
        Assert.Same (s1, tree.FindFirstSegmentWithStartAfter (0));
        Assert.Same (s1, tree.FindFirstSegmentWithStartAfter (4));
        Assert.Same (s1, tree.FindFirstSegmentWithStartAfter (5));
        Assert.Same (s2, tree.FindFirstSegmentWithStartAfter (6));
        Assert.Same (s2, tree.FindFirstSegmentWithStartAfter (9));
        Assert.Same (s2, tree.FindFirstSegmentWithStartAfter (10));
        Assert.Same (null, tree.FindFirstSegmentWithStartAfter (11));
        Assert.Same (null, tree.FindFirstSegmentWithStartAfter (100));
    }

    [Fact]
    public void FindFirstSegmentWithStartAfterWithDuplicates ()
    {
        TestTextSegment s1 = new (5, 10);
        TestTextSegment s1b = new (5, 7);
        TestTextSegment s2 = new (10, 10);
        TestTextSegment s2b = new (10, 7);
        tree.Add (s1);
        tree.Add (s1b);
        tree.Add (s2);
        tree.Add (s2b);
        Assert.Same (s1b, tree.GetNextSegment (s1));
        Assert.Same (s2b, tree.GetNextSegment (s2));
        Assert.Same (s1, tree.FindFirstSegmentWithStartAfter (-100));
        Assert.Same (s1, tree.FindFirstSegmentWithStartAfter (0));
        Assert.Same (s1, tree.FindFirstSegmentWithStartAfter (4));
        Assert.Same (s1, tree.FindFirstSegmentWithStartAfter (5));
        Assert.Same (s2, tree.FindFirstSegmentWithStartAfter (6));
        Assert.Same (s2, tree.FindFirstSegmentWithStartAfter (9));
        Assert.Same (s2, tree.FindFirstSegmentWithStartAfter (10));
        Assert.Same (null, tree.FindFirstSegmentWithStartAfter (11));
        Assert.Same (null, tree.FindFirstSegmentWithStartAfter (100));
    }

    [Fact]
    public void FindFirstSegmentWithStartAfterWithDuplicates2 ()
    {
        TestTextSegment s1 = new (5, 1);
        TestTextSegment s2 = new (5, 2);
        TestTextSegment s3 = new (5, 3);
        TestTextSegment s4 = new (5, 4);
        tree.Add (s1);
        tree.Add (s2);
        tree.Add (s3);
        tree.Add (s4);
        Assert.Same (s1, tree.FindFirstSegmentWithStartAfter (0));
        Assert.Same (s1, tree.FindFirstSegmentWithStartAfter (1));
        Assert.Same (s1, tree.FindFirstSegmentWithStartAfter (4));
        Assert.Same (s1, tree.FindFirstSegmentWithStartAfter (5));
        Assert.Same (null, tree.FindFirstSegmentWithStartAfter (6));
    }

    private TestTextSegment AddSegment (int offset, int length)
    {
//			Console.WriteLine("Add " + offset + ", " + length);
        TestTextSegment s = new (offset, length);
        tree.Add (s);
        expectedSegments.Add (s);
        return s;
    }

    private void RemoveSegment (TestTextSegment s)
    {
//			Console.WriteLine("Remove " + s);
        expectedSegments.Remove (s);
        tree.Remove (s);
    }

    private void TestRetrieval (int offset, int length)
    {
        HashSet<TestTextSegment> actual = new (tree.FindOverlappingSegments (offset, length));
        HashSet<TestTextSegment> expected = new ();
        foreach (TestTextSegment e in expectedSegments)
        {
            if (e.ExpectedOffset + e.ExpectedLength < offset)
            {
                continue;
            }

            if (e.ExpectedOffset > offset + length)
            {
                continue;
            }

            expected.Add (e);
        }

        Assert.True (actual.IsSubsetOf (expected));
        Assert.True (expected.IsSubsetOf (actual));
    }

    private void CheckSegments ()
    {
        Assert.Equal (expectedSegments.Count, tree.Count);
        foreach (TestTextSegment s in expectedSegments)
        {
            Assert.Equal (s.ExpectedOffset, s.StartOffset /*, "startoffset for " + s*/);
            Assert.Equal (s.ExpectedLength, s.Length /*, "length for " + s*/);
        }
    }

    [Fact]
    public void AddSegments ()
    {
        TestTextSegment s1 = AddSegment (10, 20);
        TestTextSegment s2 = AddSegment (15, 10);
        CheckSegments ();
    }

    private void ChangeDocument (OffsetChangeMapEntry change)
    {
        tree.UpdateOffsets (change);
        foreach (TestTextSegment s in expectedSegments)
        {
            var endOffset = s.ExpectedOffset + s.ExpectedLength;
            s.ExpectedOffset = change.GetNewOffset (s.ExpectedOffset, AnchorMovementType.AfterInsertion);
            s.ExpectedLength = Math.Max (0,
                change.GetNewOffset (endOffset, AnchorMovementType.BeforeInsertion) - s.ExpectedOffset);
        }
    }

    [Fact]
    public void InsertionBeforeAllSegments ()
    {
        TestTextSegment s1 = AddSegment (10, 20);
        TestTextSegment s2 = AddSegment (15, 10);
        ChangeDocument (new OffsetChangeMapEntry (5, 0, 2));
        CheckSegments ();
    }

    [Fact]
    public void ReplacementBeforeAllSegmentsTouchingFirstSegment ()
    {
        TestTextSegment s1 = AddSegment (10, 20);
        TestTextSegment s2 = AddSegment (15, 10);
        ChangeDocument (new OffsetChangeMapEntry (5, 5, 2));
        CheckSegments ();
    }

    [Fact]
    public void InsertionAfterAllSegments ()
    {
        TestTextSegment s1 = AddSegment (10, 20);
        TestTextSegment s2 = AddSegment (15, 10);
        ChangeDocument (new OffsetChangeMapEntry (45, 0, 2));
        CheckSegments ();
    }

    [Fact]
    public void ReplacementOverlappingWithStartOfSegment ()
    {
        TestTextSegment s1 = AddSegment (10, 20);
        TestTextSegment s2 = AddSegment (15, 10);
        ChangeDocument (new OffsetChangeMapEntry (9, 7, 2));
        CheckSegments ();
    }

    [Fact]
    public void ReplacementOfWholeSegment ()
    {
        TestTextSegment s1 = AddSegment (10, 20);
        TestTextSegment s2 = AddSegment (15, 10);
        ChangeDocument (new OffsetChangeMapEntry (10, 20, 30));
        CheckSegments ();
    }

    [Fact]
    public void ReplacementAtEndOfSegment ()
    {
        TestTextSegment s1 = AddSegment (10, 20);
        TestTextSegment s2 = AddSegment (15, 10);
        ChangeDocument (new OffsetChangeMapEntry (24, 6, 10));
        CheckSegments ();
    }

    [Fact]
    public void RandomizedNoDocumentChanges ()
    {
        for (var i = 0; i < 1000; i++)
        {
//				Console.WriteLine(tree.GetTreeAsString());
//				Console.WriteLine("Iteration " + i);

            switch (rnd.Next (3))
            {
                case 0:
                    AddSegment (rnd.Next (500), rnd.Next (30));
                    break;
                case 1:
                    AddSegment (rnd.Next (500), rnd.Next (300));
                    break;
                case 2:
                    if (tree.Count > 0)
                    {
                        RemoveSegment (expectedSegments[rnd.Next (tree.Count)]);
                    }

                    break;
            }

            CheckSegments ();
        }
    }

    [Fact]
    public void RandomizedCloseNoDocumentChanges ()
    {
        // Lots of segments in a short document. Tests how the tree copes with multiple identical segments.
        for (var i = 0; i < 1000; i++)
        {
            switch (rnd.Next (3))
            {
                case 0:
                    AddSegment (rnd.Next (20), rnd.Next (10));
                    break;
                case 1:
                    AddSegment (rnd.Next (20), rnd.Next (20));
                    break;
                case 2:
                    if (tree.Count > 0)
                    {
                        RemoveSegment (expectedSegments[rnd.Next (tree.Count)]);
                    }

                    break;
            }

            CheckSegments ();
        }
    }

    [Fact]
    public void RandomizedRetrievalTest ()
    {
        for (var i = 0; i < 1000; i++)
        {
            AddSegment (rnd.Next (500), rnd.Next (300));
        }

        CheckSegments ();
        for (var i = 0; i < 1000; i++)
        {
            TestRetrieval (rnd.Next (1000) - 100, rnd.Next (500));
        }
    }

    [Fact]
    public void RandomizedWithDocumentChanges ()
    {
        for (var i = 0; i < 500; i++)
        {
//				Console.WriteLine(tree.GetTreeAsString());
//				Console.WriteLine("Iteration " + i);

            switch (rnd.Next (6))
            {
                case 0:
                    AddSegment (rnd.Next (500), rnd.Next (30));
                    break;
                case 1:
                    AddSegment (rnd.Next (500), rnd.Next (300));
                    break;
                case 2:
                    if (tree.Count > 0)
                    {
                        RemoveSegment (expectedSegments[rnd.Next (tree.Count)]);
                    }

                    break;
                case 3:
                    ChangeDocument (new OffsetChangeMapEntry (rnd.Next (800), rnd.Next (50), rnd.Next (50)));
                    break;
                case 4:
                    ChangeDocument (new OffsetChangeMapEntry (rnd.Next (800), 0, rnd.Next (50)));
                    break;
                case 5:
                    ChangeDocument (new OffsetChangeMapEntry (rnd.Next (800), rnd.Next (50), 0));
                    break;
            }

            CheckSegments ();
        }
    }

    [Fact]
    public void RandomizedWithDocumentChangesClose ()
    {
        for (var i = 0; i < 500; i++)
        {
//				Console.WriteLine(tree.GetTreeAsString());
//				Console.WriteLine("Iteration " + i);

            switch (rnd.Next (6))
            {
                case 0:
                    AddSegment (rnd.Next (50), rnd.Next (30));
                    break;
                case 1:
                    AddSegment (rnd.Next (50), rnd.Next (3));
                    break;
                case 2:
                    if (tree.Count > 0)
                    {
                        RemoveSegment (expectedSegments[rnd.Next (tree.Count)]);
                    }

                    break;
                case 3:
                    ChangeDocument (new OffsetChangeMapEntry (rnd.Next (80), rnd.Next (10), rnd.Next (10)));
                    break;
                case 4:
                    ChangeDocument (new OffsetChangeMapEntry (rnd.Next (80), 0, rnd.Next (10)));
                    break;
                case 5:
                    ChangeDocument (new OffsetChangeMapEntry (rnd.Next (80), rnd.Next (10), 0));
                    break;
            }

            CheckSegments ();
        }
    }

    private class TestTextSegment : TextSegment
    {
        internal int ExpectedOffset, ExpectedLength;

        public TestTextSegment (int expectedOffset, int expectedLength)
        {
            ExpectedOffset = expectedOffset;
            ExpectedLength = expectedLength;
            StartOffset = expectedOffset;
            Length = expectedLength;
        }
    }
}

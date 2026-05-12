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

using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;


using Terminal.Gui.Editor.Document;
using Terminal.Gui.Editor.Utils;
namespace Terminal.Gui.Editor.Tests.Document
{
    
    public class LineManagerTests
    {
        TextDocument document;

        
        public LineManagerTests()
        {
            document = new TextDocument();
        }

        [Fact]
        public void CheckEmptyDocument()
        {
            Assert.Equal("", document.Text);
            Assert.Equal(0, document.TextLength);
            Assert.Equal(1, document.LineCount);
        }

        [Fact]
        public void CheckClearingDocument()
        {
            document.Text = "Hello,\nWorld!";
            Assert.Equal(2, document.LineCount);
            var oldLines = document.Lines.ToArray();
            document.Text = "";
            Assert.Equal("", document.Text);
            Assert.Equal(0, document.TextLength);
            Assert.Equal(1, document.LineCount);
            Assert.Same(oldLines[0], document.Lines.Single());
            Assert.False(oldLines[0].IsDeleted);
            Assert.True(oldLines[1].IsDeleted);
            Assert.Null(oldLines[0].NextLine);
            Assert.Null(oldLines[1].PreviousLine);
        }

        [Fact]
        public void CheckGetLineInEmptyDocument()
        {
            Assert.Equal(1, document.Lines.Count);
            List<DocumentLine> lines = new List<DocumentLine>(document.Lines);
            Assert.Equal(1, lines.Count);
            DocumentLine line = document.Lines[0];
            Assert.Same(line, lines[0]);
            Assert.Same(line, document.GetLineByNumber(1));
            Assert.Same(line, document.GetLineByOffset(0));
        }

        [Fact]
        public void CheckLineSegmentInEmptyDocument()
        {
            DocumentLine line = document.GetLineByNumber(1);
            Assert.Equal(1, line.LineNumber);
            Assert.Equal(0, line.Offset);
            Assert.False(line.IsDeleted);
            Assert.Equal(0, line.Length);
            Assert.Equal(0, line.TotalLength);
            Assert.Equal(0, line.DelimiterLength);
        }

        [Fact]
        public void LineIndexOfTest()
        {
            DocumentLine line = document.GetLineByNumber(1);
            Assert.Equal(0, document.Lines.IndexOf(line));
            DocumentLine lineFromOtherDocument = new TextDocument().GetLineByNumber(1);
            Assert.Equal(-1, document.Lines.IndexOf(lineFromOtherDocument));
            document.Text = "a\nb\nc";
            DocumentLine middleLine = document.GetLineByNumber(2);
            Assert.Equal(1, document.Lines.IndexOf(middleLine));
            document.Remove(1, 3);
            Assert.True(middleLine.IsDeleted);
            Assert.Equal(-1, document.Lines.IndexOf(middleLine));
        }

        [Fact]
        public void InsertInEmptyDocument()
        {
            document.Insert(0, "a");
            Assert.Equal(document.LineCount, 1);
            DocumentLine line = document.GetLineByNumber(1);
            Assert.Equal("a", document.GetText(line));
        }

        [Fact]
        public void SetText()
        {
            document.Text = "a";
            Assert.Equal(document.LineCount, 1);
            DocumentLine line = document.GetLineByNumber(1);
            Assert.Equal("a", document.GetText(line));
        }

        [Fact]
        public void InsertNothing()
        {
            document.Insert(0, "");
            Assert.Equal(document.LineCount, 1);
            Assert.Equal(document.TextLength, 0);
        }

        [Fact]
        public void InsertNull()
        {
            Assert.Throws<ArgumentNullException>(() => document.Insert(0, (string)null));
        }

        [Fact]
        public void SetTextNull()
        {
            Assert.Throws<ArgumentNullException>(() => document.Text = null);
        }

        [Fact]
        public void RemoveNothing()
        {
            document.Remove(0, 0);
            Assert.Equal(document.LineCount, 1);
            Assert.Equal(document.TextLength, 0);
        }

        [Fact]
        public void GetCharAt0EmptyDocument()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => document.GetCharAt(0));
        }

        [Fact]
        public void GetCharAtNegativeOffset()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
            {
                document.Text = "a\nb";
                document.GetCharAt(-1);
            });
        }

        [Fact]
        public void GetCharAtEndOffset()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
            {
                document.Text = "a\nb";
                document.GetCharAt(document.TextLength);
            });
        }

        [Fact]
        public void InsertAtNegativeOffset()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
            {
                document.Text = "a\nb";
                document.Insert(-1, "text");
            });
        }

        [Fact]
        public void InsertAfterEndOffset()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
            {
                document.Text = "a\nb";
                document.Insert(4, "text");
            });
        }

        [Fact]
        public void RemoveNegativeAmount()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
            {
                document.Text = "abcd";
                document.Remove(2, -1);
            });
        }

        [Fact]
        public void RemoveTooMuch()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
            {
                document.Text = "abcd";
                document.Remove(2, 10);
            });
        }

        [Fact]
        public void GetLineByNumberNegative()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
            {
                document.Text = "a\nb";
                document.GetLineByNumber(-1);
            });
        }

        [Fact]
        public void GetLineByNumberTooHigh()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
            {
                document.Text = "a\nb";
                document.GetLineByNumber(3);
            });
        }

        [Fact]
        public void GetLineByOffsetNegative()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
            {
                document.Text = "a\nb";
                document.GetLineByOffset(-1);
            });
        }


        [Fact]
        public void GetLineByOffsetToHigh()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
            {
                document.Text = "a\nb";
                document.GetLineByOffset(10);
            });
        }

        [Fact]
        public void InsertAtEndOffset()
        {
            document.Text = "a\nb";
            CheckDocumentLines("a",
                               "b");
            document.Insert(3, "text");
            CheckDocumentLines("a",
                               "btext");
        }

        [Fact]
        public void GetCharAt()
        {
            document.Text = "a\r\nb";
            Assert.Equal('a', document.GetCharAt(0));
            Assert.Equal('\r', document.GetCharAt(1));
            Assert.Equal('\n', document.GetCharAt(2));
            Assert.Equal('b', document.GetCharAt(3));
        }

        [Fact]
        public void CheckMixedNewLineTest()
        {
            const string mixedNewlineText = "line 1\nline 2\r\nline 3\rline 4";
            document.Text = mixedNewlineText;
            Assert.Equal(mixedNewlineText, document.Text);
            Assert.Equal(4, document.LineCount);
            for (int i = 1; i < 4; i++)
            {
                DocumentLine line = document.GetLineByNumber(i);
                Assert.Equal(i, line.LineNumber);
                Assert.Equal("line " + i, document.GetText(line));
            }
            Assert.Equal(1, document.GetLineByNumber(1).DelimiterLength);
            Assert.Equal(2, document.GetLineByNumber(2).DelimiterLength);
            Assert.Equal(1, document.GetLineByNumber(3).DelimiterLength);
            Assert.Equal(0, document.GetLineByNumber(4).DelimiterLength);
        }

        [Fact]
        public void LfCrIsTwoNewLinesTest()
        {
            document.Text = "a\n\rb";
            Assert.Equal("a\n\rb", document.Text);
            CheckDocumentLines("a",
                               "",
                               "b");
        }

        [Fact]
        public void RemoveFirstPartOfDelimiter()
        {
            document.Text = "a\r\nb";
            document.Remove(1, 1);
            Assert.Equal("a\nb", document.Text);
            CheckDocumentLines("a",
                               "b");
        }

        [Fact]
        public void RemoveLineContentAndJoinDelimiters()
        {
            document.Text = "a\rb\nc";
            document.Remove(2, 1);
            Assert.Equal("a\r\nc", document.Text);
            CheckDocumentLines("a",
                               "c");
        }

        [Fact]
        public void RemoveLineContentAndJoinDelimiters2()
        {
            document.Text = "a\rb\nc\nd";
            document.Remove(2, 3);
            Assert.Equal("a\r\nd", document.Text);
            CheckDocumentLines("a",
                               "d");
        }

        [Fact]
        public void RemoveLineContentAndJoinDelimiters3()
        {
            document.Text = "a\rb\r\nc";
            document.Remove(2, 2);
            Assert.Equal("a\r\nc", document.Text);
            CheckDocumentLines("a",
                               "c");
        }

        [Fact]
        public void RemoveLineContentAndJoinNonMatchingDelimiters()
        {
            document.Text = "a\nb\nc";
            document.Remove(2, 1);
            Assert.Equal("a\n\nc", document.Text);
            CheckDocumentLines("a",
                               "",
                               "c");
        }

        [Fact]
        public void RemoveLineContentAndJoinNonMatchingDelimiters2()
        {
            document.Text = "a\nb\rc";
            document.Remove(2, 1);
            Assert.Equal("a\n\rc", document.Text);
            CheckDocumentLines("a",
                               "",
                               "c");
        }

        [Fact]
        public void RemoveMultilineUpToFirstPartOfDelimiter()
        {
            document.Text = "0\n1\r\n2";
            document.Remove(1, 3);
            Assert.Equal("0\n2", document.Text);
            CheckDocumentLines("0",
                               "2");
        }

        [Fact]
        public void RemoveSecondPartOfDelimiter()
        {
            document.Text = "a\r\nb";
            document.Remove(2, 1);
            Assert.Equal("a\rb", document.Text);
            CheckDocumentLines("a",
                               "b");
        }

        [Fact]
        public void RemoveFromSecondPartOfDelimiter()
        {
            document.Text = "a\r\nb\nc";
            document.Remove(2, 3);
            Assert.Equal("a\rc", document.Text);
            CheckDocumentLines("a",
                               "c");
        }

        [Fact]
        public void RemoveFromSecondPartOfDelimiterToDocumentEnd()
        {
            document.Text = "a\r\nb";
            document.Remove(2, 2);
            Assert.Equal("a\r", document.Text);
            CheckDocumentLines("a",
                               "");
        }

        [Fact]
        public void RemoveUpToMatchingDelimiter1()
        {
            document.Text = "a\r\nb\nc";
            document.Remove(2, 2);
            Assert.Equal("a\r\nc", document.Text);
            CheckDocumentLines("a",
                               "c");
        }

        [Fact]
        public void RemoveUpToMatchingDelimiter2()
        {
            document.Text = "a\r\nb\r\nc";
            document.Remove(2, 3);
            Assert.Equal("a\r\nc", document.Text);
            CheckDocumentLines("a",
                               "c");
        }

        [Fact]
        public void RemoveUpToNonMatchingDelimiter()
        {
            document.Text = "a\r\nb\rc";
            document.Remove(2, 2);
            Assert.Equal("a\r\rc", document.Text);
            CheckDocumentLines("a",
                               "",
                               "c");
        }

        [Fact]
        public void RemoveTwoCharDelimiter()
        {
            document.Text = "a\r\nb";
            document.Remove(1, 2);
            Assert.Equal("ab", document.Text);
            CheckDocumentLines("ab");
        }

        [Fact]
        public void RemoveOneCharDelimiter()
        {
            document.Text = "a\nb";
            document.Remove(1, 1);
            Assert.Equal("ab", document.Text);
            CheckDocumentLines("ab");
        }

        void CheckDocumentLines(params string[] lines)
        {
            Assert.Equal(lines.Length, document.LineCount);
            for (int i = 0; i < lines.Length; i++)
            {
                Assert.Equal(lines[i], document.GetText(document.Lines[i]));
            }
        }

        [Fact]
        public void FixUpFirstPartOfDelimiter()
        {
            document.Text = "a\n\nb";
            document.Replace(1, 1, "\r");
            Assert.Equal("a\r\nb", document.Text);
            CheckDocumentLines("a",
                               "b");
        }

        [Fact]
        public void FixUpSecondPartOfDelimiter()
        {
            document.Text = "a\r\rb";
            document.Replace(2, 1, "\n");
            Assert.Equal("a\r\nb", document.Text);
            CheckDocumentLines("a",
                               "b");
        }

        [Fact]
        public void InsertInsideDelimiter()
        {
            document.Text = "a\r\nc";
            document.Insert(2, "b");
            Assert.Equal("a\rb\nc", document.Text);
            CheckDocumentLines("a",
                               "b",
                               "c");
        }

        [Fact]
        public void InsertInsideDelimiter2()
        {
            document.Text = "a\r\nd";
            document.Insert(2, "b\nc");
            Assert.Equal("a\rb\nc\nd", document.Text);
            CheckDocumentLines("a",
                               "b",
                               "c",
                               "d");
        }

        [Fact]
        public void InsertInsideDelimiter3()
        {
            document.Text = "a\r\nc";
            document.Insert(2, "b\r");
            Assert.Equal("a\rb\r\nc", document.Text);
            CheckDocumentLines("a",
                               "b",
                               "c");
        }

        [Fact]
        public void ExtendDelimiter1()
        {
            document.Text = "a\nc";
            document.Insert(1, "b\r");
            Assert.Equal("ab\r\nc", document.Text);
            CheckDocumentLines("ab",
                               "c");
        }

        [Fact]
        public void ExtendDelimiter2()
        {
            document.Text = "a\rc";
            document.Insert(2, "\nb");
            Assert.Equal("a\r\nbc", document.Text);
            CheckDocumentLines("a",
                               "bc");
        }

        [Fact]
        public void ReplaceLineContentBetweenMatchingDelimiters()
        {
            document.Text = "a\rb\nc";
            document.Replace(2, 1, "x");
            Assert.Equal("a\rx\nc", document.Text);
            CheckDocumentLines("a",
                               "x",
                               "c");
        }

        [Fact]
        public void GetOffset()
        {
            document.Text = "Hello,\nWorld!";
            Assert.Equal(0, document.GetOffset(1, 1));
            Assert.Equal(1, document.GetOffset(1, 2));
            Assert.Equal(5, document.GetOffset(1, 6));
            Assert.Equal(6, document.GetOffset(1, 7));
            Assert.Equal(7, document.GetOffset(2, 1));
            Assert.Equal(8, document.GetOffset(2, 2));
            Assert.Equal(12, document.GetOffset(2, 6));
            Assert.Equal(13, document.GetOffset(2, 7));
        }

        [Fact]
        public void GetOffsetIgnoreNegativeColumns()
        {
            document.Text = "Hello,\nWorld!";
            Assert.Equal(0, document.GetOffset(1, -1));
            Assert.Equal(0, document.GetOffset(1, -100));
            Assert.Equal(0, document.GetOffset(1, 0));
            Assert.Equal(7, document.GetOffset(2, -1));
            Assert.Equal(7, document.GetOffset(2, -100));
            Assert.Equal(7, document.GetOffset(2, 0));
        }

        [Fact]
        public void GetOffsetIgnoreTooHighColumns()
        {
            document.Text = "Hello,\nWorld!";
            Assert.Equal(6, document.GetOffset(1, 8));
            Assert.Equal(6, document.GetOffset(1, 100));
            Assert.Equal(13, document.GetOffset(2, 8));
            Assert.Equal(13, document.GetOffset(2, 100));
        }
    }
}

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

using Xunit;


using Terminal.Gui.Text.Document;
using Terminal.Gui.Text.Utils;
namespace Terminal.Gui.Text.Tests.Document
{
	
	public class TextUtilitiesTests
	{
		#region GetWhitespaceAfter
		[Fact]
		public void TestGetWhitespaceAfter()
		{
			Assert.Equal(new SimpleSegment(2, 3), TextUtilities.GetWhitespaceAfter(new StringTextSource("a \t \tb"), 2));
		}
		
		[Fact]
		public void TestGetWhitespaceAfterDoesNotSkipNewLine()
		{
			Assert.Equal(new SimpleSegment(2, 3), TextUtilities.GetWhitespaceAfter(new StringTextSource("a \t \tb"), 2));
		}
		
		[Fact]
		public void TestGetWhitespaceAfterEmptyResult()
		{
			Assert.Equal(new SimpleSegment(2, 0), TextUtilities.GetWhitespaceAfter(new StringTextSource("a b"), 2));
		}
		
		[Fact]
		public void TestGetWhitespaceAfterEndOfString()
		{
			Assert.Equal(new SimpleSegment(2, 0), TextUtilities.GetWhitespaceAfter(new StringTextSource("a "), 2));
		}
		
		[Fact]
		public void TestGetWhitespaceAfterUntilEndOfString()
		{
			Assert.Equal(new SimpleSegment(2, 3), TextUtilities.GetWhitespaceAfter(new StringTextSource("a \t \t"), 2));
		}
		#endregion
		
		#region GetWhitespaceBefore
		[Fact]
		public void TestGetWhitespaceBefore()
		{
			Assert.Equal(new SimpleSegment(1, 3), TextUtilities.GetWhitespaceBefore(new StringTextSource("a\t \t b"), 4));
		}
		
		[Fact]
		public void TestGetWhitespaceBeforeDoesNotSkipNewLine()
		{
			Assert.Equal(new SimpleSegment(2, 1), TextUtilities.GetWhitespaceBefore(new StringTextSource("a\n b"), 3));
		}
		
		[Fact]
		public void TestGetWhitespaceBeforeEmptyResult()
		{
			Assert.Equal(new SimpleSegment(2, 0), TextUtilities.GetWhitespaceBefore(new StringTextSource(" a b"), 2));
		}
		
		[Fact]
		public void TestGetWhitespaceBeforeStartOfString()
		{
			Assert.Equal(new SimpleSegment(0, 0), TextUtilities.GetWhitespaceBefore(new StringTextSource(" a"), 0));
		}
		
		[Fact]
		public void TestGetWhitespaceBeforeUntilStartOfString()
		{
			Assert.Equal(new SimpleSegment(0, 2), TextUtilities.GetWhitespaceBefore(new StringTextSource(" \t a"), 2));
		}
		#endregion
	}
}

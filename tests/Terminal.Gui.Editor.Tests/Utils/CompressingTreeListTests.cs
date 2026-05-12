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
using System.Linq;
using Xunit;


using Terminal.Gui.Editor.Document;
using Terminal.Gui.Editor.Utils;
namespace Terminal.Gui.Editor.Tests.Utils
{
    
    public class CompressingTreeListTests
    {
        [Fact]
        public void EmptyTreeList()
        {
            CompressingTreeList<string> list = new CompressingTreeList<string>(string.Equals);
            Assert.Equal(0, list.Count);
            foreach (string v in list)
            {
                Assert.Fail();
            }
            string[] arr = Array.Empty<string>();
            list.CopyTo(arr, 0);
        }

        [Fact]
        public void CheckAdd10BillionElements()
        {
            const int billion = 1000000000;
            CompressingTreeList<string> list = new CompressingTreeList<string>(string.Equals);
            list.InsertRange(0, billion, "A");
            list.InsertRange(1, billion, "B");
            Assert.Equal(2 * billion, list.Count);
            Assert.Throws<OverflowException>(delegate
            { list.InsertRange(2, billion, "C"); });
        }

        [Fact]
        public void AddRepeated()
        {
            CompressingTreeList<int> list = new CompressingTreeList<int>((a, b) => a == b);
            list.Add(42);
            list.Add(42);
            list.Add(42);
            list.Insert(0, 42);
            list.Insert(1, 42);
            Assert.Equal(new[] { 42, 42, 42, 42, 42 }, list.ToArray());
        }

        [Fact]
        public void RemoveRange()
        {
            CompressingTreeList<int> list = new CompressingTreeList<int>((a, b) => a == b);
            for (int i = 1; i <= 3; i++)
            {
                list.InsertRange(list.Count, 2, i);
            }
            Assert.Equal(new[] { 1, 1, 2, 2, 3, 3 }, list.ToArray());
            list.RemoveRange(1, 4);
            Assert.Equal(new[] { 1, 3 }, list.ToArray());
            list.Insert(1, 1);
            list.InsertRange(2, 2, 2);
            list.Insert(4, 1);
            Assert.Equal(new[] { 1, 1, 2, 2, 1, 3 }, list.ToArray());
            list.RemoveRange(2, 2);
            Assert.Equal(new[] { 1, 1, 1, 3 }, list.ToArray());
        }

        [Fact]
        public void RemoveAtEnd()
        {
            CompressingTreeList<int> list = new CompressingTreeList<int>((a, b) => a == b);
            for (int i = 1; i <= 3; i++)
            {
                list.InsertRange(list.Count, 2, i);
            }
            Assert.Equal(new[] { 1, 1, 2, 2, 3, 3 }, list.ToArray());
            list.RemoveRange(3, 3);
            Assert.Equal(new[] { 1, 1, 2 }, list.ToArray());
        }

        [Fact]
        public void RemoveAtStart()
        {
            CompressingTreeList<int> list = new CompressingTreeList<int>((a, b) => a == b);
            for (int i = 1; i <= 3; i++)
            {
                list.InsertRange(list.Count, 2, i);
            }
            Assert.Equal(new[] { 1, 1, 2, 2, 3, 3 }, list.ToArray());
            list.RemoveRange(0, 1);
            Assert.Equal(new[] { 1, 2, 2, 3, 3 }, list.ToArray());
        }

        [Fact]
        public void RemoveAtStart2()
        {
            CompressingTreeList<int> list = new CompressingTreeList<int>((a, b) => a == b);
            for (int i = 1; i <= 3; i++)
            {
                list.InsertRange(list.Count, 2, i);
            }
            Assert.Equal(new[] { 1, 1, 2, 2, 3, 3 }, list.ToArray());
            list.RemoveRange(0, 3);
            Assert.Equal(new[] { 2, 3, 3 }, list.ToArray());
        }

        [Fact]
        public void Transform()
        {
            CompressingTreeList<int> list = new CompressingTreeList<int>((a, b) => a == b);
            foreach (int x in new[] { 0, 1, 1, 0 }) list.Add(x);
            int calls = 0;
            list.Transform(i => { calls++; return i + 1; });
            Assert.Equal(3, calls);
            Assert.Equal(new[] { 1, 2, 2, 1 }, list.ToArray());
        }

        [Fact]
        public void TransformToZero()
        {
            CompressingTreeList<int> list = new CompressingTreeList<int>((a, b) => a == b);
            foreach (int x in new[] { 0, 1, 1, 0 }) list.Add(x);
            list.Transform(i => 0);
            Assert.Equal(new[] { 0, 0, 0, 0 }, list.ToArray());
        }

        [Fact]
        public void TransformRange()
        {
            CompressingTreeList<int> list = new CompressingTreeList<int>((a, b) => a == b);
            foreach (int x in new[] { 0, 1, 1, 1, 0, 0 }) list.Add(x);
            list.TransformRange(2, 3, i => 0);
            Assert.Equal(new[] { 0, 1, 0, 0, 0, 0 }, list.ToArray());
        }
    }
}

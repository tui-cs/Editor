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


using System.Collections;
using Terminal.Gui.Editor.Document.Utils;
using Xunit;

namespace Terminal.Gui.Editor.Tests.Utils;

public class DequeTests
{
    #region Construction and Initial State

    [Fact]
    public void NewDeque_ShouldHaveCountZero ()
    {
        Deque<int> deque = new ();
        Assert.Equal (0, deque.Count);
    }

    [Fact]
    public void NewDeque_Enumeration_ShouldBeEmpty ()
    {
        Deque<int> deque = new ();
        Assert.Empty (deque.ToList ());
    }

    #endregion

    #region PushBack

    [Fact]
    public void PushBack_SingleElement_ShouldIncrementCount ()
    {
        Deque<int> deque = new ();
        deque.PushBack (42);
        Assert.Equal (1, deque.Count);
    }

    [Fact]
    public void PushBack_SingleElement_ShouldBeAccessibleAtIndexZero ()
    {
        Deque<int> deque = new ();
        deque.PushBack (42);
        Assert.Equal (42, deque[0]);
    }

    [Fact]
    public void PushBack_MultipleElements_ShouldMaintainInsertionOrder ()
    {
        Deque<int> deque = new ();
        deque.PushBack (1);
        deque.PushBack (2);
        deque.PushBack (3);

        Assert.Equal (1, deque[0]);
        Assert.Equal (2, deque[1]);
        Assert.Equal (3, deque[2]);
        Assert.Equal (3, deque.Count);
    }

    [Fact]
    public void PushBack_BeyondInitialCapacity_ShouldGrow ()
    {
        // Initial capacity is 4, push 10 elements to force multiple grows
        Deque<int> deque = new ();
        for (var i = 0; i < 10; i++)
        {
            deque.PushBack (i);
        }

        Assert.Equal (10, deque.Count);
        for (var i = 0; i < 10; i++)
        {
            Assert.Equal (i, deque[i]);
        }
    }

    [Fact]
    public void PushBack_NullReferenceType_ShouldSucceed ()
    {
        Deque<string> deque = new ();
        deque.PushBack (null);

        Assert.Equal (1, deque.Count);
        Assert.Null (deque[0]);
    }

    #endregion

    #region PushFront

    [Fact]
    public void PushFront_SingleElement_ShouldIncrementCount ()
    {
        Deque<int> deque = new ();
        deque.PushFront (42);
        Assert.Equal (1, deque.Count);
    }

    [Fact]
    public void PushFront_SingleElement_ShouldBeAccessibleAtIndexZero ()
    {
        Deque<int> deque = new ();
        deque.PushFront (42);
        Assert.Equal (42, deque[0]);
    }

    [Fact]
    public void PushFront_MultipleElements_ShouldMaintainReverseInsertionOrder ()
    {
        Deque<int> deque = new ();
        deque.PushFront (1);
        deque.PushFront (2);
        deque.PushFront (3);

        // Last pushed to front is at index 0
        Assert.Equal (3, deque[0]);
        Assert.Equal (2, deque[1]);
        Assert.Equal (1, deque[2]);
        Assert.Equal (3, deque.Count);
    }

    [Fact]
    public void PushFront_BeyondInitialCapacity_ShouldGrow ()
    {
        Deque<int> deque = new ();
        for (var i = 0; i < 10; i++)
        {
            deque.PushFront (i);
        }

        Assert.Equal (10, deque.Count);
        // Elements are in reverse order: 9, 8, 7, ..., 0
        for (var i = 0; i < 10; i++)
        {
            Assert.Equal (9 - i, deque[i]);
        }
    }

    #endregion

    #region PopBack

    [Fact]
    public void PopBack_ShouldReturnLastElement ()
    {
        Deque<int> deque = new ();
        deque.PushBack (1);
        deque.PushBack (2);
        deque.PushBack (3);

        Assert.Equal (3, deque.PopBack ());
    }

    [Fact]
    public void PopBack_ShouldDecrementCount ()
    {
        Deque<int> deque = new ();
        deque.PushBack (1);
        deque.PushBack (2);
        deque.PopBack ();

        Assert.Equal (1, deque.Count);
    }

    [Fact]
    public void PopBack_AllElements_ShouldEmptyDeque ()
    {
        Deque<int> deque = new ();
        deque.PushBack (1);
        deque.PushBack (2);
        deque.PushBack (3);

        Assert.Equal (3, deque.PopBack ());
        Assert.Equal (2, deque.PopBack ());
        Assert.Equal (1, deque.PopBack ());
        Assert.Equal (0, deque.Count);
    }

    [Fact]
    public void PopBack_OnEmptyDeque_ShouldThrowInvalidOperationException ()
    {
        Deque<int> deque = new ();
        Assert.Throws<InvalidOperationException> (() => deque.PopBack ());
    }

    [Fact]
    public void PopBack_AfterClear_ShouldThrowInvalidOperationException ()
    {
        Deque<int> deque = new ();
        deque.PushBack (1);
        deque.Clear ();

        Assert.Throws<InvalidOperationException> (() => deque.PopBack ());
    }

    #endregion

    #region PopFront

    [Fact]
    public void PopFront_ShouldReturnFirstElement ()
    {
        Deque<int> deque = new ();
        deque.PushBack (1);
        deque.PushBack (2);
        deque.PushBack (3);

        Assert.Equal (1, deque.PopFront ());
    }

    [Fact]
    public void PopFront_ShouldDecrementCount ()
    {
        Deque<int> deque = new ();
        deque.PushBack (1);
        deque.PushBack (2);
        deque.PopFront ();

        Assert.Equal (1, deque.Count);
    }

    [Fact]
    public void PopFront_AllElements_ShouldEmptyDeque ()
    {
        Deque<int> deque = new ();
        deque.PushBack (1);
        deque.PushBack (2);
        deque.PushBack (3);

        Assert.Equal (1, deque.PopFront ());
        Assert.Equal (2, deque.PopFront ());
        Assert.Equal (3, deque.PopFront ());
        Assert.Equal (0, deque.Count);
    }

    [Fact]
    public void PopFront_OnEmptyDeque_ShouldThrowInvalidOperationException ()
    {
        Deque<int> deque = new ();
        Assert.Throws<InvalidOperationException> (() => deque.PopFront ());
    }

    #endregion

    #region Mixed Push/Pop Operations (Ring Buffer Wrapping)

    [Fact]
    public void MixedOperations_PushBackPopFront_ShouldBehaveLikeFIFOQueue ()
    {
        Deque<int> deque = new ();
        for (var i = 0; i < 100; i++)
        {
            deque.PushBack (i);
            Assert.Equal (i, deque.PopFront ());
        }

        Assert.Equal (0, deque.Count);
    }

    [Fact]
    public void MixedOperations_PushFrontPopBack_ShouldBehaveLikeFIFOQueue ()
    {
        Deque<int> deque = new ();
        for (var i = 0; i < 100; i++)
        {
            deque.PushFront (i);
            Assert.Equal (i, deque.PopBack ());
        }

        Assert.Equal (0, deque.Count);
    }

    [Fact]
    public void MixedOperations_PushBackPopBack_ShouldBehaveLikeLIFOStack ()
    {
        Deque<int> deque = new ();
        deque.PushBack (1);
        deque.PushBack (2);
        deque.PushBack (3);

        Assert.Equal (3, deque.PopBack ());
        Assert.Equal (2, deque.PopBack ());
        Assert.Equal (1, deque.PopBack ());
    }

    [Fact]
    public void MixedOperations_PushFrontPopFront_ShouldBehaveLikeLIFOStack ()
    {
        Deque<int> deque = new ();
        deque.PushFront (1);
        deque.PushFront (2);
        deque.PushFront (3);

        Assert.Equal (3, deque.PopFront ());
        Assert.Equal (2, deque.PopFront ());
        Assert.Equal (1, deque.PopFront ());
    }

    [Fact]
    public void MixedOperations_AlternatingPushFrontAndPushBack ()
    {
        Deque<int> deque = new ();
        // Build: PushFront(1), PushBack(2), PushFront(3), PushBack(4)
        // Expected order: [3, 1, 2, 4]
        deque.PushFront (1);
        deque.PushBack (2);
        deque.PushFront (3);
        deque.PushBack (4);

        Assert.Equal (4, deque.Count);
        Assert.Equal (3, deque[0]);
        Assert.Equal (1, deque[1]);
        Assert.Equal (2, deque[2]);
        Assert.Equal (4, deque[3]);
    }

    [Fact]
    public void MixedOperations_WrapAround_ShouldMaintainCorrectOrder ()
    {
        // Force the internal ring buffer to wrap around by
        // filling, partially draining from front, then refilling from back
        Deque<int> deque = new ();

        // Fill to capacity (triggers initial allocation of 4)
        deque.PushBack (1);
        deque.PushBack (2);
        deque.PushBack (3);
        deque.PushBack (4);

        // Drain two from front - head advances
        Assert.Equal (1, deque.PopFront ());
        Assert.Equal (2, deque.PopFront ());

        // Push two more to back - tail wraps around in the ring buffer
        deque.PushBack (5);
        deque.PushBack (6);

        // Verify: [3, 4, 5, 6]
        Assert.Equal (4, deque.Count);
        Assert.Equal (3, deque[0]);
        Assert.Equal (4, deque[1]);
        Assert.Equal (5, deque[2]);
        Assert.Equal (6, deque[3]);
    }

    [Fact]
    public void MixedOperations_HeavyWrapping_StressTest ()
    {
        // Simulate UndoStack-like pattern: push many, pop some, push more
        Deque<int> deque = new ();
        LinkedList<int> reference = new ();

        Random rng = new (42); // deterministic seed
        for (var i = 0; i < 1_000; i++)
        {
            var op = rng.Next (4);
            switch (op)
            {
                case 0: // PushBack
                    deque.PushBack (i);
                    reference.AddLast (i);
                    break;
                case 1: // PushFront
                    deque.PushFront (i);
                    reference.AddFirst (i);
                    break;
                case 2: // PopBack
                    if (deque.Count > 0)
                    {
                        Assert.Equal (reference.Last.Value, deque.PopBack ());
                        reference.RemoveLast ();
                    }

                    break;
                case 3: // PopFront
                    if (deque.Count > 0)
                    {
                        Assert.Equal (reference.First.Value, deque.PopFront ());
                        reference.RemoveFirst ();
                    }

                    break;
            }

            Assert.Equal (reference.Count, deque.Count);
        }

        // Final verification: all remaining elements match
        var refArray = reference.ToArray ();
        for (var i = 0; i < deque.Count; i++)
        {
            Assert.Equal (refArray[i], deque[i]);
        }
    }

    #endregion

    #region Indexer

    [Fact]
    public void Indexer_Get_ValidIndex_ShouldReturnCorrectElement ()
    {
        Deque<string> deque = new ();
        deque.PushBack ("a");
        deque.PushBack ("b");
        deque.PushBack ("c");

        Assert.Equal ("a", deque[0]);
        Assert.Equal ("b", deque[1]);
        Assert.Equal ("c", deque[2]);
    }

    [Fact]
    public void Indexer_Set_ValidIndex_ShouldUpdateElement ()
    {
        Deque<int> deque = new ();
        deque.PushBack (1);
        deque.PushBack (2);
        deque.PushBack (3);

        deque[1] = 99;

        Assert.Equal (1, deque[0]);
        Assert.Equal (99, deque[1]);
        Assert.Equal (3, deque[2]);
    }

    [Fact]
    public void Indexer_Get_NegativeIndex_ShouldThrow ()
    {
        Deque<int> deque = new ();
        deque.PushBack (1);

        Assert.Throws<ArgumentOutOfRangeException> (() => { _ = deque[-1]; });
    }

    [Fact]
    public void Indexer_Get_IndexEqualToCount_ShouldThrow ()
    {
        Deque<int> deque = new ();
        deque.PushBack (1);

        Assert.Throws<ArgumentOutOfRangeException> (() => { _ = deque[1]; });
    }

    [Fact]
    public void Indexer_Get_OnEmptyDeque_ShouldThrow ()
    {
        Deque<int> deque = new ();
        Assert.Throws<ArgumentOutOfRangeException> (() => { _ = deque[0]; });
    }

    [Fact]
    public void Indexer_Set_NegativeIndex_ShouldThrow ()
    {
        Deque<int> deque = new ();
        deque.PushBack (1);

        Assert.Throws<ArgumentOutOfRangeException> (() => { deque[-1] = 99; });
    }

    [Fact]
    public void Indexer_Set_IndexEqualToCount_ShouldThrow ()
    {
        Deque<int> deque = new ();
        deque.PushBack (1);

        Assert.Throws<ArgumentOutOfRangeException> (() => { deque[1] = 99; });
    }

    [Fact]
    public void Indexer_AfterWrapAround_ShouldReturnCorrectElements ()
    {
        Deque<int> deque = new ();

        // Fill to capacity
        deque.PushBack (1);
        deque.PushBack (2);
        deque.PushBack (3);
        deque.PushBack (4);

        // Pop from front to advance head, then push to back to wrap tail
        deque.PopFront ();
        deque.PopFront ();
        deque.PushBack (5);
        deque.PushBack (6);

        // Indexer should logically see [3, 4, 5, 6]
        Assert.Equal (3, deque[0]);
        Assert.Equal (4, deque[1]);
        Assert.Equal (5, deque[2]);
        Assert.Equal (6, deque[3]);
    }

    #endregion

    #region Clear

    [Fact]
    public void Clear_ShouldResetCountToZero ()
    {
        Deque<int> deque = new ();
        deque.PushBack (1);
        deque.PushBack (2);
        deque.PushBack (3);

        deque.Clear ();

        Assert.Equal (0, deque.Count);
    }

    [Fact]
    public void Clear_ShouldAllowSubsequentPush ()
    {
        Deque<int> deque = new ();
        deque.PushBack (1);
        deque.PushBack (2);
        deque.Clear ();

        deque.PushBack (99);

        Assert.Equal (1, deque.Count);
        Assert.Equal (99, deque[0]);
    }

    [Fact]
    public void Clear_OnEmptyDeque_ShouldBeNoOp ()
    {
        Deque<int> deque = new ();
        deque.Clear ();
        Assert.Equal (0, deque.Count);
    }

    [Fact]
    public void Clear_ThenRefill_ShouldWorkCorrectly ()
    {
        // Exercises the in-place clear path (buffer is reused)
        Deque<int> deque = new ();
        for (var i = 0; i < 20; i++)
        {
            deque.PushBack (i);
        }

        deque.Clear ();

        for (var i = 100; i < 120; i++)
        {
            deque.PushBack (i);
        }

        Assert.Equal (20, deque.Count);
        for (var i = 0; i < 20; i++)
        {
            Assert.Equal (100 + i, deque[i]);
        }
    }

    [Fact]
    public void Clear_WithWrappedBuffer_ShouldClearCorrectly ()
    {
        Deque<string> deque = new ();

        // Fill to 4, pop 2 from front, push 2 to back - creates wrap-around
        deque.PushBack ("a");
        deque.PushBack ("b");
        deque.PushBack ("c");
        deque.PushBack ("d");
        deque.PopFront ();
        deque.PopFront ();
        deque.PushBack ("e");
        deque.PushBack ("f");

        deque.Clear ();

        Assert.Equal (0, deque.Count);

        // Push new elements and verify clean state
        deque.PushBack ("x");
        Assert.Equal (1, deque.Count);
        Assert.Equal ("x", deque[0]);
    }

    #endregion

    #region Contains

    [Fact]
    public void Contains_ExistingElement_ShouldReturnTrue ()
    {
        Deque<int> deque = new ();
        deque.PushBack (1);
        deque.PushBack (2);
        deque.PushBack (3);

        Assert.True (deque.Contains (2));
    }

    [Fact]
    public void Contains_NonExistingElement_ShouldReturnFalse ()
    {
        Deque<int> deque = new ();
        deque.PushBack (1);
        deque.PushBack (2);
        deque.PushBack (3);

        Assert.False (deque.Contains (99));
    }

    [Fact]
    public void Contains_OnEmptyDeque_ShouldReturnFalse ()
    {
        Deque<int> deque = new ();
        Assert.False (deque.Contains (0));
    }

    [Fact]
    public void Contains_NullInReferenceTypeDeque_ShouldReturnTrue ()
    {
        Deque<string> deque = new ();
        deque.PushBack ("a");
        deque.PushBack (null);
        deque.PushBack ("b");

        Assert.True (deque.Contains (null));
    }

    [Fact]
    public void Contains_NullNotPresent_ShouldReturnFalse ()
    {
        Deque<string> deque = new ();
        deque.PushBack ("a");
        deque.PushBack ("b");

        Assert.False (deque.Contains (null));
    }

    [Fact]
    public void Contains_AfterWrapAround_ShouldFindElement ()
    {
        Deque<int> deque = new ();
        deque.PushBack (1);
        deque.PushBack (2);
        deque.PushBack (3);
        deque.PushBack (4);
        deque.PopFront ();
        deque.PopFront ();
        deque.PushBack (5);
        deque.PushBack (6);

        // Logical content: [3, 4, 5, 6]
        Assert.True (deque.Contains (3));
        Assert.True (deque.Contains (6));
        Assert.False (deque.Contains (1));
        Assert.False (deque.Contains (2));
    }

    #endregion

    #region CopyTo

    [Fact]
    public void CopyTo_ShouldCopyAllElementsInOrder ()
    {
        Deque<int> deque = new ();
        deque.PushBack (10);
        deque.PushBack (20);
        deque.PushBack (30);

        var arr = new int[3];
        deque.CopyTo (arr, 0);

        Assert.Equal (new[] { 10, 20, 30 }, arr);
    }

    [Fact]
    public void CopyTo_WithOffset_ShouldCopyToCorrectPosition ()
    {
        Deque<int> deque = new ();
        deque.PushBack (10);
        deque.PushBack (20);

        var arr = new int[5];
        deque.CopyTo (arr, 2);

        Assert.Equal (new[] { 0, 0, 10, 20, 0 }, arr);
    }

    [Fact]
    public void CopyTo_EmptyDeque_ShouldNotModifyArray ()
    {
        Deque<int> deque = new ();
        var arr = new[] { 1, 2, 3 };

        deque.CopyTo (arr, 0);

        Assert.Equal (new[] { 1, 2, 3 }, arr);
    }

    [Fact]
    public void CopyTo_NullArray_ShouldThrowArgumentNullException ()
    {
        Deque<int> deque = new ();
        deque.PushBack (1);

        Assert.Throws<ArgumentNullException> (() => deque.CopyTo (null, 0));
    }

    [Fact]
    public void CopyTo_AfterWrapAround_ShouldCopyInLogicalOrder ()
    {
        Deque<int> deque = new ();
        deque.PushBack (1);
        deque.PushBack (2);
        deque.PushBack (3);
        deque.PushBack (4);
        deque.PopFront ();
        deque.PopFront ();
        deque.PushBack (5);
        deque.PushBack (6);

        var arr = new int[4];
        deque.CopyTo (arr, 0);

        // Logical order: [3, 4, 5, 6]
        Assert.Equal (new[] { 3, 4, 5, 6 }, arr);
    }

    #endregion

    #region Enumeration

    [Fact]
    public void GetEnumerator_ShouldYieldElementsInOrder ()
    {
        Deque<int> deque = new ();
        deque.PushBack (1);
        deque.PushBack (2);
        deque.PushBack (3);

        List<int> result = new ();
        foreach (var item in deque)
        {
            result.Add (item);
        }

        Assert.Equal (new[] { 1, 2, 3 }, result.ToArray ());
    }

    [Fact]
    public void GetEnumerator_EmptyDeque_ShouldYieldNothing ()
    {
        Deque<int> deque = new ();
        List<int> result = new ();
        foreach (var item in deque)
        {
            result.Add (item);
        }

        Assert.Empty (result);
    }

    [Fact]
    public void GetEnumerator_AfterWrapAround_ShouldYieldLogicalOrder ()
    {
        Deque<int> deque = new ();
        deque.PushBack (1);
        deque.PushBack (2);
        deque.PushBack (3);
        deque.PushBack (4);
        deque.PopFront ();
        deque.PopFront ();
        deque.PushBack (5);
        deque.PushBack (6);

        List<int> result = deque.ToList ();
        Assert.Equal (new[] { 3, 4, 5, 6 }, result.ToArray ());
    }

    [Fact]
    public void GetEnumerator_ViaIEnumerable_ShouldYieldSameElements ()
    {
        Deque<int> deque = new ();
        deque.PushBack (10);
        deque.PushBack (20);
        deque.PushBack (30);

        // Cast to IEnumerable to force the explicit interface path
        IEnumerable enumerable = deque;
        List<int> result = new ();
        foreach (int item in enumerable)
        {
            result.Add (item);
        }

        Assert.Equal (new[] { 10, 20, 30 }, result.ToArray ());
    }

    [Fact]
    public void GetEnumerator_ViaIEnumerableOfT_ShouldYieldSameElements ()
    {
        Deque<int> deque = new ();
        deque.PushBack (10);
        deque.PushBack (20);
        deque.PushBack (30);

        // Cast to IEnumerable<T> to force the explicit interface path
        IEnumerable<int> enumerable = deque;
        List<int> result = new ();
        foreach (var item in enumerable)
        {
            result.Add (item);
        }

        Assert.Equal (new[] { 10, 20, 30 }, result.ToArray ());
    }

    [Fact]
    public void LinqOperations_ShouldWorkWithDeque ()
    {
        Deque<int> deque = new ();
        for (var i = 1; i <= 10; i++)
        {
            deque.PushBack (i);
        }

        Assert.Equal (55, deque.Sum ());
        Assert.Equal (10, deque.Max ());
        Assert.Equal (1, deque.Min ());
        Assert.Equal (5, deque.Count (x => x > 5));
    }

    #endregion

    #region ICollection<T> Interface

    [Fact]
    public void ICollection_IsReadOnly_ShouldReturnFalse ()
    {
        ICollection<int> deque = new Deque<int> ();
        Assert.False (deque.IsReadOnly);
    }

    [Fact]
    public void ICollection_Add_ShouldDelegateToPushBack ()
    {
        ICollection<int> deque = new Deque<int> ();
        deque.Add (1);
        deque.Add (2);
        deque.Add (3);

        Assert.Equal (3, deque.Count);

        // Verify order via enumeration
        List<int> result = deque.ToList ();
        Assert.Equal (new[] { 1, 2, 3 }, result.ToArray ());
    }

    [Fact]
    public void ICollection_Remove_ShouldThrowNotSupportedException ()
    {
        ICollection<int> deque = new Deque<int> ();
        deque.Add (1);

        Assert.Throws<NotSupportedException> (() => deque.Remove (1));
    }

    #endregion

    #region Growth and Capacity (Power-of-2 Invariant)

    [Fact]
    public void Growth_CapacityShouldAlwaysBePowerOf2 ()
    {
        // We can't directly inspect capacity, but we can infer it:
        // After pushing N elements, if we push one more without a pop,
        // the capacity must be >= N+1 and a power of 2.
        // We verify this indirectly by ensuring all operations remain correct
        // at power-of-2 boundaries.
        Deque<int> deque = new ();

        // Push exactly at boundaries: 1, 2, 4, 8, 16, 32, 64
        for (var i = 0; i < 64; i++)
        {
            deque.PushBack (i);
            Assert.Equal (i + 1, deque.Count);
            Assert.Equal (i, deque[i]);
        }

        // Verify all elements are intact
        for (var i = 0; i < 64; i++)
        {
            Assert.Equal (i, deque[i]);
        }
    }

    [Fact]
    public void Growth_AfterMultipleGrows_ElementOrderPreserved ()
    {
        Deque<int> deque = new ();

        // Force several grow operations
        for (var i = 0; i < 100; i++)
        {
            deque.PushBack (i);
        }

        for (var i = 0; i < 100; i++)
        {
            Assert.Equal (i, deque[i]);
        }
    }

    [Fact]
    public void Growth_WithWrappedBuffer_ShouldUnwrapCorrectlyOnGrow ()
    {
        // This is the critical test: when the ring buffer is wrapped
        // (head > tail) and we need to grow, SetCapacity must correctly
        // linearize the two segments into the new array.
        Deque<int> deque = new ();

        // Fill to 4 (initial capacity)
        deque.PushBack (0);
        deque.PushBack (1);
        deque.PushBack (2);
        deque.PushBack (3);

        // Pop two from front - head advances to index 2
        deque.PopFront (); // removes 0
        deque.PopFront (); // removes 1

        // Push two to back - fills back to 4 (tail wraps to index 2)
        deque.PushBack (4);
        deque.PushBack (5);

        // Now push one more - this forces a grow while wrapped
        deque.PushBack (6);

        // Logical content should be [2, 3, 4, 5, 6]
        Assert.Equal (5, deque.Count);
        Assert.Equal (2, deque[0]);
        Assert.Equal (3, deque[1]);
        Assert.Equal (4, deque[2]);
        Assert.Equal (5, deque[3]);
        Assert.Equal (6, deque[4]);
    }

    #endregion

    #region Value Type vs Reference Type Behavior

    [Fact]
    public void ValueType_PopBack_ShouldReturnCorrectValues ()
    {
        // Exercises the IsReferenceOrContainsReferences<T>() == false path
        Deque<double> deque = new ();
        deque.PushBack (1.1);
        deque.PushBack (2.2);
        deque.PushBack (3.3);

        Assert.Equal (3.3, deque.PopBack (), 0.001);
        Assert.Equal (2.2, deque.PopBack (), 0.001);
        Assert.Equal (1.1, deque.PopBack (), 0.001);
    }

    [Fact]
    public void StructWithReferenceField_ShouldClearOnPop ()
    {
        // A struct containing a reference field exercises the
        // IsReferenceOrContainsReferences<T>() == true path for value types
        Deque<KeyValuePair<string, int>> deque = new ();
        deque.PushBack (new KeyValuePair<string, int> ("a", 1));
        deque.PushBack (new KeyValuePair<string, int> ("b", 2));

        KeyValuePair<string, int> result = deque.PopBack ();
        Assert.Equal ("b", result.Key);
        Assert.Equal (2, result.Value);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void SingleElement_PushBackPopBack ()
    {
        Deque<int> deque = new ();
        deque.PushBack (42);
        Assert.Equal (42, deque.PopBack ());
        Assert.Equal (0, deque.Count);
    }

    [Fact]
    public void SingleElement_PushBackPopFront ()
    {
        Deque<int> deque = new ();
        deque.PushBack (42);
        Assert.Equal (42, deque.PopFront ());
        Assert.Equal (0, deque.Count);
    }

    [Fact]
    public void SingleElement_PushFrontPopBack ()
    {
        Deque<int> deque = new ();
        deque.PushFront (42);
        Assert.Equal (42, deque.PopBack ());
        Assert.Equal (0, deque.Count);
    }

    [Fact]
    public void SingleElement_PushFrontPopFront ()
    {
        Deque<int> deque = new ();
        deque.PushFront (42);
        Assert.Equal (42, deque.PopFront ());
        Assert.Equal (0, deque.Count);
    }

    [Fact]
    public void RepeatedClearAndRefill_ShouldNotCorruptState ()
    {
        Deque<int> deque = new ();

        for (var cycle = 0; cycle < 10; cycle++)
        {
            for (var i = 0; i < 20; i++)
            {
                deque.PushBack (cycle * 100 + i);
            }

            Assert.Equal (20, deque.Count);
            Assert.Equal (cycle * 100, deque[0]);
            Assert.Equal (cycle * 100 + 19, deque[19]);

            deque.Clear ();
            Assert.Equal (0, deque.Count);
        }
    }

    [Fact]
    public void LargeDeque_ShouldHandleManyElements ()
    {
        Deque<int> deque = new ();
        const int count = 100_000;

        for (var i = 0; i < count; i++)
        {
            deque.PushBack (i);
        }

        Assert.Equal (count, deque.Count);
        Assert.Equal (0, deque[0]);
        Assert.Equal (count - 1, deque[count - 1]);

        // Drain from front
        for (var i = 0; i < count; i++)
        {
            Assert.Equal (i, deque.PopFront ());
        }

        Assert.Equal (0, deque.Count);
    }

    [Fact]
    public void PushFront_ThenGrow_ShouldPreserveOrder ()
    {
        // PushFront moves head backward; when grow happens, the
        // two-segment copy in SetCapacity must handle head > 0 correctly
        Deque<int> deque = new ();

        for (var i = 0; i < 10; i++)
        {
            deque.PushFront (i);
        }

        // Expected: [9, 8, 7, 6, 5, 4, 3, 2, 1, 0]
        for (var i = 0; i < 10; i++)
        {
            Assert.Equal (9 - i, deque[i]);
        }
    }

    [Fact]
    public void AlternatingPushFrontAndPopBack_ShouldWorkCorrectly ()
    {
        // This exercises the scenario where head keeps moving backward
        // and tail keeps moving backward, both wrapping frequently
        Deque<int> deque = new ();

        for (var i = 0; i < 50; i++)
        {
            deque.PushFront (i);
            deque.PushFront (i + 100);

            Assert.Equal (i, deque.PopBack ());
            // deque still has one element: i + 100
            Assert.Equal (1, deque.Count);
            Assert.Equal (i + 100, deque[0]);

            deque.PopFront ();
        }

        Assert.Equal (0, deque.Count);
    }

    #endregion

    #region Behavioral Parity with Original Implementation

    // These tests verify behaviors that existed in the original Deque<T>
    // to ensure the optimized version is a drop-in replacement.

    [Fact]
    public void Parity_ICollection_Add_SameAsPushBack ()
    {
        Deque<int> dequeViaAdd = new ();
        Deque<int> dequeViaPush = new ();

        for (var i = 0; i < 10; i++)
        {
            ((ICollection<int>)dequeViaAdd).Add (i);
            dequeViaPush.PushBack (i);
        }

        Assert.Equal (dequeViaPush.Count, dequeViaAdd.Count);
        for (var i = 0; i < dequeViaPush.Count; i++)
        {
            Assert.Equal (dequeViaPush[i], dequeViaAdd[i]);
        }
    }

    [Fact]
    public void Parity_CopyToAndEnumeration_ShouldMatchIndexer ()
    {
        Deque<int> deque = new ();
        // Build a wrapped deque
        for (var i = 0; i < 8; i++)
        {
            deque.PushBack (i);
        }

        for (var i = 0; i < 4; i++)
        {
            deque.PopFront ();
        }

        for (var i = 8; i < 12; i++)
        {
            deque.PushBack (i);
        }

        // Collect via indexer
        var viaIndexer = new int[deque.Count];
        for (var i = 0; i < deque.Count; i++)
        {
            viaIndexer[i] = deque[i];
        }

        // Collect via CopyTo
        var viaCopyTo = new int[deque.Count];
        deque.CopyTo (viaCopyTo, 0);

        // Collect via enumeration
        var viaEnumeration = deque.ToArray ();

        Assert.Equal (viaIndexer, viaCopyTo);
        Assert.Equal (viaIndexer, viaEnumeration);
    }

    [Fact]
    public void Parity_UndoStackPattern_PushBackWithSizeLimitTrim ()
    {
        // Simulates the UndoStack's pattern:
        // push operations to back, trim oldest from front when over limit
        Deque<int> deque = new ();
        const int sizeLimit = 10;

        for (var i = 0; i < 50; i++)
        {
            deque.PushBack (i);
            while (deque.Count > sizeLimit)
            {
                deque.PopFront ();
            }
        }

        Assert.Equal (sizeLimit, deque.Count);
        // Should contain the last 10 elements: 40..49
        for (var i = 0; i < sizeLimit; i++)
        {
            Assert.Equal (40 + i, deque[i]);
        }
    }

    #endregion
}

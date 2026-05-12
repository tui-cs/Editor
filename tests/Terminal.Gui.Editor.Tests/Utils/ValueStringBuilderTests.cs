// Adapted for Terminal.Gui from AvaloniaEdit d7a6b63
// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

// Source taken from: https://github.com/dotnet/runtime/blob/v10.0.105/src/libraries/Common/tests/Tests/System/Text/ValueStringBuilderTests.cs
// and converted from XUnit to NUnit tests.


using System.Diagnostics.CodeAnalysis;
using System.Text;
using Terminal.Gui.Document.Utils;
using Xunit;

namespace Terminal.Gui.Editor.Tests.Utils;

[SuppressMessage ("Reliability", "S2930: IDisposables should be disposed", Justification = "Just unit tests")]
public class ValueStringBuilderTests
{
    [Fact]
    public void Ctor_Default_CanAppend ()
    {
        // Arrange
        ValueStringBuilder vsb = default;

        // Act
        var initialLength = vsb.Length;
        vsb.Append ('a');
        var finalLength = vsb.Length;
        var result = vsb.ToString ();

        // Assert
        Assert.Equal (0, initialLength);
        Assert.Equal (1, finalLength);
        Assert.Equal ("a", result);
    }

    [Fact]
    public void Ctor_Span_CanAppend ()
    {
        // Arrange
        using ValueStringBuilder vsb = new (new char[1]);

        // Act
        var initialLength = vsb.Length;
        vsb.Append ('a');
        var finalLength = vsb.Length;
        var result = vsb.ToString ();

        // Assert
        Assert.Equal (0, initialLength);
        Assert.Equal (1, finalLength);
        Assert.Equal ("a", result);
    }

    [Fact]
    public void Ctor_InitialCapacity_CanAppend ()
    {
        // Arrange
        using ValueStringBuilder vsb = new (1);

        // Act
        var initialLength = vsb.Length;
        vsb.Append ('a');
        var finalLength = vsb.Length;
        var result = vsb.ToString ();

        // Assert
        Assert.Equal (0, initialLength);
        Assert.Equal (1, finalLength);
        Assert.Equal ("a", result);
    }

    [Fact]
    public void Append_Char_MatchesStringBuilder ()
    {
        // Arrange
        StringBuilder sb = new ();
        using ValueStringBuilder vsb = new ();

        // Act
        for (var i = 1; i <= 100; i++)
        {
            sb.Append ((char)i);
            vsb.Append ((char)i);
        }

        var actualLength = vsb.Length;
        var expectedLength = sb.Length;
        var actual = vsb.ToString ();
        var expected = sb.ToString ();

        // Assert
        Assert.Equal (expectedLength, actualLength);
        Assert.Equal (expected, actual);
    }

    [Fact]
    public void Append_String_MatchesStringBuilder ()
    {
        // Arrange
        StringBuilder sb = new ();
        using ValueStringBuilder vsb = new ();

        // Act
        for (var i = 1; i <= 100; i++)
        {
            var s = i.ToString ();
            sb.Append (s);
            vsb.Append (s);
        }

        var actualLength = vsb.Length;
        var expectedLength = sb.Length;
        var actual = vsb.ToString ();
        var expected = sb.ToString ();

        // Assert
        Assert.Equal (expectedLength, actualLength);
        Assert.Equal (expected, actual);
    }

    [Theory]
    [InlineData (0, 4 * 1024 * 1024)]
    [InlineData (1025, 4 * 1024 * 1024)]
    [InlineData (3 * 1024 * 1024, 6 * 1024 * 1024)]
    public void Append_String_Large_MatchesStringBuilder (int initialLength, int stringLength)
    {
        // Arrange
        StringBuilder sb = new (initialLength);
        using ValueStringBuilder vsb = new (new char[initialLength]);
        var s = new string ('a', stringLength);

        // Act
        sb.Append (s);
        vsb.Append (s);

        var actualLength = vsb.Length;
        var expectedLength = sb.Length;
        var actual = vsb.ToString ();
        var expected = sb.ToString ();

        // Assert
        Assert.Equal (expectedLength, actualLength);
        Assert.Equal (expected, actual);
    }

    [Fact]
    public void Append_CharInt_MatchesStringBuilder ()
    {
        // Arrange
        StringBuilder sb = new ();
        using ValueStringBuilder vsb = new ();

        // Act
        for (var i = 1; i <= 100; i++)
        {
            sb.Append ((char)i, i);
            vsb.Append ((char)i, i);
        }

        var actualLength = vsb.Length;
        var expectedLength = sb.Length;
        var actual = vsb.ToString ();
        var expected = sb.ToString ();

        // Assert
        Assert.Equal (expectedLength, actualLength);
        Assert.Equal (expected, actual);
    }

    [Fact]
    public void AppendSpan_Capacity ()
    {
        // Arrange
        using ValueStringBuilder vsb = new ();

        // Act
        vsb.AppendSpan (17);
        var capacityAfterFirstAppendSpan = vsb.Capacity;

        vsb.AppendSpan (100);
        var capacityAfterSecondAppendSpan = vsb.Capacity;

        // Assert
        Assert.Equal (32, capacityAfterFirstAppendSpan);
        Assert.Equal (128, capacityAfterSecondAppendSpan);
    }

    [Fact]
    public void AppendSpan_DataAppendedCorrectly ()
    {
        // Arrange
        StringBuilder sb = new ();
        using ValueStringBuilder vsb = new ();

        // Act
        for (var i = 1; i <= 1000; i++)
        {
            var s = i.ToString ();

            sb.Append (s);

            Span<char> span = vsb.AppendSpan (s.Length);
            s.AsSpan ().CopyTo (span);
        }

        var actualLength = vsb.Length;
        var expectedLength = sb.Length;
        var actual = vsb.ToString ();
        var expected = sb.ToString ();

        // Assert
        Assert.Equal (expectedLength, actualLength);
        Assert.Equal (expected, actual);
    }

    [Fact]
    public void Insert_IntCharInt_MatchesStringBuilder ()
    {
        // Arrange
        StringBuilder sb = new ();
        using ValueStringBuilder vsb = new ();
        Random rand = new (42);

        // Act
        for (var i = 1; i <= 100; i++)
        {
            var index = rand.Next (sb.Length);
            sb.Insert (index, new string ((char)i, 1), i);
            vsb.Insert (index, (char)i, i);
        }

        var actualLength = vsb.Length;
        var expectedLength = sb.Length;
        var actual = vsb.ToString ();
        var expected = sb.ToString ();

        // Assert
        Assert.Equal (expectedLength, actualLength);
        Assert.Equal (expected, actual);
    }

    [Fact]
    public void Insert_IntString_MatchesStringBuilder ()
    {
        // Arrange
        StringBuilder sb = new ();
        ValueStringBuilder vsb = new ();

        // Act
        sb.Insert (0, new string ('a', 6));
        vsb.Insert (0, new string ('a', 6));
        var lengthAfterFirstInsert = vsb.Length;
        var capacityAfterFirstInsert = vsb.Capacity;

        sb.Insert (0, new string ('b', 11));
        vsb.Insert (0, new string ('b', 11));
        var lengthAfterSecondInsert = vsb.Length;
        var capacityAfterSecondInsert = vsb.Capacity;

        sb.Insert (0, new string ('c', 15));
        vsb.Insert (0, new string ('c', 15));
        var lengthAfterThirdInsert = vsb.Length;
        var capacityAfterThirdInsert = vsb.Capacity;

        sb.Length = 24;
        vsb.Length = 24;

        sb.Insert (0, new string ('d', 40));
        vsb.Insert (0, new string ('d', 40));
        var finalLength = vsb.Length;
        var finalCapacity = vsb.Capacity;
        var actual = vsb.ToString ();
        var expected = sb.ToString ();

        // Assert
        Assert.Equal (6, lengthAfterFirstInsert);
        Assert.Equal (16, capacityAfterFirstInsert);

        Assert.Equal (17, lengthAfterSecondInsert);
        Assert.Equal (32, capacityAfterSecondInsert);

        Assert.Equal (32, lengthAfterThirdInsert);
        Assert.Equal (32, capacityAfterThirdInsert);

        Assert.Equal (64, finalLength);
        Assert.Equal (64, finalCapacity);

        Assert.Equal (sb.Length, finalLength);
        Assert.Equal (expected, actual);
    }

    [Fact]
    public void AsSpan_ReturnsCorrectValue_DoesntClearBuilder ()
    {
        // Arrange
        StringBuilder sb = new ();
        using ValueStringBuilder vsb = new ();

        for (var i = 1; i <= 100; i++)
        {
            var s = i.ToString ();
            sb.Append (s);
            vsb.Append (s);
        }

        // Act
        var resultString = new string (vsb.AsSpan ());
        var stringBuilderLength = sb.Length;
        var valueStringBuilderLength = vsb.Length;
        var valueStringBuilderString = vsb.ToString ();
        var stringBuilderString = sb.ToString ();

        // Assert
        Assert.Equal (stringBuilderString, resultString);
        Assert.NotEqual (0, stringBuilderLength);
        Assert.Equal (stringBuilderLength, valueStringBuilderLength);
        Assert.Equal (stringBuilderString, valueStringBuilderString);
    }

    [Fact]
    public void ToString_ClearsBuilder_ThenReusable ()
    {
        // Arrange
        const string text1 = "test";
        const string text2 = "another test";
        using ValueStringBuilder vsb = new ();

        vsb.Append (text1);

        // Act
        var lengthBeforeToString = vsb.Length;
        var firstResult = vsb.ToString ();
        var lengthAfterToString = vsb.Length;
        var secondResult = vsb.ToString ();

        vsb.Append (text2);
        var finalLength = vsb.Length;
        var finalResult = vsb.ToString ();

        // Assert
        Assert.Equal (text1.Length, lengthBeforeToString);
        Assert.Equal (text1, firstResult);
        Assert.Equal (0, lengthAfterToString);
        Assert.Equal (string.Empty, secondResult);
        Assert.Equal (text2.Length, finalLength);
        Assert.Equal (text2, finalResult);
    }

    [Fact]
    public void Dispose_ClearsBuilder_ThenReusable ()
    {
        // Arrange
        const string text1 = "test";
        const string text2 = "another test";
        ValueStringBuilder vsb = new ();

        vsb.Append (text1);

        // Act
        var lengthBeforeDispose = vsb.Length;
        vsb.Dispose ();
        var lengthAfterDispose = vsb.Length;
        var resultAfterDispose = vsb.ToString ();

        vsb.Append (text2);
        var finalLength = vsb.Length;
        var finalResult = vsb.ToString ();

        // Assert
        Assert.Equal (text1.Length, lengthBeforeDispose);
        Assert.Equal (0, lengthAfterDispose);
        Assert.Equal (string.Empty, resultAfterDispose);
        Assert.Equal (text2.Length, finalLength);
        Assert.Equal (text2, finalResult);
    }

    [Fact]
    public void Indexer ()
    {
        // Arrange
        const string text1 = "foobar";
        ValueStringBuilder vsb = new ();
        vsb.Append (text1);

        // Act
        var originalCharacter = vsb[3];
        vsb[3] = 'c';
        var updatedCharacter = vsb[3];
        vsb.Dispose ();

        // Assert
        Assert.Equal ('b', originalCharacter);
        Assert.Equal ('c', updatedCharacter);
    }

    [Fact]
    public void EnsureCapacity_IfRequestedCapacityWins ()
    {
        // Arrange
        // Note: constants used here may be dependent on minimal buffer size
        // the ArrayPool is able to return.
        using ValueStringBuilder builder = new (stackalloc char[32]);

        // Act
        builder.EnsureCapacity (65);
        var capacity = builder.Capacity;

        // Assert
        Assert.Equal (128, capacity);
    }

    [Fact]
    public void EnsureCapacity_IfBufferTimesTwoWins ()
    {
        // Arrange
        ValueStringBuilder builder = new (stackalloc char[32]);

        // Act
        builder.EnsureCapacity (33);
        var capacity = builder.Capacity;
        builder.Dispose ();

        // Assert
        Assert.Equal (64, capacity);
    }

    [Fact]
    public void EnsureCapacity_NoAllocIfNotNeeded ()
    {
        // Arrange
        // Note: constants used here may be dependent on minimal buffer size
        // the ArrayPool is able to return.
        ValueStringBuilder builder = new (stackalloc char[64]);

        // Act
        builder.EnsureCapacity (16);
        var capacity = builder.Capacity;
        builder.Dispose ();

        // Assert
        Assert.Equal (64, capacity);
    }
}

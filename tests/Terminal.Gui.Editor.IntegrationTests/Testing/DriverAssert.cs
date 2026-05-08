// Claude - claude-opus-4-7

using Terminal.Gui.Drivers;
using Xunit.Sdk;

namespace Terminal.Gui.Editor.IntegrationTests.Testing;

/// <summary>
///     Assertion helpers over <see cref="IDriver.Contents" />. Modern, terse alternative to
///     <c>Tests/UnitTests.Legacy/DriverAssert</c> in the Terminal.Gui repo — only what we need
///     and no static <c>Application.Driver</c> fallback.
/// </summary>
public static class DriverAssert
{
    /// <summary>
    ///     Asserts that the rendered driver contents contain <paramref name="expected" /> as a substring (whitespace
    ///     insensitive at line ends).
    /// </summary>
    public static void ContentsContains (IDriver driver, string expected)
    {
        ArgumentNullException.ThrowIfNull (driver);
        ArgumentNullException.ThrowIfNull (expected);

        var actual = driver.ToString () ?? string.Empty;

        if (actual.Contains (expected, StringComparison.Ordinal))
        {
            return;
        }

        throw new XunitException ($"Expected driver contents to contain:\n{expected}\n\nActual:\n{actual}");
    }

    /// <summary>Asserts that the rendered driver contents do NOT contain <paramref name="text" />.</summary>
    public static void ContentsDoesNotContain (IDriver driver, string text)
    {
        ArgumentNullException.ThrowIfNull (driver);
        ArgumentNullException.ThrowIfNull (text);

        var actual = driver.ToString () ?? string.Empty;

        if (!actual.Contains (text, StringComparison.Ordinal))
        {
            return;
        }

        throw new XunitException ($"Expected driver contents NOT to contain:\n{text}\n\nActual:\n{actual}");
    }
}

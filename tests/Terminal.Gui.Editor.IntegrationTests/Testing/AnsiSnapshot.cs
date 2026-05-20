// Claude - claude-opus-4-7

using System.Runtime.CompilerServices;
using System.Text;
using Terminal.Gui.Drivers;
using Xunit;
using Xunit.Sdk;

namespace Terminal.Gui.Editor.IntegrationTests.Testing;

/// <summary>
///     Golden-file snapshot of the rendered screen as <b>pure ANSI</b>.
///     <see cref="IDriver.ToAnsi" /> emits exactly the escape-sequence stream the driver would
///     write to recreate the current screen contents — colors, text styles, layout, everything
///     except the terminal cursor (which is a separate, non-deterministic <c>SetCursor</c>). So a
///     recorded <c>.ans</c> file <i>is</i> the look: <c>cat &lt;file&gt;.ans</c> in any terminal
///     reproduces it faithfully.
/// </summary>
/// <remarks>
///     <para>
///         The point is letting an agent iterate on rendering without a human eyeballing the
///         result. On mismatch the failure prints the plain-text render inline (visible directly
///         in the test log, no terminal needed) and writes a sibling <c>.ans.actual</c> the agent
///         can <c>cat</c> for an exact, full-fidelity view before accepting.
///     </para>
///     <para>
///         Workflow: first run records the golden and passes. Later runs compare byte-for-byte.
///         To accept an intended change, re-run with <c>UPDATE_SNAPSHOTS=1</c> (or delete the
///         <c>.ans</c> file). Goldens live next to the test source in <c>__snapshots__/</c>;
///         override the root with <c>SNAPSHOT_DIR</c>.
///     </para>
/// </remarks>
public static class AnsiSnapshot
{
    private static bool UpdateRequested =>
        Environment.GetEnvironmentVariable ("UPDATE_SNAPSHOTS") is "1" or "true";

    /// <summary>
    ///     Compares the driver's current ANSI render against the golden named <paramref name="name" />.
    ///     Records the golden (and passes) when it does not exist or <c>UPDATE_SNAPSHOTS</c> is set.
    /// </summary>
    /// <param name="driver">The driver to snapshot — typically <c>fx.Driver</c> after <c>fx.Render ()</c>.</param>
    /// <param name="name">Stable snapshot name, unique within the test class (becomes <c>&lt;name&gt;.ans</c>).</param>
    /// <param name="callerFile">Compiler-supplied; locates <c>__snapshots__/</c> beside the test source.</param>
    public static void Verify (IDriver driver, string name, [CallerFilePath] string callerFile = "")
    {
        ArgumentNullException.ThrowIfNull (driver);
        ArgumentException.ThrowIfNullOrWhiteSpace (name);

        var actual = Canonicalize (driver.ToAnsi ());
        var dir = SnapshotDir (callerFile);
        Directory.CreateDirectory (dir);
        var path = Path.Combine (dir, name + ".ans");

        if (UpdateRequested || !File.Exists (path))
        {
            WriteRaw (path, actual);
            var verb = UpdateRequested ? "updated" : "recorded";
            TestContext.Current.SendDiagnosticMessage ($"[snapshot {verb}] {path} — `cat` it to verify the look.");

            return;
        }

        var expected = Canonicalize (File.ReadAllText (path));

        if (string.Equals (expected, actual, StringComparison.Ordinal))
        {
            return;
        }

        var actualPath = path + ".actual";
        WriteRaw (actualPath, actual);

        throw new XunitException (
            $"""
             ANSI snapshot '{name}' does not match {path}.

             Plain-text render of the actual screen (cell glyphs only — attributes/colors omitted):
             ----------------------------------------------------------------------
             {driver}
             ----------------------------------------------------------------------

             Exact look (with colors/styles): cat '{actualPath}'
             Expected look:                   cat '{path}'

             If this change is intended, accept it by re-running with UPDATE_SNAPSHOTS=1
             (or copy the .actual over the .ans).
             """);
    }

    private static string SnapshotDir (string callerFile)
    {
        var overrideDir = Environment.GetEnvironmentVariable ("SNAPSHOT_DIR");

        if (!string.IsNullOrWhiteSpace (overrideDir))
        {
            return overrideDir;
        }

        var sourceDir = Path.GetDirectoryName (callerFile);

        if (string.IsNullOrEmpty (sourceDir))
        {
            throw new InvalidOperationException (
                "Could not resolve the snapshot directory from the caller path. Set SNAPSHOT_DIR.");
        }

        return Path.Combine (sourceDir, "__snapshots__");
    }

    // TG's OutputBase.ToAnsi separates rows with StringBuilder.AppendLine () == Environment.NewLine
    // — CRLF on Windows, LF elsewhere. Without this, a golden recorded on one OS never matches
    // another OS's render (the CI failure that motivated this). Canonicalize both sides to LF.
    // `cat` fidelity is unaffected: terminals map LF -> CRLF via the ONLCR tty discipline, so a
    // LF-only .ans still reproduces the screen exactly.
    private static string Canonicalize (string? ansi)
    {
        return (ansi ?? string.Empty).Replace ("\r\n", "\n").Replace ("\r", "\n");
    }

    // UTF-8 without BOM. Content is already LF-canonical; write it verbatim (no extra
    // translation) so the on-disk golden stays a faithful `cat`-able reproduction.
    private static void WriteRaw (string path, string ansi)
    {
        File.WriteAllText (path, ansi, new UTF8Encoding (false));
    }
}

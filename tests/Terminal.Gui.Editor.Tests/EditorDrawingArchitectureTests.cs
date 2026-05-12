// Claude - claude-opus-4-7

using System.Text;
using System.Text.RegularExpressions;
using Xunit;

namespace Terminal.Gui.Editor.Tests;

/// <summary>
///     Source-level guards for the rules in <c>specs/constitution.md</c>:
///     <list type="bullet">
///         <item>R1 — "No new feature draws directly inside <c>OnDrawingContent</c>." The body stays a thin walker.</item>
///         <item>R2 — "Cells, not chars." No per-character iteration of rendered text in the draw path.</item>
///     </list>
///     These read <c>Editor.Drawing.cs</c> as a string and assert structural properties. Yes, regex
///     over source is brittle, but the failure mode they guard against is *also* a regex-grep — a
///     developer reintroducing a `for (i = 0; i &lt; text.Length; ...)` loop. If a legitimate change
///     forces these to trip, the right move is to tighten the parse (Roslyn), not loosen the rule.
/// </summary>
public class EditorDrawingArchitectureTests
{
    /// <summary>Hard cap matching the <c>drawing-overhaul</c> spec target (FR-007).</summary>
    private const int OnDrawingContentMaxLines = 30;

    [Fact]
    public void OnDrawingContent_Body_Has_No_Char_Iteration ()
    {
        var body = ExtractMethodBody (ReadDrawingSource (), "OnDrawingContent");

        // The classic R2 violation. Allow `for (var lineIndex = ...)` and similar over LINES; only
        // ban iteration whose bound is something `.Length` (text/string/char span).
        Regex charIteration = new (
            @"for\s*\(\s*(?:var|int)\s+\w+\s*=\s*0\s*;\s*\w+\s*<\s*[\w.]+\.Length\s*;");

        Assert.False (
            charIteration.IsMatch (body),
            "OnDrawingContent must not iterate text character-by-character. "
            + "Visual features land as IVisualLineTransformer / IBackgroundRenderer; "
            + "the draw path walks CellVisualLine elements. (constitution.md §R2)");
    }

    [Fact]
    public void OnDrawingContent_Body_Is_Under_30_Lines ()
    {
        var body = ExtractMethodBody (ReadDrawingSource (), "OnDrawingContent");
        var lineCount = body.Split ('\n').Length;

        Assert.True (
            lineCount <= OnDrawingContentMaxLines,
            $"OnDrawingContent body is {lineCount} lines; cap is {OnDrawingContentMaxLines}. "
            + "Extract helpers — the draw path is a thin walker. (constitution.md §R1, drawing-overhaul §FR-007)");
    }

    [Fact]
    public void Deleted_Column_Conversion_Wrappers_Are_Not_Reintroduced ()
    {
        var source = ReadAllEditorSources ();

        Assert.DoesNotContain ("DrawLineContent", source);
        Assert.DoesNotContain ("GetVisualWidthForCharacter", source);
        Assert.DoesNotContain ("private int GetVisualColumnFromLogicalColumn", source);
        Assert.DoesNotContain ("private int GetLogicalColumnFromVisualColumn", source);
    }

    private static string ReadDrawingSource ()
    {
        return File.ReadAllText (LocateSource ("Editor.Drawing.cs"));
    }

    private static string ReadAllEditorSources ()
    {
        var dir = Path.GetDirectoryName (LocateSource ("Editor.cs"))!;
        StringBuilder sb = new ();

        foreach (var path in Directory.EnumerateFiles (dir, "Editor*.cs", SearchOption.TopDirectoryOnly))
        {
            sb.AppendLine (File.ReadAllText (path));
        }

        return sb.ToString ();
    }

    private static string LocateSource (string fileName)
    {
        // Walk up from the test binary to the repo root, then drop into src/.
        var dir = AppContext.BaseDirectory;

        while (dir is not null)
        {
            var candidate = Path.Combine (dir, "src", "Terminal.Gui.Editor", fileName);

            if (File.Exists (candidate))
            {
                return candidate;
            }

            dir = Path.GetDirectoryName (dir);
        }

        throw new FileNotFoundException (
            $"Could not locate {fileName} by walking up from {AppContext.BaseDirectory}.");
    }

    /// <summary>
    ///     Extracts the body of <paramref name="methodName" /> from <paramref name="source" /> by
    ///     finding the method's opening signature line and matching braces. Best-effort but
    ///     sufficient for a single source file's protected/public methods.
    /// </summary>
    private static string ExtractMethodBody (string source, string methodName)
    {
        Regex signature = new (
            $@"^\s*(?:protected|public|private|internal)[\w\s\?]*\b{Regex.Escape (methodName)}\s*\([^)]*\)\s*$",
            RegexOptions.Multiline);

        Match match = signature.Match (source);
        Assert.True (match.Success, $"Could not find signature line for {methodName}");

        var openBrace = source.IndexOf ('{', match.Index + match.Length);
        Assert.True (openBrace >= 0, $"Could not find opening brace for {methodName}");

        var depth = 0;
        var i = openBrace;

        while (i < source.Length)
        {
            if (source[i] == '{')
            {
                depth++;
            }
            else if (source[i] == '}')
            {
                depth--;

                if (depth == 0)
                {
                    return source.Substring (openBrace + 1, i - openBrace - 1);
                }
            }

            i++;
        }

        throw new InvalidOperationException ($"Unbalanced braces while extracting {methodName}");
    }
}

// Codex - GPT-5

using Xunit;

namespace Terminal.Gui.Editor.Tests;

/// <summary>
///     Source-level guard for constitution R4: caret-after-edit tracking belongs to
///     <c>TextAnchor</c>, not hand-rolled offset arithmetic in <c>Editor.OnDocumentChanged</c>.
/// </summary>
public class EditorCaretAnchorArchitectureTests
{
    [Fact]
    public void Editor_Uses_TextAnchor_For_Caret_Tracking ()
    {
        var source = File.ReadAllText (LocateSource ("Editor.cs"));

        Assert.Contains ("TextAnchor? _caretAnchor", source);
        Assert.Contains ("AnchorMovementType.AfterInsertion", source);
        Assert.DoesNotContain ("private int _caretOffset", source);
        Assert.DoesNotContain ("if (_caretOffset >= e.Offset)", source);
    }

    private static string LocateSource (string fileName)
    {
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
}

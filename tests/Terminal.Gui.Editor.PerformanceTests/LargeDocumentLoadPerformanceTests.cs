using System.Diagnostics;
using System.Text;
using Terminal.Gui.Editor.Highlighting;
using Xunit;

namespace Terminal.Gui.Editor.PerformanceTests;

public class LargeDocumentLoadPerformanceTests
{
    /// <summary>
    ///     A 10 MiB / ~150k-line C#-highlighted document must load through <see cref="Editor.LoadAsync" />
    ///     well under budget. Before max-width virtualization this took ~10 s because the editor built and
    ///     syntax-highlighted a <c>CellVisualLine</c> for every line just to size the horizontal scrollbar.
    ///     The model layer alone loads in ~0.2 s, so 3 s is a deliberately loose CI-jitter budget (~5×).
    /// </summary>
    [Fact]
    public async Task Editor_LoadAsync_10Mb_HighlightedSource_CompletesWellUnderBudget ()
    {
        StringBuilder sb = new (10 * 1024 * 1024 + 128);

        while (sb.Length < 10 * 1024 * 1024)
        {
            sb.Append ("        private const int Id = 12345; // a representative C# source line\n");
        }

        var bytes = Encoding.UTF8.GetBytes (sb.ToString ());

        Editor editor = new ()
        {
            HighlightingDefinition = HighlightingManager.Instance.GetDefinitionByExtension (".cs")
        };

        await using MemoryStream stream = new (bytes);

        Stopwatch sw = Stopwatch.StartNew ();
        await editor.LoadAsync (stream, cancellationToken: TestContext.Current.CancellationToken);
        sw.Stop ();

        Assert.Equal (bytes.Length, editor.Document!.TextLength);
        Assert.True (
            sw.ElapsedMilliseconds < 3000,
            $"Editor.LoadAsync of {bytes.Length:N0} bytes took {sw.ElapsedMilliseconds} ms — "
            + "expected < 3000 ms (was ~10 s before max-width virtualization).");
    }
}

using System.Diagnostics;
using System.Text;
using Terminal.Gui.Document;
using Xunit;

namespace Terminal.Gui.Editor.PerformanceTests;

public class StreamingLoadPerformanceTests
{
    [Fact]
    public async Task StreamingLoad_10Mb_ReportsInitialProgressWithinBudget ()
    {
        byte[] bytes = Encoding.UTF8.GetBytes (new string ('x', 10 * 1024 * 1024));
        await using MemoryStream stream = new (bytes);
        TaskCompletionSource firstProgress = new ();
        Progress<TextDocumentProgress> progress = new (_ => firstProgress.TrySetResult ());

        Stopwatch sw = Stopwatch.StartNew ();
        Task<TextDocument> loadTask = TextDocument.LoadAsync (
            stream,
            progress: progress,
            cancellationToken: TestContext.Current.CancellationToken);
        Task completed = await Task.WhenAny (
            firstProgress.Task,
            Task.Delay (TimeSpan.FromMilliseconds (200), TestContext.Current.CancellationToken));
        sw.Stop ();

        Assert.Same (firstProgress.Task, completed);
        Assert.True (sw.ElapsedMilliseconds < 200,
            $"Initial streaming load progress took {sw.ElapsedMilliseconds}ms — expected < 200ms.");

        _ = await loadTask;
    }
}

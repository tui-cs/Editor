using System.Diagnostics;
using System.Text;
using Terminal.Gui.Editor.Document;
using Xunit;

namespace Terminal.Gui.Editor.PerformanceTests;

public class StreamingLoadPerformanceTests
{
    private static TimeSpan InitialProgressBudget =>
        // Windows runners frequently show higher variance for this test due to OS/AV/IO scheduling noise.
        // Keep a slightly looser budget so the test stays a regression signal without being flaky.
        OperatingSystem.IsWindows () ? TimeSpan.FromMilliseconds (800) : TimeSpan.FromMilliseconds (500);

    [Fact]
    public async Task StreamingLoad_10Mb_ReportsInitialProgressWithinBudget ()
    {
        var bytes = Encoding.UTF8.GetBytes (new string ('x', 10 * 1024 * 1024));
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
            Task.Delay (InitialProgressBudget, TestContext.Current.CancellationToken));
        sw.Stop ();

        Assert.Same (firstProgress.Task, completed);
        Assert.True (sw.Elapsed < InitialProgressBudget,
            $"Initial streaming load progress took {sw.ElapsedMilliseconds}ms — expected < {InitialProgressBudget.TotalMilliseconds:N0}ms.");

        _ = await loadTask;
    }
}

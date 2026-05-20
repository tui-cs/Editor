// Claude - claude-opus-4-7

using System.Collections.Concurrent;
using System.Text;
using Xunit;

namespace Terminal.Gui.Editor.Tests;

/// <summary>
///     Guards the progressive (marshalled) <see cref="Editor.LoadAsync" /> path ted uses for large
///     CLI opens. The cost that made a 10 MiB open take ~8 s in a running app was not the document
///     model (~0.75 s) but the <em>number</em> of marshalled flushes: each flush is a UI-thread
///     round-trip plus a full render frame that repaints the (unchanged, caret-pinned) top screen.
///     Coarse subsequent flushing keeps that count tiny. A regression to small chunks would push it
///     back to ~160 and is exactly what this test fails on. Deterministic — no wall-clock, no UI —
///     so it belongs here, not in PerformanceTests.
/// </summary>
public class EditorProgressiveLoadTests
{
    // Runs LoadAsync on the progressive path, pumping every marshalled action on THIS thread (the
    // document's owner thread, as ted's App.Invoke marshal ultimately does) and counting flushes.
    private static async Task<int> CountMarshalledFlushes (Editor editor, byte[] bytes, CancellationToken ct)
    {
        await using MemoryStream stream = new (bytes);
        BlockingCollection<(Action act, TaskCompletionSource tcs)> queue = new ();
        var flushes = 0;

        Task Marshal (Action a)
        {
            TaskCompletionSource tcs = new (TaskCreationOptions.RunContinuationsAsynchronously);
            queue.Add ((a, tcs), ct);

            return tcs.Task;
        }

        Task load = editor.LoadAsync (stream, marshal: Marshal, cancellationToken: ct);

        while (!load.IsCompleted)
        {
            if (!queue.TryTake (out (Action act, TaskCompletionSource tcs) item, 25, ct))
            {
                continue;
            }

            flushes++;
            RunPumpItem (item);
        }

        while (queue.TryTake (out (Action act, TaskCompletionSource tcs) item))
        {
            flushes++;
            RunPumpItem (item);
        }

        await load;

        return flushes;
    }

    private static void RunPumpItem ((Action act, TaskCompletionSource tcs) item)
    {
        try
        {
            item.act ();
            item.tcs.SetResult ();
        }
        catch (Exception ex)
        {
            item.tcs.SetException (ex);
        }
    }

    [Fact]
    public async Task ProgressiveLoad_10Mb_UsesCoarseFlushing ()
    {
        StringBuilder sb = new (10 * 1024 * 1024 + 128);

        while (sb.Length < 10 * 1024 * 1024)
        {
            sb.Append ("        private const int Id = 12345; // a representative C# source line\n");
        }

        var bytes = Encoding.UTF8.GetBytes (sb.ToString ());

        Editor editor = new ();
        var flushes = await CountMarshalledFlushes (editor, bytes, TestContext.Current.CancellationToken);

        Assert.Equal (bytes.Length, editor.Document!.TextLength);

        // 16 KiB first flush + ~10 × 1 MiB subsequent flushes ≈ 11. The pre-fix 64 KiB chunking
        // produced ~161. 20 leaves headroom for buffering jitter while still failing hard on a
        // regression to small chunks (the ~8 s → ~1 s bug).
        Assert.True (
            flushes <= 20,
            $"Progressive 10 MiB load marshalled {flushes} flushes — expected <= 20 (coarse "
            + "subsequent flushing). A value near ~160 means SubsequentFlushChars regressed to a "
            + "small value, which makes a large CLI open take several seconds in a running app.");
    }
}

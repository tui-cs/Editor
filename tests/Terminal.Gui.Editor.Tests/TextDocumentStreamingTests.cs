// CoPilot - gpt-5.4

using System.Text;
using Terminal.Gui.Document;
using Xunit;

namespace Terminal.Gui.Editor.Tests;

public class TextDocumentStreamingTests
{
    [Fact]
    public async Task LoadAsync_SaveAsync_RoundTrips_MixedLineEndings_AndBom ()
    {
        UTF8Encoding encoding = new (true);
        var text = "one\r\ntwo\nthree\rfour";
        var bytes = encoding.GetPreamble ().Concat (encoding.GetBytes (text)).ToArray ();

        await using MemoryStream input = new (bytes);
        TextDocument document = await TextDocument.LoadAsync (
            input,
            cancellationToken: TestContext.Current.CancellationToken);

        await using MemoryStream output = new ();
        await document.SaveAsync (output, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal (text, document.Text);
        Assert.Equal (bytes, output.ToArray ());
    }

    [Fact]
    public async Task LoadAsync_Reports_Multiple_Progress_Updates_For_Large_Stream ()
    {
        var text = new string ('x', 100_000);
        await using MemoryStream input = new (Encoding.UTF8.GetBytes (text));
        List<TextDocumentProgress> reports = [];
        CapturingProgress progress = new (reports);

        TextDocument document = await TextDocument.LoadAsync (
            input,
            progress: progress,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal (text.Length, document.TextLength);
        Assert.True (reports.Count > 1);
        Assert.Equal (text.Length, reports[^1].CharactersProcessed);
    }

    [Fact]
    public async Task LoadAsync_Observes_Cancellation ()
    {
        await using MemoryStream input = new (Encoding.UTF8.GetBytes ("abc"));
        using CancellationTokenSource cts = new ();
        await cts.CancelAsync ();

        await Assert.ThrowsAsync<OperationCanceledException> (() =>
            TextDocument.LoadAsync (input, cancellationToken: cts.Token));
    }

    [Fact]
    public async Task Editor_LoadAsync_And_SaveAsync_Delegate_To_Document ()
    {
        Editor editor = new ();
        await using MemoryStream input = new (Encoding.UTF8.GetBytes ("alpha\r\nbeta"));

        await editor.LoadAsync (input, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal ("alpha\r\nbeta", editor.Document!.Text);

        await using MemoryStream output = new ();
        await editor.SaveAsync (output, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal ("alpha\r\nbeta", Encoding.UTF8.GetString (output.ToArray ()));
    }

    private sealed class CapturingProgress : IProgress<TextDocumentProgress>
    {
        private readonly List<TextDocumentProgress> _reports;

        public CapturingProgress (List<TextDocumentProgress> reports)
        {
            _reports = reports;
        }

        public void Report (TextDocumentProgress value)
        {
            _reports.Add (value);
        }
    }
}

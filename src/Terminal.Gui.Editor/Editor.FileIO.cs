using System.Text;
using Terminal.Gui.Document;

namespace Terminal.Gui.Editor;

public partial class Editor
{
    /// <summary>
    ///     Streams text from <paramref name="stream" /> into <see cref="Document" /> by delegating to
    ///     <see cref="TextDocument.LoadAsync" />.
    /// </summary>
    public async Task LoadAsync (
        Stream stream,
        Encoding? encoding = null,
        IProgress<TextDocumentProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull (stream);

        Document?.SetOwnerThread (null);
        TextDocument document = await TextDocument.LoadAsync (stream, encoding, progress, cancellationToken);
        document.SetOwnerThread (Thread.CurrentThread);
        Document = document;
        CaretOffset = 0;
        document.SetOwnerThread (null);
    }

    /// <summary>
    ///     Streams <see cref="Document" /> to <paramref name="stream" /> by delegating to
    ///     <see cref="TextDocument.SaveAsync" />.
    /// </summary>
    public Task SaveAsync (
        Stream stream,
        IProgress<TextDocumentProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        TextDocument document = Document
                                ?? throw new InvalidOperationException ("Cannot save because the editor has no document.");

        return document.SaveAsync (stream, progress, cancellationToken);
    }
}

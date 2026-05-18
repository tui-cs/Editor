namespace Ted;

/// <summary>
///     Reports progress synchronously for non-hosted app scenarios where <see cref="Progress{T}" />
///     would queue callbacks to the thread pool instead of an application UI thread.
/// </summary>
internal sealed class InlineProgress<T> : IProgress<T>
{
    private readonly Action<T> _handler;

    public InlineProgress (Action<T> handler)
    {
        _handler = handler ?? throw new ArgumentNullException (nameof (handler));
    }

    public void Report (T value)
    {
        _handler (value);
    }
}

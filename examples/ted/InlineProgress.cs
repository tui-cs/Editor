namespace Ted;

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

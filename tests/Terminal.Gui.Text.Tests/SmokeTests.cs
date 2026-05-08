// Claude - claude-opus-4-7
using Xunit;

namespace Terminal.Gui.Text.Tests;

public class SmokeTests
{
    [Fact]
    public void AssemblyLoads ()
    {
        Assert.NotNull (typeof (AssemblyMarker).Assembly);
    }
}

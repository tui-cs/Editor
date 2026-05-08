// Claude - claude-opus-4-7
using Xunit;

namespace Terminal.Gui.Editor.Tests;

public class SmokeTests
{
    [Fact]
    public void AssemblyLoads ()
    {
        Assert.NotNull (typeof (Terminal.Gui.Views.Editor).Assembly);
    }
}

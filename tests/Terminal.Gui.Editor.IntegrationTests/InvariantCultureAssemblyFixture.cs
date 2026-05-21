using System.Globalization;
using Terminal.Gui.Editor.IntegrationTests;
using Xunit;

[assembly: AssemblyFixture (typeof (InvariantCultureAssemblyFixture))]

namespace Terminal.Gui.Editor.IntegrationTests;

public sealed class InvariantCultureAssemblyFixture : IDisposable
{
    private readonly CultureInfo? _originalDefaultCulture = CultureInfo.DefaultThreadCurrentCulture;
    private readonly CultureInfo? _originalDefaultUiCulture = CultureInfo.DefaultThreadCurrentUICulture;

    public InvariantCultureAssemblyFixture ()
    {
        CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
        CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.InvariantCulture;
    }

    public void Dispose ()
    {
        CultureInfo.DefaultThreadCurrentCulture = _originalDefaultCulture;
        CultureInfo.DefaultThreadCurrentUICulture = _originalDefaultUiCulture;
    }
}

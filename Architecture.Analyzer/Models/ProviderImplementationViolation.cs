using Microsoft.CodeAnalysis;

namespace Architecture.Analyzer.Models;

internal sealed class ProviderImplementationViolation
{
    public ProviderImplementationViolation(string interfaceName, string implementationName, Location location)
    {
        InterfaceName = interfaceName;
        ImplementationName = implementationName;
        Location = location;
    }

    public string InterfaceName { get; }

    public string ImplementationName { get; }

    public Location Location { get; }
}
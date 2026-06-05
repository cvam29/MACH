using ArchUnitNET.Domain;
using ArchUnitNET.Fluent;
using ArchUnitNET.Loader;
using ArchUnitNET.xUnit;
using ReflectionAssembly = System.Reflection.Assembly;
using static ArchUnitNET.Fluent.ArchRuleDefinition;

namespace Mach.Architecture.Tests;

/// <summary>
/// Enforces the hexagonal dependency rule:
/// Doors → Brain + Translators + ServiceDefaults; Translators → Brain only;
/// Brain (Domain) → nothing in-solution.
/// </summary>
public class DependencyRuleTests
{
    private const string DomainPattern = @"Mach\.Domain($|\..*)";
    private const string ApplicationPattern = @"Mach\.Application($|\..*)";
    private const string ContractsPattern = @"Mach\.Contracts($|\..*)";
    private const string InfrastructurePattern = @"Mach\.Infrastructure\..*";
    private const string ServiceDefaultsPattern = @"Mach\.ServiceDefaults($|\..*)";
    private const string HostPattern = @"Mach\..*\.Functions($|\..*)";

    private static readonly ArchUnitNET.Domain.Architecture Arch = LoadArchitecture();

    private static ArchUnitNET.Domain.Architecture LoadArchitecture()
    {
        // These typed anchors guarantee the core assemblies are copied to the test output
        // (project references whose types are touched). The full set — including the
        // Translators (which no longer expose a placeholder anchor) and the Doors (hosts,
        // which have no public anchor) — is then loaded from the output directory by file
        // name, so the rules see every Mach.* assembly regardless of its public surface.
        _ = typeof(Domain.Result);
        _ = typeof(Application.DependencyInjection);
        _ = typeof(Contracts.IntegrationEvent);
        _ = typeof(ServiceDefaults.MachJsonOptions);

        var assemblies = new List<ReflectionAssembly>();
        foreach (var path in Directory.EnumerateFiles(AppContext.BaseDirectory, "Mach.*.dll"))
        {
            var name = Path.GetFileNameWithoutExtension(path);
            if (name.EndsWith(".Tests", StringComparison.Ordinal))
            {
                continue;
            }

            try
            {
                assemblies.Add(ReflectionAssembly.LoadFrom(path));
            }
            catch (Exception ex) when (ex is BadImageFormatException or System.IO.FileLoadException)
            {
                // Skip unloadable images.
            }
        }

        return new ArchLoader().LoadAssemblies([.. assemblies.Distinct()]).Build();
    }

    private static IObjectProvider<IType> InNamespaceMatching(string pattern)
        => Types().That().ResideInNamespaceMatching(pattern);

    [Fact]
    public void Domain_DependsOnNothingInSolution()
    {
        Types().That().ResideInNamespaceMatching(DomainPattern)
            .Should().NotDependOnAny(InNamespaceMatching(ApplicationPattern))
            .AndShould().NotDependOnAny(InNamespaceMatching(ContractsPattern))
            .AndShould().NotDependOnAny(InNamespaceMatching(InfrastructurePattern))
            .AndShould().NotDependOnAny(InNamespaceMatching(ServiceDefaultsPattern))
            .AndShould().NotDependOnAny(InNamespaceMatching(HostPattern))
            .Because("the Domain is the pure core and must depend on nothing in-solution.")
            .Check(Arch);
    }

    [Fact]
    public void Application_DependsOnlyOnDomain()
    {
        Types().That().ResideInNamespaceMatching(ApplicationPattern)
            .Should().NotDependOnAny(InNamespaceMatching(ContractsPattern))
            .AndShould().NotDependOnAny(InNamespaceMatching(InfrastructurePattern))
            .AndShould().NotDependOnAny(InNamespaceMatching(ServiceDefaultsPattern))
            .AndShould().NotDependOnAny(InNamespaceMatching(HostPattern))
            .Because("the Application layer defines ports and may only depend on the Domain.")
            .Check(Arch);
    }

    [Fact]
    public void Infrastructure_DoesNotDependOnOtherTranslatorsOrHosts()
    {
        Types().That().ResideInNamespaceMatching(InfrastructurePattern)
            .Should().NotDependOnAny(InNamespaceMatching(HostPattern))
            .Because("Translators must not depend on the Doors (hosts).")
            .Check(Arch);
    }
}

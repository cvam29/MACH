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
        // Anchor types force the referenced src assemblies to load into the AppDomain.
        var anchors = new[]
        {
            typeof(Domain.Result).Assembly,
            typeof(Application.DependencyInjection).Assembly,
            typeof(Contracts.IntegrationEvent).Assembly,
            typeof(ServiceDefaults.MachJsonOptions).Assembly,
            typeof(Infrastructure.Commercetools.PlaceholderMarker).Assembly,
            typeof(Infrastructure.Contentstack.PlaceholderMarker).Assembly,
            typeof(Infrastructure.Algolia.PlaceholderMarker).Assembly,
            typeof(Infrastructure.Adyen.PlaceholderMarker).Assembly,
            typeof(Infrastructure.Email.PlaceholderMarker).Assembly,
            typeof(Infrastructure.Maps.PlaceholderMarker).Assembly,
            typeof(Infrastructure.Messaging.PlaceholderMarker).Assembly,
            typeof(Persistence.PlaceholderMarker).Assembly,
        };

        // Host (Door) assemblies have no public anchor type, so load them from the test
        // output directory by file name (they are copied in as project references).
        var assemblies = new List<ReflectionAssembly>(anchors);
        foreach (var path in Directory.EnumerateFiles(AppContext.BaseDirectory, "Mach.*.Functions.dll"))
        {
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

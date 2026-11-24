using System.Reflection;
using NetArchTest.Rules;
using SecureFile.Core.Files;
using SecureFile.Presentation.Main;
using ArchitectureTestResult = NetArchTest.Rules.TestResult;

namespace SecureFile.Architecture.Tests;

/// <summary>
/// Enforces the layer rule (PROJECT_PLAN §6): UI → Presentation → Core, never the other way, plus ADR-12 (no network).
/// If someone adds a forbidden reference, these tests fail and the build in CI turns red.
/// </summary>
/// <remarks>
/// The UI rules check the <b>assemblies</b> a layer references, not namespace prefixes: a ViewModel may legitimately use
/// <c>System.Windows.Input.ICommand</c>, which lives in the cross-platform System.ObjectModel assembly, not in WPF.
/// </remarks>
public sealed class LayerDependencyTests
{
    private static readonly Assembly CoreAssembly = typeof(FileDetails).Assembly;

    private static readonly Assembly PresentationAssembly = typeof(MainViewModel).Assembly;

    /// <summary>Assemblies that only a user-interface project may reference.</summary>
    private static readonly string[] UserInterfaceAssemblyPrefixes =
    [
        "PresentationFramework",   // WPF
        "PresentationCore",        // WPF
        "WindowsBase",             // WPF base (dispatcher, dependency properties)
        "System.Windows.Forms",    // WinForms
        "System.Xaml",             // XAML loading
        "Avalonia",                // cross-platform UI (bonus)
        "SecureFile.Wpf",
        "SecureFile.Avalonia",
    ];

    /// <summary>Assemblies that would allow network access (ADR-12: the app never uses the network).</summary>
    private static readonly string[] NetworkAssemblyPrefixes =
    [
        "System.Net.Http",
        "System.Net.Sockets",
        "System.Net.Requests",
        "System.Net.WebClient",
        "System.Net.NetworkInformation",
    ];

    [Fact]
    public void Core_DoesNotReferenceUserInterfacePresentationOrNetworkAssemblies()
    {
        string[] forbidden = [.. UserInterfaceAssemblyPrefixes, .. NetworkAssemblyPrefixes, "SecureFile.Presentation", "CommunityToolkit.Mvvm"];

        Assert.Empty(FindReferencesStartingWith(CoreAssembly, forbidden));
    }

    [Fact]
    public void Presentation_DoesNotReferenceUserInterfaceFrameworkOrNetworkAssemblies()
    {
        string[] forbidden = [.. UserInterfaceAssemblyPrefixes, .. NetworkAssemblyPrefixes];

        Assert.Empty(FindReferencesStartingWith(PresentationAssembly, forbidden));
    }

    [Fact]
    public void Core_DoesNotUseUserInterfaceNamespaces()
    {
        // Second line of defence at type level (catches use through type forwarding as well).
        ArchitectureTestResult result = Types.InAssembly(CoreAssembly)
            .ShouldNot().HaveDependencyOnAny("System.Windows.Controls", "System.Windows.Media", "System.Windows.Threading", "Avalonia")
            .GetResult();

        Assert.True(result.IsSuccessful, DescribeFailures(result));
    }

    [Fact]
    public void Core_NonStaticClasses_AreSealed()
    {
        // Design rule: Core classes are complete units, not base classes. Static classes are excluded by
        // AreNotAbstract() (the compiler marks them "abstract sealed") and cannot be inherited anyway.
        ArchitectureTestResult result = Types.InAssembly(CoreAssembly)
            .That().AreClasses().And().AreNotAbstract()
            .Should().BeSealed().GetResult();

        Assert.True(result.IsSuccessful, DescribeFailures(result));
    }

    [Fact]
    public void Core_ContainsTypes_SoTheRulesAboveAreMeaningful()
    {
        // Guards against rules that "pass" only because they inspected nothing.
        Assert.NotEmpty(CoreAssembly.GetTypes());
    }

    private static List<string> FindReferencesStartingWith(Assembly assembly, string[] forbiddenPrefixes)
    {
        return [.. assembly.GetReferencedAssemblies()
            .Select(reference => reference.Name ?? string.Empty)
            .Where(name => forbiddenPrefixes.Any(prefix => name.StartsWith(prefix, StringComparison.Ordinal)))];
    }

    private static string DescribeFailures(ArchitectureTestResult result)
    {
        IEnumerable<string> failingTypes = result.FailingTypeNames ?? [];
        return "Rule broken by: " + string.Join(", ", failingTypes);
    }
}

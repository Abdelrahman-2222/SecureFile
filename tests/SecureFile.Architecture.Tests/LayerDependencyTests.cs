using System.Reflection;
using NetArchTest.Rules;
using SecureFile.Core.Files;
using ArchitectureTestResult = NetArchTest.Rules.TestResult;

namespace SecureFile.Architecture.Tests;

/// <summary>
/// Enforces the layer rule (PROJECT_PLAN §6): UI → Presentation → Core, never the other way.
/// If someone adds a forbidden reference, these tests fail and the build in CI turns red.
/// </summary>
public sealed class LayerDependencyTests
{
    private static readonly Assembly CoreAssembly = typeof(FileDetails).Assembly;
    private static readonly Assembly PresentationAssembly = Assembly.Load("SecureFile.Presentation");

    /// <summary>Namespaces of UI frameworks and of our own higher layers.</summary>
    private static readonly string[] UserInterfaceNamespaces =
    [
        "System.Windows",          // WPF
        "System.Windows.Forms",    // WinForms
        "Avalonia",                // cross-platform UI (bonus)
        "SecureFile.Wpf",
        "SecureFile.Avalonia",
    ];

    [Fact]
    public void Core_DoesNotDependOnPresentationOrAnyUserInterface()
    {
        string[] forbidden = [.. UserInterfaceNamespaces, "SecureFile.Presentation", "CommunityToolkit.Mvvm"];

        ArchitectureTestResult result = Types.InAssembly(CoreAssembly).ShouldNot().HaveDependencyOnAny(forbidden).GetResult();

        Assert.True(result.IsSuccessful, DescribeFailures(result));
    }

    [Fact]
    public void Presentation_DoesNotDependOnAnyUserInterfaceFramework()
    {
        ArchitectureTestResult result = Types.InAssembly(PresentationAssembly)
            .ShouldNot().HaveDependencyOnAny(UserInterfaceNamespaces).GetResult();

        Assert.True(result.IsSuccessful, DescribeFailures(result));
    }

    [Fact]
    public void Core_ConcreteClasses_AreSealed()
    {
        // Design rule: Core classes are complete units, not base classes (static classes count as sealed).
        ArchitectureTestResult result = Types.InAssembly(CoreAssembly)
            .That().AreClasses().And().AreNotAbstract()
            .Should().BeSealed().GetResult();

        Assert.True(result.IsSuccessful, DescribeFailures(result));
    }

    private static string DescribeFailures(ArchitectureTestResult result)
    {
        IEnumerable<string> failingTypes = result.FailingTypeNames ?? [];
        return "Rule broken by: " + string.Join(", ", failingTypes);
    }
}

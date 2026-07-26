using System.Reflection;

using NexusLabs.Foundry.MicrosoftAgentFramework.Harness.Bundle;

namespace NexusLabs.Foundry.MicrosoftAgentFramework.Harness.Tests;

/// <summary>
/// Proves that the optional Harness bundle package is isolated from the core
/// <c>NexusLabs.Foundry.MicrosoftAgentFramework</c> package: the core package never references
/// <c>Microsoft.Agents.AI.Harness</c> or Needlr, this optional package references exactly the
/// bundle and core-integration dependencies it needs, and the public namespace boundary between
/// the two Harness surfaces (core's internal selected-provider lane versus this package's bundle
/// lane) is preserved.
/// </summary>
public sealed class HarnessPackageIsolationTests
{
    private const string HarnessAssemblyName = "Microsoft.Agents.AI.Harness";
    private const string CoreAssemblyName = "NexusLabs.Foundry.MicrosoftAgentFramework";
    private const string BundleAssemblyName = "NexusLabs.Foundry.MicrosoftAgentFramework.Harness";

    private static Assembly CoreAssembly => typeof(IAgentFactory).Assembly;

    private static Assembly BundleAssembly => typeof(FoundryHarnessAgentFactory).Assembly;

    private static string TestAssemblyDepsJsonPath =>
        Path.Combine(
            AppContext.BaseDirectory,
            "NexusLabs.Foundry.MicrosoftAgentFramework.Harness.Tests.deps.json");

    [Fact]
    public void CoreAssembly_HasExpectedSimpleName()
    {
        Assert.Equal(CoreAssemblyName, CoreAssembly.GetName().Name);
    }

    [Fact]
    public void BundleAssembly_HasExpectedSimpleName()
    {
        Assert.Equal(BundleAssemblyName, BundleAssembly.GetName().Name);
    }

    [Fact]
    public void CoreAssembly_DoesNotReferenceHarnessBundlePackage()
    {
        var referencedNames = GetReferencedSimpleNames(CoreAssembly);
        Assert.DoesNotContain(HarnessAssemblyName, referencedNames);
    }

    [Fact]
    public void CoreAssembly_DoesNotReferenceNeedlr()
    {
        var referencedNames = GetReferencedSimpleNames(CoreAssembly);
        Assert.DoesNotContain(referencedNames, name => name.Contains("Needlr", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void CoreProjectFile_HasNoHarnessBundlePackageReference()
    {
        var csprojText = ReadProjectFileText(CoreAssemblyName);
        Assert.DoesNotContain(HarnessAssemblyName, csprojText, StringComparison.Ordinal);
    }

    [Fact]
    public void CoreProjectFile_HasNoNeedlrReference()
    {
        var csprojText = ReadProjectFileText(CoreAssemblyName);
        Assert.DoesNotContain("Needlr", csprojText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BundleAssembly_ReferencesHarnessBundlePackage()
    {
        var referencedNames = GetReferencedSimpleNames(BundleAssembly);
        Assert.Contains(HarnessAssemblyName, referencedNames);
    }

    [Fact]
    public void BundleLibrary_DependsOnExactlyHarnessBundleAndCoreIntegration()
    {
        var closure = DependencyClosureReader.ReadDirectDependenciesByLibraryName(TestAssemblyDepsJsonPath);
        var bundleDependencies = closure[BundleAssemblyName];

        Assert.Equal(
            new HashSet<string>(StringComparer.Ordinal) { HarnessAssemblyName, CoreAssemblyName },
            bundleDependencies);
    }

    [Fact]
    public void CoreLibrary_HasNoHarnessOrNeedlrDependencyInDependencyClosure()
    {
        var closure = DependencyClosureReader.ReadDirectDependenciesByLibraryName(TestAssemblyDepsJsonPath);
        var coreDependencies = closure[CoreAssemblyName];

        Assert.DoesNotContain(
            coreDependencies,
            name => name.Contains("Harness", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(
            coreDependencies,
            name => name.Contains("Needlr", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void BundleAssembly_DoesNotReferenceNeedlr()
    {
        var referencedNames = GetReferencedSimpleNames(BundleAssembly);
        Assert.DoesNotContain(referencedNames, name => name.Contains("Needlr", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void BundlePublicTypes_LiveUnderTheBundleNamespace()
    {
        var publicTypes = BundleAssembly.GetExportedTypes();
        Assert.NotEmpty(publicTypes);
        Assert.All(
            publicTypes,
            type => Assert.StartsWith(
                "NexusLabs.Foundry.MicrosoftAgentFramework.Harness.Bundle",
                type.Namespace,
                StringComparison.Ordinal));
    }

    [Fact]
    public void BundleNamespace_DoesNotCollideWithCoreInternalHarnessNamespace()
    {
        var coreInternalHarnessTypeExists = CoreAssembly
            .GetTypes()
            .Any(type => type.Namespace == "NexusLabs.Foundry.MicrosoftAgentFramework.Harness");
        var bundlePublicTypeInSameNamespace = BundleAssembly
            .GetExportedTypes()
            .Any(type => type.Namespace == "NexusLabs.Foundry.MicrosoftAgentFramework.Harness");

        Assert.True(coreInternalHarnessTypeExists);
        Assert.False(bundlePublicTypeInSameNamespace);
    }

    private static IReadOnlyList<string> GetReferencedSimpleNames(Assembly assembly) =>
        assembly.GetReferencedAssemblies()
            .Select(name => name.Name ?? string.Empty)
            .ToList();

    private static string ReadProjectFileText(string projectSimpleName)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "NexusLabs.Foundry.slnx")))
        {
            directory = directory.Parent;
        }

        if (directory is null)
        {
            throw new InvalidOperationException(
                "Could not locate the 'src' directory (marked by NexusLabs.Foundry.slnx) by walking up " +
                $"from '{AppContext.BaseDirectory}'.");
        }

        var csprojPath = Path.Combine(directory.FullName, projectSimpleName, $"{projectSimpleName}.csproj");
        return File.ReadAllText(csprojPath);
    }
}

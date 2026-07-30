using System.Text.RegularExpressions;

using NexusLabs.Foundry.MicrosoftAgentFramework.Harness.Capabilities;

namespace NexusLabs.Foundry.MicrosoftAgentFramework.Tests.Harness;

/// <summary>
/// Proves FR-060: shell is a separate opt-in package and a manually composed
/// capability. No Foundry package depends on a shell package, no Foundry Harness
/// capability toggle exposes shell, and the documented composition path is the
/// ordinary tool and context-provider seam rather than a Harness options property.
/// </summary>
public sealed class HarnessShellCompositionTests
{
    [Fact]
    public void HarnessCapability_DoesNotExposeShellToggle()
    {
        var capabilityNames = Enum.GetNames<HarnessCapability>();

        Assert.DoesNotContain(
            capabilityNames,
            name => name.Contains("Shell", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void NoFoundryProject_ReferencesAShellPackage()
    {
        var offenders = new List<string>();
        foreach (var project in EnumerateProjectFiles())
        {
            foreach (var reference in EnumeratePackageReferenceNames(
                File.ReadAllText(project)))
            {
                if (IsShellPackage(reference))
                {
                    offenders.Add($"{Path.GetFileName(project)} -> {reference}");
                }
            }
        }

        Assert.Empty(offenders);
    }

    [Fact]
    public void CentralPackageManagement_PinsNoShellPackage()
    {
        var props = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "src",
            "Directory.Packages.props"));
        var pinned = Regex.Matches(
                props,
                "<PackageVersion\\s+Include=\"(?<name>[^\"]+)\"",
                RegexOptions.None,
                TimeSpan.FromSeconds(5))
            .Select(match => match.Groups["name"].Value)
            .Where(IsShellPackage)
            .ToArray();

        Assert.Empty(pinned);
    }

    [Fact]
    public void Documentation_RecordsAbsentHarnessOptionsShellProperty()
    {
        var documentation = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "docs",
            "maf-harness.md"));

        Assert.Contains("## Shell is a separate opt-in package", documentation);
        Assert.Contains("no shell property", documentation, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("HarnessAgentOptions", documentation);
    }

    private static bool IsShellPackage(string packageName) =>
        packageName.Contains("Shell", StringComparison.OrdinalIgnoreCase);

    private static IEnumerable<string> EnumeratePackageReferenceNames(string projectText) =>
        Regex.Matches(
                projectText,
                "<PackageReference\\s+Include=\"(?<name>[^\"]+)\"",
                RegexOptions.None,
                TimeSpan.FromSeconds(5))
            .Select(match => match.Groups["name"].Value);

    private static IEnumerable<string> EnumerateProjectFiles() =>
        Directory.EnumerateFiles(
            Path.Combine(FindRepositoryRoot(), "src"),
            "*.csproj",
            SearchOption.AllDirectories);

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "src", "NexusLabs.Foundry.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the Foundry repository root.");
    }
}

using System.Text.Json;

namespace NexusLabs.Foundry.MicrosoftAgentFramework.Harness.Tests;

/// <summary>
/// Reads the direct, per-library dependency edges recorded in a <c>.deps.json</c> build asset,
/// keyed by simple library name (the portion of the library key before the "/version" suffix).
/// </summary>
/// <remarks>
/// A <c>.deps.json</c> file records, for every project and package in the resolved closure, that
/// library's own immediate dependencies — not just the flattened transitive closure. This makes
/// it a reliable source of "does project X directly depend on project Y" evidence that does not
/// depend on whether the C# compiler happened to emit an <c>AssemblyRef</c> metadata entry (which
/// it omits when no type from the referenced assembly is actually used in code).
/// </remarks>
internal static class DependencyClosureReader
{
    internal static IReadOnlyDictionary<string, IReadOnlySet<string>> ReadDirectDependenciesByLibraryName(
        string depsJsonPath)
    {
        using var stream = File.OpenRead(depsJsonPath);
        using var document = JsonDocument.Parse(stream);

        var targets = document.RootElement.GetProperty("targets");
        var targetFramework = targets.EnumerateObject().Single();

        var result = new Dictionary<string, IReadOnlySet<string>>(StringComparer.Ordinal);
        foreach (var library in targetFramework.Value.EnumerateObject())
        {
            var libraryName = library.Name.Split('/')[0];
            var dependencyNames = new HashSet<string>(StringComparer.Ordinal);
            if (library.Value.TryGetProperty("dependencies", out var dependencies))
            {
                foreach (var dependency in dependencies.EnumerateObject())
                {
                    dependencyNames.Add(dependency.Name);
                }
            }

            result[libraryName] = dependencyNames;
        }

        return result;
    }
}

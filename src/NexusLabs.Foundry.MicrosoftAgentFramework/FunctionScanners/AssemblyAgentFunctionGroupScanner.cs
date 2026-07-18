using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace NexusLabs.Foundry.MicrosoftAgentFramework.FunctionScanners;
[RequiresUnreferencedCode("Assembly scanning uses reflection to discover types with [AgentFunctionGroup] attributes.")]
[RequiresDynamicCode("Assembly scanning uses reflection APIs that may require dynamic code generation.")]
internal sealed class AssemblyAgentFunctionGroupScanner(IReadOnlyList<Assembly> _assemblies)
{
    public IReadOnlyDictionary<string, IReadOnlyList<Type>> ScanForFunctionGroups()
    {
        var groups = new Dictionary<string, List<Type>>();

        foreach (var assembly in _assemblies.Where(a => !a.IsDynamic))
        {
            IEnumerable<Type> types;
            try { types = assembly.GetTypes(); }
            catch (ReflectionTypeLoadException ex) { types = ex.Types.Where(t => t is not null)!; }

            foreach (var type in types.Where(
                type => type.IsClass
                    && (!type.IsAbstract || IsStaticClass(type))))
            {
                foreach (AgentFunctionGroupAttribute attr in
                    type.GetCustomAttributes<AgentFunctionGroupAttribute>(inherit: false))
                {
                    if (!groups.TryGetValue(attr.GroupName, out var list))
                        groups[attr.GroupName] = list = [];

                    if (!list.Contains(type))
                        list.Add(type);
                }
            }
        }

        return groups.ToDictionary(
            k => k.Key,
            v => (IReadOnlyList<Type>)v.Value.AsReadOnly());
    }

    private static bool IsStaticClass(Type type) =>
        type.IsAbstract && type.IsSealed;
}

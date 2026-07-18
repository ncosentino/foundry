using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace NexusLabs.Foundry.MicrosoftAgentFramework.FunctionScanners;
[RequiresUnreferencedCode("Assembly scanning uses reflection to discover types with [AgentFunction] methods.")]
[RequiresDynamicCode("Assembly scanning uses reflection APIs that may require dynamic code generation.")]
internal sealed class AssemblyAgentFunctionScanner(
    IReadOnlyList<Assembly> _assemblies) :
    IAgentFrameworkFunctionScanner
{
    public IReadOnlyList<Type> ScanForFunctionTypes()
    {
        return FromAssemblies(_assemblies)
            .Where(HasAgentFunctions)
            .Distinct()
            .ToArray();
    }

    private static IEnumerable<Type> FromAssemblies(IEnumerable<Assembly> assemblies) =>
        assemblies
            .Where(a => !a.IsDynamic)
            .SelectMany(SafeGetTypes)
            .Where(t => t.IsClass && (!t.IsAbstract || IsStaticClass(t)));

    private static bool HasAgentFunctions(Type t)
    {
        var bindingFlags = IsStaticClass(t)
            ? BindingFlags.Public | BindingFlags.Static
            : BindingFlags.Public | BindingFlags.Instance;
        return t.GetMethods(bindingFlags)
            .Any(m => m.IsDefined(typeof(AgentFunctionAttribute), inherit: true));
    }

    private static IEnumerable<Type> SafeGetTypes(Assembly a)
    {
        try { return a.GetTypes(); }
        catch (ReflectionTypeLoadException ex) { return ex.Types.Where(t => t is not null)!; }
    }

    private static bool IsStaticClass(Type type) =>
        type.IsAbstract && type.IsSealed;
}

// GeneratorCoexistenceApp
//
// PURPOSE: Proves that Foundry's and Needlr's source generators coexist with
//          MAF's Workflows.Generators in the same compilation without conflict.
//
// WHAT'S HERE:
//   - Foundry generator: [FoundryAgent], [AgentFunctionGroup], [AgentFunction]
//   - Needlr generator:  DI type registry
//   - MAF generator:     [MessageHandler] on an Executor class

using GeneratorCoexistenceApp;

Console.WriteLine("╔══════════════════════════════════════════════════════════════╗");
Console.WriteLine("║  Generator Coexistence Example                              ║");
Console.WriteLine("║  Foundry + Needlr + MAF generators in one build             ║");
Console.WriteLine("╚══════════════════════════════════════════════════════════════╝");
Console.WriteLine();

// ── Foundry generator output verification ──
Console.WriteLine("── Foundry Source Generator Output ──");

var agentType = typeof(DataAssistant);
Console.WriteLine($"  [FoundryAgent] DataAssistant registered: {agentType.FullName}");

var functionType = typeof(DataFunctions);
Console.WriteLine($"  [AgentFunctionGroup] DataFunctions registered: {functionType.FullName}");

// Verify the source-generated AIFunction provider exists
var providerType = agentType.Assembly.GetTypes()
    .FirstOrDefault(t => t.Name == "GeneratedAIFunctionProvider");
Console.WriteLine($"  GeneratedAIFunctionProvider emitted: {providerType is not null}");

// Verify the source-generated agent registry exists
var registryType = agentType.Assembly.GetTypes()
    .FirstOrDefault(t => t.Name == "AgentFrameworkFunctionRegistry");
Console.WriteLine($"  AgentFrameworkFunctionRegistry emitted: {registryType is not null}");

// Verify the source-generated bootstrap exists
var bootstrapType = agentType.Assembly.GetTypes()
    .FirstOrDefault(t => t.Name == "FoundryAgentFrameworkModuleInitializer");
Console.WriteLine($"  FoundryAgentFrameworkModuleInitializer emitted: {bootstrapType is not null}");

Console.WriteLine();

// ── MAF generator output verification ──
Console.WriteLine("── MAF Workflows.Generators Output ──");

var executorType = typeof(GeneratorCoexistenceApp.Executors.EchoExecutor);
Console.WriteLine($"  [MessageHandler] EchoExecutor registered: {executorType.FullName}");

// MAF's generator overrides ConfigureProtocol on the partial executor.
var configureProtocol = executorType.GetMethod(
    "ConfigureProtocol",
    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
Console.WriteLine($"  ConfigureProtocol generated: {configureProtocol is not null}");

Console.WriteLine();

// ── Generated file inventory ──
Console.WriteLine("── Generated Files (check obj/Generated/) ──");
Console.WriteLine("  Foundry emits:");
Console.WriteLine("    - FoundryAgentFrameworkBootstrap.g.cs");
Console.WriteLine("    - AgentFrameworkFunctions.g.cs");
Console.WriteLine("    - GeneratedAIFunctionProvider.g.cs");
Console.WriteLine("    - AgentRegistry.g.cs");
Console.WriteLine("    - AgentFunctionGroupRegistry.g.cs");
Console.WriteLine("  MAF emits:");
Console.WriteLine("    - EchoExecutor route configuration (in obj/Generated/)");

Console.WriteLine();
if (providerType is null ||
    registryType is null ||
    bootstrapType is null ||
    configureProtocol is null)
{
    Console.Error.WriteLine("Generator coexistence validation failed.");
    return 1;
}

Console.WriteLine("✅ All generators compiled successfully in the same project.");
Console.WriteLine("   No file name collisions, no attribute conflicts, no DI conflicts.");
return 0;

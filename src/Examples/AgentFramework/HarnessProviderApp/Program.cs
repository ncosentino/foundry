using Microsoft.Extensions.Configuration;

using HarnessProviderApp;

// ============================================================================
// Harness Provider Example
//
// Runs one Foundry Harness agent against a configurable chat provider.
//
//   scripted (default) — deterministic, offline, no credentials, no cost.
//   copilot            — a real model through your local GitHub Copilot CLI
//                        credentials.
//
// Switch providers without editing code by creating appsettings.Development.json
// (already git-ignored) next to appsettings.json:
//
//   { "Harness": { "Provider": "copilot" } }
//
// or by setting an environment variable:
//
//   Harness__Provider=copilot dotnet run
// ============================================================================

var configuration = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: false)
    .AddJsonFile("appsettings.Development.json", optional: true)
    .AddEnvironmentVariables()
    .Build();

return await HarnessProviderRun.ExecuteAsync(configuration);

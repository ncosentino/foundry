namespace HarnessEvaluationApp;

internal sealed record HostedEvaluationOptions(
    string OutputDirectory,
    string ModelId,
    bool DryRun,
    ulong GlobalRunSeed,
    ulong BatchOrderingSeed,
    ulong ArmOrderingSeed,
    ulong BootstrapSeed,
    int MaximumAttempts,
    int MaximumRequests,
    int MaximumRequestsPerAttempt,
    int SchedulingDeadlineMinutes,
    int AttemptTimeoutSeconds,
    int MaximumConcurrency,
    decimal CostCapUsd,
    decimal EstimatedCostPerRequest)
{
    internal static HostedEvaluationOptions Load(string[] args)
    {
        var dryRun = args.Contains("--dry-run", StringComparer.Ordinal);
        var outputIndex = Array.IndexOf(args, "--output");
        var outputDirectory = outputIndex >= 0 && outputIndex + 1 < args.Length
            ? Path.GetFullPath(args[outputIndex + 1])
            : Path.GetFullPath("artifacts/eval/hosted-run");
        var modelId = Environment.GetEnvironmentVariable("HARNESS_EVAL_MODEL_ID")
            ?? Environment.GetEnvironmentVariable("MODEL_ID")
            ?? "openai/gpt-4.1-mini";
        return new HostedEvaluationOptions(
            outputDirectory,
            modelId,
            dryRun,
            ReadUInt64("HARNESS_EVAL_GLOBAL_SEED", 137),
            ReadUInt64("HARNESS_EVAL_BATCH_ORDERING_SEED", 104729),
            ReadUInt64("HARNESS_EVAL_ARM_ORDERING_SEED", 130363),
            ReadUInt64("HARNESS_EVAL_BOOTSTRAP_SEED", 155921),
            ReadInt32("HARNESS_EVAL_MAX_ATTEMPTS", 144),
            ReadInt32("HARNESS_EVAL_MAX_RESERVED_REQUESTS", 1152),
            ReadInt32("HARNESS_EVAL_MAX_REQUESTS_PER_ATTEMPT", 8),
            ReadInt32("HARNESS_EVAL_SCHEDULING_DEADLINE_MINUTES", 50),
            dryRun
                ? ReadInt32("HARNESS_EVAL_DRY_RUN_ATTEMPT_SECONDS", 2)
                : ReadInt32("HARNESS_EVAL_MAX_ATTEMPT_SECONDS", 120),
            ReadInt32("HARNESS_EVAL_MAX_CONCURRENCY", 3),
            ReadDecimal("HARNESS_EVAL_COST_CAP_USD", 25m),
            ReadDecimal("HARNESS_EVAL_ESTIMATED_USD_PER_REQUEST", 0.02m));
    }

    private static int ReadInt32(string name, int fallback) =>
        int.TryParse(
            Environment.GetEnvironmentVariable(name),
            System.Globalization.NumberStyles.Integer,
            System.Globalization.CultureInfo.InvariantCulture,
            out var value)
            ? value
            : fallback;

    private static ulong ReadUInt64(string name, ulong fallback) =>
        ulong.TryParse(
            Environment.GetEnvironmentVariable(name),
            System.Globalization.NumberStyles.Integer,
            System.Globalization.CultureInfo.InvariantCulture,
            out var value)
            ? value
            : fallback;

    private static decimal ReadDecimal(string name, decimal fallback) =>
        decimal.TryParse(
            Environment.GetEnvironmentVariable(name),
            System.Globalization.NumberStyles.Number,
            System.Globalization.CultureInfo.InvariantCulture,
            out var value)
            ? value
            : fallback;
}

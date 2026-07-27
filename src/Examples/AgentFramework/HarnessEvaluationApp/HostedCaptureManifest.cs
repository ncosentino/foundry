namespace HarnessEvaluationApp;

internal sealed record HostedCaptureManifest(
    string SchemaVersion,
    IReadOnlyList<string> AttemptDirectories,
    IReadOnlyList<string> ResponseFiles);

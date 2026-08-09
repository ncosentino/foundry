using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace AutonomousPhasePipelineApp.Core;

internal sealed class ReferenceArtifactStore
{
    private const string ArtifactIdPrefix = "artifact://sha256/";

    private readonly ConcurrentDictionary<string, string> _contents =
        new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, ReferenceArtifactManifest> _manifests =
        new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, byte>>
        _authorizedArtifacts = new(StringComparer.Ordinal);

    internal ReferenceArtifactReference Write(
        string runId,
        string content) =>
        Write(runId, content, "application/json");

    internal ReferenceArtifactReference Write(
        string runId,
        string content,
        string mediaType)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(runId);
        ArgumentException.ThrowIfNullOrWhiteSpace(content);
        byte[] bytes = Encoding.UTF8.GetBytes(content);
        string digest = Convert
            .ToHexString(SHA256.HashData(bytes))
            .ToLowerInvariant();
        string id = $"{ArtifactIdPrefix}{digest}";

        if (!_contents.TryAdd(id, content) &&
            !string.Equals(_contents[id], content, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Artifact '{id}' resolved to conflicting content.");
        }

        Authorize(runId, id);
        return new ReferenceArtifactReference(
            id,
            digest,
            bytes.Length,
            mediaType);
    }

    internal ReferenceArtifactReference WriteManifest(
        ReferenceArtifactManifest manifest)
    {
        string content = JsonSerializer.Serialize(manifest);
        ReferenceArtifactReference reference = Write(
            manifest.RunId,
            content);
        _manifests[reference.Id] = manifest;
        return reference;
    }

    internal string Read(
        string runId,
        string artifactId)
    {
        EnsureAuthorized(runId, artifactId);
        return ReadCore(artifactId);
    }

    internal ReferenceArtifactManifest GetManifest(
        ReferenceArtifactReference reference,
        string runId)
    {
        _ = Read(runId, reference.Id);
        ReferenceArtifactManifest manifest = _manifests.TryGetValue(
            reference.Id,
            out ReferenceArtifactManifest? storedManifest)
            ? storedManifest
            : throw new KeyNotFoundException(
                $"Manifest '{reference.Id}' was not found.");
        if (!string.Equals(
            manifest.RunId,
            runId,
            StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Manifest '{reference.Id}' belongs to a different run.");
        }

        return manifest;
    }

    internal string ReadManifestBundle(
        string runId,
        string manifestId)
    {
        EnsureAuthorized(runId, manifestId);
        if (!_manifests.TryGetValue(
            manifestId,
            out ReferenceArtifactManifest? manifest))
        {
            throw new KeyNotFoundException(
                $"Manifest '{manifestId}' was not found.");
        }

        if (!string.Equals(
            manifest.RunId,
            runId,
            StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Manifest '{manifestId}' belongs to a different run.");
        }

        var builder = new StringBuilder();
        builder.AppendLine(Read(runId, manifestId));
        builder.AppendLine("ARTIFACT_BODIES");
        builder.AppendLine(Read(runId, manifest.Research.Id));
        foreach (ReferencePhaseArtifact branch in manifest.Branches)
        {
            if (branch.Artifact is { } artifact)
            {
                builder.AppendLine(Read(runId, artifact.Id));
            }
        }

        return builder.ToString();
    }

    private string ReadCore(string artifactId)
    {
        if (!artifactId.StartsWith(
            ArtifactIdPrefix,
            StringComparison.Ordinal))
        {
            throw new ArgumentException(
                $"Artifact ID '{artifactId}' is not a content-addressed reference.",
                nameof(artifactId));
        }

        if (!_contents.TryGetValue(artifactId, out string? content))
        {
            throw new KeyNotFoundException(
                $"Artifact '{artifactId}' was not found.");
        }

        byte[] bytes = Encoding.UTF8.GetBytes(content);
        string actualDigest = Convert
            .ToHexString(SHA256.HashData(bytes))
            .ToLowerInvariant();
        string expectedDigest = artifactId[ArtifactIdPrefix.Length..];
        if (!string.Equals(
            actualDigest,
            expectedDigest,
            StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                $"Artifact '{artifactId}' failed digest validation.");
        }

        return content;
    }

    private void Authorize(
        string runId,
        string artifactId)
    {
        ConcurrentDictionary<string, byte> authorized =
            _authorizedArtifacts.GetOrAdd(
                runId,
                static _ => new ConcurrentDictionary<string, byte>(
                    StringComparer.Ordinal));
        authorized.TryAdd(artifactId, 0);
    }

    private void EnsureAuthorized(
        string runId,
        string artifactId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(runId);
        if (!_authorizedArtifacts.TryGetValue(
                runId,
                out ConcurrentDictionary<string, byte>? authorized) ||
            !authorized.ContainsKey(artifactId))
        {
            throw new UnauthorizedAccessException(
                $"Run '{runId}' is not authorized to read artifact '{artifactId}'.");
        }
    }
}

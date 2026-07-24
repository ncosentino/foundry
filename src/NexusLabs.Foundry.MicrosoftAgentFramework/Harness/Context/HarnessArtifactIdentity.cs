using System.Security.Cryptography;
using System.Text;

using NexusLabs.Foundry.MicrosoftAgentFramework.Workspace;

namespace NexusLabs.Foundry.MicrosoftAgentFramework.Harness.Context;

/// <summary>
/// Single shared utility for the deterministic digest and content-addressed workspace path used
/// by every G4 workspace artifact (<c>specs/001-maf-harness-first-class/evidence/harness-lifecycle-feasibility.md</c>,
/// T041, Decision 4 and "Content-addressed path format"). Every offloaded artifact's identity is a
/// pure function of the exact UTF-8 bytes of its serialized content — the same string the
/// byte-threshold offload decision was measured against — so retrying a write or reconstructing a
/// reference from recorded metadata always reproduces the identical digest and path.
/// </summary>
internal static class HarnessArtifactIdentity
{
    /// <summary>
    /// The default workspace-relative root segment artifacts are sharded under, matching the T041
    /// evidence's illustrative sharded form (<c>.foundry/artifacts/&lt;2 hex&gt;/&lt;2 hex&gt;/&lt;64-char hex digest&gt;</c>).
    /// </summary>
    internal const string DefaultArtifactRoot = ".foundry/artifacts";

    /// <summary>The fixed length, in hex characters, of a SHA-256 digest.</summary>
    internal const int DigestHexLength = 64;

    /// <summary>
    /// Computes the lowercase hex SHA-256 digest of the UTF-8 bytes of <paramref name="content"/>.
    /// </summary>
    internal static string ComputeDigest(string content)
    {
        ArgumentNullException.ThrowIfNull(content);

        var bytes = Encoding.UTF8.GetBytes(content);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    /// <summary>Computes the UTF-8 byte length of <paramref name="content"/>.</summary>
    internal static int ComputeUtf8ByteLength(string content)
    {
        ArgumentNullException.ThrowIfNull(content);
        return Encoding.UTF8.GetByteCount(content);
    }

    /// <summary>
    /// Returns <see langword="true"/> only when <paramref name="digest"/> is exactly
    /// <see cref="DigestHexLength"/> lowercase hex characters. Uppercase hex, short/long strings,
    /// and non-hex characters are all rejected — a digest is only ever compared or used to derive a
    /// path in this exact canonical lowercase form.
    /// </summary>
    internal static bool IsWellFormedDigest(string? digest)
    {
        if (string.IsNullOrEmpty(digest) || digest.Length != DigestHexLength)
        {
            return false;
        }

        foreach (var c in digest)
        {
            var isLowercaseHexDigit = (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f');
            if (!isLowercaseHexDigit)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Builds the deterministic, sharded, content-addressed workspace path for
    /// <paramref name="digest"/> under the fixed
    /// <see cref="DefaultArtifactRoot"/>:
    /// <c>{DefaultArtifactRoot}/{digest[..2]}/{digest[2..4]}/{digest}</c>.
    /// Sharding two levels deep keeps any single directory listing bounded, matching the
    /// <c>IWorkspace.GetFilePaths()</c> flat-enumeration cost concern recorded by T041 Decision 5.
    /// </summary>
    /// <exception cref="ArgumentException">
    /// <paramref name="digest"/> is not a well-formed digest per <see cref="IsWellFormedDigest"/>.
    /// </exception>
    internal static string BuildPath(string digest)
    {
        if (!IsWellFormedDigest(digest))
        {
            throw new ArgumentException(
                $"'{digest}' is not a well-formed lowercase {DigestHexLength}-character hex SHA-256 digest.",
                nameof(digest));
        }

        var shardA = digest[..2];
        var shardB = digest[2..4];
        var path = $"{DefaultArtifactRoot}/{shardA}/{shardB}/{digest}";

        // Defensive: guarantee the path this utility hands back is already the canonical form every
        // other workspace-facing type expects, so a caller never has to re-canonicalize it.
        return WorkspacePath.Canonicalize(path);
    }

    /// <summary>
    /// Builds the stable, bounded, LLM/reference-facing identity string for an artifact with the
    /// given <paramref name="digest"/>: <c>artifact://sha256/{digest}</c>. Deterministic — identical
    /// content always produces the identical reference identity.
    /// </summary>
    /// <exception cref="ArgumentException">
    /// <paramref name="digest"/> is not a well-formed digest per <see cref="IsWellFormedDigest"/>.
    /// </exception>
    internal static string BuildReferenceId(string digest)
    {
        if (!IsWellFormedDigest(digest))
        {
            throw new ArgumentException(
                $"'{digest}' is not a well-formed lowercase {DigestHexLength}-character hex SHA-256 digest.",
                nameof(digest));
        }

        return $"artifact://sha256/{digest}";
    }
}

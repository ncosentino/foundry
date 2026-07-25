// Tests intentionally exercise the offload transform's and rehydration mechanism's explicit
// CancellationToken parameter (CancellationToken.None) directly. This is the behavior under test,
// not an oversight of TestContext.Current.CancellationToken.
#pragma warning disable xUnit1051

using System.Reflection;

using NexusLabs.Foundry.MicrosoftAgentFramework.Context;
using NexusLabs.Foundry.MicrosoftAgentFramework.Diagnostics;
using NexusLabs.Foundry.MicrosoftAgentFramework.Harness.Context;
using NexusLabs.Foundry.MicrosoftAgentFramework.Progress;
using NexusLabs.Foundry.MicrosoftAgentFramework.Tools;
using NexusLabs.Foundry.MicrosoftAgentFramework.Workspace;

namespace NexusLabs.Foundry.MicrosoftAgentFramework.Tests.Harness;

/// <summary>
/// Focused tests for the privacy-safe artifact offload/rehydration diagnostics
/// (<see cref="HarnessArtifactDiagnostics"/>) and their corresponding progress events
/// (<see cref="HarnessArtifactOffloadDecisionEvent"/>, <see cref="HarnessArtifactRehydrationDecisionEvent"/>):
/// exactly-once emission per decision, correlation/sequence, categorical outcome/reason mapping,
/// observed/threshold byte values, bounded reference identity, safety with no accessor/no active
/// scope, and the complete absence of raw/sensitive data from every diagnostic and progress-event
/// string property.
/// </summary>
public sealed class HarnessArtifactObservabilityTests
{
    private static readonly DateTimeOffset CreatedAtUtc = new(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset RehydratedAtUtc = new(2025, 1, 1, 0, 5, 0, TimeSpan.Zero);

    // ================================================================================
    // Offload seam
    // ================================================================================

    [Fact]
    public void Transform_InlineDecision_EmitsExactlyOneOffloadDecisionEvent_MatchingOutcomeDiagnostics()
    {
        var (accessor, reporter, events) = CreateProgressHarness();
        using var fixture = HarnessArtifactTestFixture.Create();
        var content = new string('a', 50);
        var policy = CreatePolicy(fixture, maximumInlineToolResultBytes: 100);

        HarnessToolResultOffloadOutcome outcome;
        using (accessor.BeginScope(reporter))
        {
            var request = CreateOffloadRequest(fixture, content, policy, accessor);
            outcome = HarnessToolResultOffloadTransform.Transform(request);
        }

        Assert.Equal(HarnessToolResultOffloadStatus.Inline, outcome.Status);
        var evt = Assert.Single(events.OfType<HarnessArtifactOffloadDecisionEvent>());
        Assert.Same(outcome.Diagnostics, evt.Diagnostics);
        Assert.Equal(HarnessArtifactOperationCategory.Offload, evt.Diagnostics.Operation);
        Assert.Equal(HarnessArtifactOutcomeCategory.Inline, evt.Diagnostics.Outcome);
        Assert.Equal(HarnessArtifactContentCategory.ToolResult, evt.Diagnostics.Content);
        Assert.Equal(HarnessArtifactDecisionReason.BelowThreshold, evt.Diagnostics.Reason);
        Assert.Equal(50, evt.Diagnostics.ObservedUtf8ByteSize);
        Assert.Equal(100, evt.Diagnostics.ConfiguredThresholdOrBudget);
        Assert.Null(evt.Diagnostics.ReferenceId);
        Assert.Equal(reporter.WorkflowId, evt.WorkflowId);
        Assert.Null(evt.ParentAgentId);
        Assert.Equal(0, evt.Depth);
        Assert.Equal(HarnessArtifactOperationCategory.Offload, evt.Diagnostics.Attribution.Operation);
        Assert.Equal(50, evt.Diagnostics.Attribution.InputUtf8Bytes);
        Assert.Equal(50, evt.Diagnostics.Attribution.OutputUtf8Bytes);
    }

    [Fact]
    public void Transform_OffloadedDecision_EmitsExactlyOneOffloadDecisionEvent_WithReferenceIdAndThresholdExceededReason()
    {
        var (accessor, reporter, events) = CreateProgressHarness();
        using var fixture = HarnessArtifactTestFixture.Create();
        var content = new string('b', 101);
        var policy = CreatePolicy(fixture, maximumInlineToolResultBytes: 100);

        HarnessToolResultOffloadOutcome outcome;
        using (accessor.BeginScope(reporter))
        {
            var request = CreateOffloadRequest(fixture, content, policy, accessor);
            outcome = HarnessToolResultOffloadTransform.Transform(request);
        }

        Assert.Equal(HarnessToolResultOffloadStatus.Offloaded, outcome.Status);
        var evt = Assert.Single(events.OfType<HarnessArtifactOffloadDecisionEvent>());
        Assert.Same(outcome.Diagnostics, evt.Diagnostics);
        Assert.Equal(HarnessArtifactOutcomeCategory.Offloaded, evt.Diagnostics.Outcome);
        Assert.Equal(HarnessArtifactDecisionReason.ThresholdExceeded, evt.Diagnostics.Reason);
        Assert.Equal(101, evt.Diagnostics.ObservedUtf8ByteSize);
        Assert.Equal(100, evt.Diagnostics.ConfiguredThresholdOrBudget);
        Assert.Equal(outcome.ReferenceText, evt.Diagnostics.ReferenceId);
        Assert.StartsWith("artifact://sha256/", evt.Diagnostics.ReferenceId);
        Assert.Equal(HarnessArtifactOperationCategory.Offload, evt.Diagnostics.Attribution.Operation);
        Assert.Equal(101, evt.Diagnostics.Attribution.InputUtf8Bytes);
        Assert.Equal(
            HarnessArtifactIdentity.ComputeUtf8ByteLength(outcome.ReferenceText!),
            evt.Diagnostics.Attribution.OutputUtf8Bytes);
    }

    [Fact]
    public void Transform_ExistingReferenceDecision_EmitsExactlyOneOffloadDecisionEvent_WithExistingContentMatchReason()
    {
        var (accessor, reporter, events) = CreateProgressHarness();
        using var fixture = HarnessArtifactTestFixture.Create();
        var content = new string('c', 101);
        var policy = CreatePolicy(fixture, maximumInlineToolResultBytes: 100);

        // First call (outside any active scope) persists the artifact without emitting into
        // `events`, so the assertions below observe exactly one event for the decision under test.
        var firstRequest = CreateOffloadRequest(fixture, content, policy, progressAccessor: null);
        var firstOutcome = HarnessToolResultOffloadTransform.Transform(firstRequest);
        Assert.Equal(HarnessToolResultOffloadStatus.Offloaded, firstOutcome.Status);

        HarnessToolResultOffloadOutcome outcome;
        using (accessor.BeginScope(reporter))
        {
            var request = CreateOffloadRequest(fixture, content, policy, accessor);
            outcome = HarnessToolResultOffloadTransform.Transform(request);
        }

        Assert.Equal(HarnessToolResultOffloadStatus.ExistingReference, outcome.Status);
        var evt = Assert.Single(events.OfType<HarnessArtifactOffloadDecisionEvent>());
        Assert.Same(outcome.Diagnostics, evt.Diagnostics);
        Assert.Equal(HarnessArtifactOutcomeCategory.ExistingReference, evt.Diagnostics.Outcome);
        Assert.Equal(HarnessArtifactDecisionReason.ExistingContentMatch, evt.Diagnostics.Reason);
        Assert.Equal(101, evt.Diagnostics.ObservedUtf8ByteSize);
        Assert.Equal(outcome.ReferenceText, evt.Diagnostics.ReferenceId);
        Assert.Equal(HarnessArtifactOperationCategory.Offload, evt.Diagnostics.Attribution.Operation);
        Assert.Equal(101, evt.Diagnostics.Attribution.InputUtf8Bytes);
        Assert.Equal(
            HarnessArtifactIdentity.ComputeUtf8ByteLength(outcome.ReferenceText!),
            evt.Diagnostics.Attribution.OutputUtf8Bytes);
    }

    [Fact]
    public void Transform_FailedDecision_NoAuthorizedWorkspace_EmitsExactlyOneOffloadDecisionEvent_WithNoReferenceId()
    {
        var (accessor, reporter, events) = CreateProgressHarness();
        var policy = HarnessToolResultOffloadPolicy.Create(
            10,
            "no-binding-session",
            HarnessToolResultOffloadDescriptions.Default,
            checkpoint: null);
        var oversized = new string('d', 50);

        HarnessToolResultOffloadOutcome outcome;
        using (accessor.BeginScope(reporter))
        {
            var request = new HarnessToolResultOffloadRequest(
                oversized,
                "tool",
                "call-1",
                ExecutionBinding: null,
                ExecutionContextAccessor: null,
                policy,
                CreatedAtUtc,
                CancellationToken.None,
                accessor);
            outcome = HarnessToolResultOffloadTransform.Transform(request);
        }

        Assert.Equal(HarnessToolResultOffloadStatus.Failed, outcome.Status);
        var evt = Assert.Single(events.OfType<HarnessArtifactOffloadDecisionEvent>());
        Assert.Same(outcome.Diagnostics, evt.Diagnostics);
        Assert.Equal(HarnessArtifactOutcomeCategory.Failed, evt.Diagnostics.Outcome);
        Assert.Equal(HarnessArtifactDecisionReason.NoAuthorizedWorkspace, evt.Diagnostics.Reason);
        Assert.Equal(50, evt.Diagnostics.ObservedUtf8ByteSize);
        Assert.Equal(10, evt.Diagnostics.ConfiguredThresholdOrBudget);
        Assert.Null(evt.Diagnostics.ReferenceId);
        Assert.Equal(50, evt.Diagnostics.Attribution.InputUtf8Bytes);
        Assert.Null(evt.Diagnostics.Attribution.OutputUtf8Bytes);
    }

    [Fact]
    public void Transform_RecoveryRequiredDecision_EmitsExactlyOneOffloadDecisionEvent_WithCanceledAfterWriteReason()
    {
        var (accessor, reporter, events) = CreateProgressHarness();
        var innerWorkspace = new InMemoryWorkspace();
        var fakeWorkspace = new FakeWorkspace(innerWorkspace);
        using var fixture = HarnessArtifactTestFixture.Create(fakeWorkspace);
        var content = new string('e', 500);

        using var cts = new CancellationTokenSource();
        fakeWorkspace.WriteFileOverride = (path, text) =>
        {
            var result = innerWorkspace.TryWriteFile(path, text);
            cts.Cancel();
            return result;
        };

        var policy = CreatePolicy(fixture, maximumInlineToolResultBytes: 10);

        HarnessToolResultOffloadOutcome outcome;
        using (accessor.BeginScope(reporter))
        {
            var request = new HarnessToolResultOffloadRequest(
                content,
                HarnessArtifactTestFixture.DefaultToolName,
                HarnessArtifactTestFixture.DefaultCallId,
                fixture.Binding,
                fixture.Accessor,
                policy,
                CreatedAtUtc,
                cts.Token,
                accessor);
            outcome = HarnessToolResultOffloadTransform.Transform(request);
        }

        Assert.Equal(HarnessToolResultOffloadStatus.RecoveryRequired, outcome.Status);
        var evt = Assert.Single(events.OfType<HarnessArtifactOffloadDecisionEvent>());
        Assert.Same(outcome.Diagnostics, evt.Diagnostics);
        Assert.Equal(HarnessArtifactOutcomeCategory.RecoveryRequired, evt.Diagnostics.Outcome);
        Assert.Equal(HarnessArtifactDecisionReason.CanceledAfterWrite, evt.Diagnostics.Reason);
        Assert.Equal(500, evt.Diagnostics.ObservedUtf8ByteSize);
        Assert.Null(evt.Diagnostics.ReferenceId);
        Assert.Equal(500, evt.Diagnostics.Attribution.InputUtf8Bytes);
        Assert.Null(evt.Diagnostics.Attribution.OutputUtf8Bytes);
    }

    // ================================================================================
    // Rehydration seam
    // ================================================================================

    [Fact]
    public void Rehydrate_ResolvedDecision_EmitsExactlyOneRehydrationDecisionEvent_WithDigestVerifiedReason()
    {
        var (accessor, reporter, events) = CreateProgressHarness();
        using var fixture = HarnessArtifactTestFixture.Create(new FakeWorkspace(), accessor);
        const string content = "the exact body that must come back byte-for-byte unchanged";
        var reference = fixture.CreateReference(content, CreatedAtUtc);
        fixture.Workspace.TryWriteFile(reference.WorkspacePath, content);
        var request = HarnessArtifactRehydrationRequest.Create(
            reference, HarnessArtifactRehydrationRequestSource.ToolRequest, 1_000_000);

        HarnessArtifactRehydrationResult result;
        using (accessor.BeginScope(reporter))
        {
            result = fixture.Rehydration.Rehydrate(request, RehydratedAtUtc, CancellationToken.None);
        }

        Assert.Equal(HarnessArtifactResolutionStatus.Resolved, result.Status);
        var evt = Assert.Single(events.OfType<HarnessArtifactRehydrationDecisionEvent>());
        Assert.Same(result.Diagnostics, evt.Diagnostics);
        Assert.Equal(HarnessArtifactOperationCategory.Rehydration, evt.Diagnostics.Operation);
        Assert.Equal(HarnessArtifactOutcomeCategory.Resolved, evt.Diagnostics.Outcome);
        Assert.Equal(HarnessArtifactDecisionReason.DigestVerified, evt.Diagnostics.Reason);
        Assert.Equal(HarnessArtifactContentCategory.RecoverableContextSegment, evt.Diagnostics.Content);
        Assert.Equal(HarnessArtifactIdentity.ComputeUtf8ByteLength(content), evt.Diagnostics.ObservedUtf8ByteSize);
        Assert.Equal(1_000_000, evt.Diagnostics.ConfiguredThresholdOrBudget);
        Assert.Equal(reference.ReferenceId, evt.Diagnostics.ReferenceId);
        Assert.Equal(HarnessArtifactOperationCategory.Rehydration, evt.Diagnostics.Attribution.Operation);
        Assert.Equal(
            HarnessArtifactIdentity.ComputeUtf8ByteLength(reference.ReferenceId),
            evt.Diagnostics.Attribution.InputUtf8Bytes);
        Assert.Equal(
            HarnessArtifactIdentity.ComputeUtf8ByteLength(content),
            evt.Diagnostics.Attribution.OutputUtf8Bytes);
    }

    [Fact]
    public void Rehydrate_StaleDecision_EmitsExactlyOneRehydrationDecisionEvent_WithDigestMismatchReason()
    {
        var (accessor, reporter, events) = CreateProgressHarness();
        using var fixture = HarnessArtifactTestFixture.Create(new FakeWorkspace(), accessor);
        const string originalContent = "original content this reference's digest was computed from";
        var reference = fixture.CreateReference(originalContent, CreatedAtUtc);
        fixture.Workspace.TryWriteFile(reference.WorkspacePath, originalContent);
        fixture.Workspace.TryWriteFile(reference.WorkspacePath, "mutated content with a different digest entirely");
        var request = HarnessArtifactRehydrationRequest.Create(
            reference, HarnessArtifactRehydrationRequestSource.ToolRequest, 1_000_000);

        HarnessArtifactRehydrationResult result;
        using (accessor.BeginScope(reporter))
        {
            result = fixture.Rehydration.Rehydrate(request, RehydratedAtUtc, CancellationToken.None);
        }

        Assert.Equal(HarnessArtifactResolutionStatus.Stale, result.Status);
        var evt = Assert.Single(events.OfType<HarnessArtifactRehydrationDecisionEvent>());
        Assert.Same(result.Diagnostics, evt.Diagnostics);
        Assert.Equal(HarnessArtifactOutcomeCategory.Stale, evt.Diagnostics.Outcome);
        Assert.Equal(HarnessArtifactDecisionReason.DigestMismatch, evt.Diagnostics.Reason);
        Assert.NotNull(evt.Diagnostics.ObservedUtf8ByteSize);
        Assert.Equal(reference.ReferenceId, evt.Diagnostics.ReferenceId);
        Assert.Null(evt.Diagnostics.Attribution.OutputUtf8Bytes);
        Assert.Equal(
            HarnessArtifactIdentity.ComputeUtf8ByteLength(reference.ReferenceId),
            evt.Diagnostics.Attribution.InputUtf8Bytes);
    }

    [Fact]
    public void Rehydrate_MissingDecision_EmitsExactlyOneRehydrationDecisionEvent_WithMissingReasonAndNoObservedBytes()
    {
        var (accessor, reporter, events) = CreateProgressHarness();
        using var fixture = HarnessArtifactTestFixture.Create(new FakeWorkspace(), accessor);
        var reference = fixture.CreateReference("content that was never actually persisted", CreatedAtUtc);
        var request = HarnessArtifactRehydrationRequest.Create(
            reference, HarnessArtifactRehydrationRequestSource.ToolRequest, 1_000_000);

        HarnessArtifactRehydrationResult result;
        using (accessor.BeginScope(reporter))
        {
            result = fixture.Rehydration.Rehydrate(request, RehydratedAtUtc, CancellationToken.None);
        }

        Assert.Equal(HarnessArtifactResolutionStatus.Missing, result.Status);
        var evt = Assert.Single(events.OfType<HarnessArtifactRehydrationDecisionEvent>());
        Assert.Same(result.Diagnostics, evt.Diagnostics);
        Assert.Equal(HarnessArtifactOutcomeCategory.Missing, evt.Diagnostics.Outcome);
        Assert.Equal(HarnessArtifactDecisionReason.Missing, evt.Diagnostics.Reason);
        Assert.Null(evt.Diagnostics.ObservedUtf8ByteSize);
        Assert.Equal(reference.ReferenceId, evt.Diagnostics.ReferenceId);
        Assert.Null(evt.Diagnostics.Attribution.OutputUtf8Bytes);
    }

    [Fact]
    public void Rehydrate_UnauthorizedDecision_EmitsExactlyOneRehydrationDecisionEvent_WithOwnerMismatchReasonAndNoObservedBytes()
    {
        var (accessor, reporter, events) = CreateProgressHarness();
        using var fixture = HarnessArtifactTestFixture.Create(new FakeWorkspace(), accessor);
        const string content = "content whose reference records a foreign owner identity";
        var foreignReference = fixture.CreateForeignOwnedReference(content, CreatedAtUtc);
        fixture.Workspace.TryWriteFile(foreignReference.WorkspacePath, content);
        var request = HarnessArtifactRehydrationRequest.Create(
            foreignReference, HarnessArtifactRehydrationRequestSource.ToolRequest, 1_000_000);

        HarnessArtifactRehydrationResult result;
        using (accessor.BeginScope(reporter))
        {
            result = fixture.Rehydration.Rehydrate(request, RehydratedAtUtc, CancellationToken.None);
        }

        Assert.Equal(HarnessArtifactResolutionStatus.Unauthorized, result.Status);
        var evt = Assert.Single(events.OfType<HarnessArtifactRehydrationDecisionEvent>());
        Assert.Same(result.Diagnostics, evt.Diagnostics);
        Assert.Equal(HarnessArtifactOutcomeCategory.Unauthorized, evt.Diagnostics.Outcome);
        Assert.Equal(HarnessArtifactDecisionReason.OwnerMismatch, evt.Diagnostics.Reason);
        Assert.Null(evt.Diagnostics.ObservedUtf8ByteSize);
        Assert.Equal(foreignReference.ReferenceId, evt.Diagnostics.ReferenceId);
        Assert.Null(evt.Diagnostics.Attribution.OutputUtf8Bytes);
    }

    [Fact]
    public void Rehydrate_OverBudgetDecision_EmitsExactlyOneRehydrationDecisionEvent_WithBudgetExceededReason()
    {
        var (accessor, reporter, events) = CreateProgressHarness();
        using var fixture = HarnessArtifactTestFixture.Create(new FakeWorkspace(), accessor);
        var content = new string('x', 100);
        var reference = fixture.CreateReference(content, CreatedAtUtc);
        fixture.Workspace.TryWriteFile(reference.WorkspacePath, content);
        var request = HarnessArtifactRehydrationRequest.Create(
            reference, HarnessArtifactRehydrationRequestSource.ToolRequest, maximumRehydratedUtf8Bytes: 10);

        HarnessArtifactRehydrationResult result;
        using (accessor.BeginScope(reporter))
        {
            result = fixture.Rehydration.Rehydrate(request, RehydratedAtUtc, CancellationToken.None);
        }

        Assert.Equal(HarnessArtifactResolutionStatus.OverBudget, result.Status);
        var evt = Assert.Single(events.OfType<HarnessArtifactRehydrationDecisionEvent>());
        Assert.Same(result.Diagnostics, evt.Diagnostics);
        Assert.Equal(HarnessArtifactOutcomeCategory.OverBudget, evt.Diagnostics.Outcome);
        Assert.Equal(HarnessArtifactDecisionReason.BudgetExceeded, evt.Diagnostics.Reason);
        Assert.Equal(100, evt.Diagnostics.ObservedUtf8ByteSize);
        Assert.Equal(10, evt.Diagnostics.ConfiguredThresholdOrBudget);
        Assert.Null(evt.Diagnostics.Attribution.OutputUtf8Bytes);
    }

    // ================================================================================
    // Attribution: multibyte payload/reference byte counting
    // ================================================================================

    [Fact]
    public void OffloadAttribution_MultibyteContent_ReportsActualUtf8ByteCountNotCharCount()
    {
        var (accessor, reporter, events) = CreateProgressHarness();
        using var fixture = HarnessArtifactTestFixture.Create();

        // Each repeated character is a 3-byte UTF-8 sequence (U+65E5, "日"), so the UTF-8 byte size
        // is exactly 3x the .NET char count — never equal to it — proving byte counting is actually
        // UTF-8-based rather than a mistaken char-count or Length substitute.
        var content = new string('\u65e5', 40);
        Assert.Equal(120, System.Text.Encoding.UTF8.GetByteCount(content));
        var policy = CreatePolicy(fixture, maximumInlineToolResultBytes: 100);

        HarnessToolResultOffloadOutcome outcome;
        using (accessor.BeginScope(reporter))
        {
            outcome = HarnessToolResultOffloadTransform.Transform(
                CreateOffloadRequest(fixture, content, policy, accessor));
        }

        Assert.Equal(HarnessToolResultOffloadStatus.Offloaded, outcome.Status);
        var evt = Assert.Single(events.OfType<HarnessArtifactOffloadDecisionEvent>());
        Assert.Equal(120, evt.Diagnostics.ObservedUtf8ByteSize);
        Assert.Equal(120, evt.Diagnostics.Attribution.InputUtf8Bytes);
        Assert.Equal(
            HarnessArtifactIdentity.ComputeUtf8ByteLength(outcome.ReferenceText!),
            evt.Diagnostics.Attribution.OutputUtf8Bytes);
    }

    [Fact]
    public void RehydrationAttribution_MultibyteResolvedBody_ReportsActualUtf8ByteCountNotCharCount()
    {
        var (accessor, reporter, events) = CreateProgressHarness();
        using var fixture = HarnessArtifactTestFixture.Create(new FakeWorkspace(), accessor);

        // A mix of single-byte and multi-byte (2-byte "é", 4-byte emoji) UTF-8 sequences: the
        // resolved-body byte count must reflect the true encoded size, never the .NET char/UTF-16
        // code-unit count.
        const string content = "café \ud83d\ude00 test";
        var expectedBytes = System.Text.Encoding.UTF8.GetByteCount(content);
        var reference = fixture.CreateReference(content, CreatedAtUtc);
        fixture.Workspace.TryWriteFile(reference.WorkspacePath, content);
        var request = HarnessArtifactRehydrationRequest.Create(
            reference, HarnessArtifactRehydrationRequestSource.ToolRequest, 1_000_000);

        HarnessArtifactRehydrationResult result;
        using (accessor.BeginScope(reporter))
        {
            result = fixture.Rehydration.Rehydrate(request, RehydratedAtUtc, CancellationToken.None);
        }

        Assert.Equal(HarnessArtifactResolutionStatus.Resolved, result.Status);
        var evt = Assert.Single(events.OfType<HarnessArtifactRehydrationDecisionEvent>());
        Assert.Equal(expectedBytes, evt.Diagnostics.ObservedUtf8ByteSize);
        Assert.Equal(expectedBytes, evt.Diagnostics.Attribution.OutputUtf8Bytes);
        Assert.Equal(
            HarnessArtifactIdentity.ComputeUtf8ByteLength(reference.ReferenceId),
            evt.Diagnostics.Attribution.InputUtf8Bytes);
    }

    // ================================================================================
    // HarnessArtifactDiagnostics factory validation: canonical reference-id enforcement
    // ================================================================================

    [Fact]
    public void ForOffload_OffloadedOutcome_WithNullReferenceId_Throws()
    {
        Assert.Throws<ArgumentException>(() => HarnessArtifactDiagnostics.ForOffload(
            HarnessArtifactOutcomeCategory.Offloaded,
            HarnessArtifactContentCategory.ToolResult,
            HarnessArtifactDecisionReason.ThresholdExceeded,
            observedUtf8ByteSize: 200,
            configuredThresholdBytes: 100,
            referenceId: null));
    }

    [Fact]
    public void ForOffload_ExistingReferenceOutcome_WithMalformedReferenceId_Throws()
    {
        Assert.Throws<ArgumentException>(() => HarnessArtifactDiagnostics.ForOffload(
            HarnessArtifactOutcomeCategory.ExistingReference,
            HarnessArtifactContentCategory.ToolResult,
            HarnessArtifactDecisionReason.ExistingContentMatch,
            observedUtf8ByteSize: 200,
            configuredThresholdBytes: 100,
            referenceId: "artifact://sha256/not-actually-hex"));
    }

    [Theory]
    [InlineData(HarnessArtifactOutcomeCategory.Inline, HarnessArtifactDecisionReason.BelowThreshold)]
    [InlineData(HarnessArtifactOutcomeCategory.Failed, HarnessArtifactDecisionReason.NoAuthorizedWorkspace)]
    [InlineData(HarnessArtifactOutcomeCategory.RecoveryRequired, HarnessArtifactDecisionReason.CanceledAfterWrite)]
    public void ForOffload_OutcomesThatMustNotCarryAReference_WithNonNullReferenceId_Throws(
        HarnessArtifactOutcomeCategory outcome, HarnessArtifactDecisionReason reason)
    {
        var validReferenceId = HarnessArtifactIdentity.BuildReferenceId(
            HarnessArtifactIdentity.ComputeDigest("valid reference content"));

        Assert.Throws<ArgumentException>(() => HarnessArtifactDiagnostics.ForOffload(
            outcome,
            HarnessArtifactContentCategory.ToolResult,
            reason,
            observedUtf8ByteSize: 50,
            configuredThresholdBytes: 100,
            referenceId: validReferenceId));
    }

    [Fact]
    public void ForRehydration_WithMalformedReferenceId_Throws()
    {
        Assert.Throws<ArgumentException>(() => HarnessArtifactDiagnostics.ForRehydration(
            HarnessArtifactOutcomeCategory.Resolved,
            HarnessArtifactDecisionReason.DigestVerified,
            observedUtf8ByteSize: 50,
            configuredBudgetBytes: 100,
            referenceId: "not-a-canonical-reference-id"));
    }

    [Fact]
    public void ForRehydration_WithWrongPrefixReferenceId_Throws()
    {
        var digest = HarnessArtifactIdentity.ComputeDigest("valid reference content");

        Assert.Throws<ArgumentException>(() => HarnessArtifactDiagnostics.ForRehydration(
            HarnessArtifactOutcomeCategory.Missing,
            HarnessArtifactDecisionReason.Missing,
            observedUtf8ByteSize: null,
            configuredBudgetBytes: 100,
            referenceId: "artifact://sha1/" + digest));
    }

    [Fact]
    public void ForRehydration_WithUppercaseHexReferenceId_Throws()
    {
        var digest = HarnessArtifactIdentity.ComputeDigest("valid reference content");

        Assert.Throws<ArgumentException>(() => HarnessArtifactDiagnostics.ForRehydration(
            HarnessArtifactOutcomeCategory.Resolved,
            HarnessArtifactDecisionReason.DigestVerified,
            observedUtf8ByteSize: 50,
            configuredBudgetBytes: 100,
            referenceId: "artifact://sha256/" + digest.ToUpperInvariant()));
    }

    [Fact]
    public void ForOffload_ValidCanonicalReferenceId_Succeeds()
    {
        var validReferenceId = HarnessArtifactIdentity.BuildReferenceId(
            HarnessArtifactIdentity.ComputeDigest("valid reference content"));

        var diagnostics = HarnessArtifactDiagnostics.ForOffload(
            HarnessArtifactOutcomeCategory.Offloaded,
            HarnessArtifactContentCategory.ToolResult,
            HarnessArtifactDecisionReason.ThresholdExceeded,
            observedUtf8ByteSize: 200,
            configuredThresholdBytes: 100,
            referenceId: validReferenceId);

        Assert.Equal(validReferenceId, diagnostics.ReferenceId);
    }

    // ================================================================================
    // No-accessor / no-active-scope safety
    // ================================================================================

    [Fact]
    public void Transform_NoProgressAccessor_PreservesOrdinaryBehavior_AndDoesNotThrow()
    {
        using var fixture = HarnessArtifactTestFixture.Create();
        var content = new string('a', 50);
        var policy = CreatePolicy(fixture, maximumInlineToolResultBytes: 100);
        var request = CreateOffloadRequest(fixture, content, policy, progressAccessor: null);

        var outcome = HarnessToolResultOffloadTransform.Transform(request);

        Assert.Equal(HarnessToolResultOffloadStatus.Inline, outcome.Status);
        Assert.NotNull(outcome.Diagnostics);
    }

    [Fact]
    public void Transform_AccessorPresentButNoActiveScope_EmitsNothing_AndDoesNotThrow()
    {
        var (accessor, _, events) = CreateProgressHarness();
        using var fixture = HarnessArtifactTestFixture.Create();
        var content = new string('a', 50);
        var policy = CreatePolicy(fixture, maximumInlineToolResultBytes: 100);
        // Note: no `accessor.BeginScope(reporter)` — the accessor is supplied but has no active
        // scope, so `Current` resolves to the null reporter and no event is recorded.
        var request = CreateOffloadRequest(fixture, content, policy, accessor);

        var outcome = HarnessToolResultOffloadTransform.Transform(request);

        Assert.Equal(HarnessToolResultOffloadStatus.Inline, outcome.Status);
        Assert.Empty(events);
    }

    [Fact]
    public void Rehydrate_NoProgressAccessor_PreservesOrdinaryBehavior_AndDoesNotThrow()
    {
        using var fixture = HarnessArtifactTestFixture.Create(new FakeWorkspace(), progressAccessor: null);
        const string content = "content resolved with no progress accessor at all";
        var reference = fixture.CreateReference(content, CreatedAtUtc);
        fixture.Workspace.TryWriteFile(reference.WorkspacePath, content);
        var request = HarnessArtifactRehydrationRequest.Create(
            reference, HarnessArtifactRehydrationRequestSource.ToolRequest, 1_000_000);

        var result = fixture.Rehydration.Rehydrate(request, RehydratedAtUtc, CancellationToken.None);

        Assert.Equal(HarnessArtifactResolutionStatus.Resolved, result.Status);
        Assert.NotNull(result.Diagnostics);
    }

    [Fact]
    public void Rehydrate_AccessorPresentButNoActiveScope_EmitsNothing_AndDoesNotThrow()
    {
        var (accessor, _, events) = CreateProgressHarness();
        using var fixture = HarnessArtifactTestFixture.Create(new FakeWorkspace(), accessor);
        const string content = "content resolved while the accessor has no active scope";
        var reference = fixture.CreateReference(content, CreatedAtUtc);
        fixture.Workspace.TryWriteFile(reference.WorkspacePath, content);
        var request = HarnessArtifactRehydrationRequest.Create(
            reference, HarnessArtifactRehydrationRequestSource.ToolRequest, 1_000_000);

        // Note: no `accessor.BeginScope(reporter)` around this call.
        var result = fixture.Rehydration.Rehydrate(request, RehydratedAtUtc, CancellationToken.None);

        Assert.Equal(HarnessArtifactResolutionStatus.Resolved, result.Status);
        Assert.Empty(events);
    }

    // ================================================================================
    // Correlation / sequencing across multiple decisions
    // ================================================================================

    [Fact]
    public void MultipleDecisions_AcrossOffloadAndRehydration_SequenceNumbersIncreaseMonotonically()
    {
        var (accessor, reporter, events) = CreateProgressHarness();
        using var fixture = HarnessArtifactTestFixture.Create(new FakeWorkspace(), accessor);
        var smallContent = new string('a', 10);
        var largeContent = new string('b', 200);
        var policy = CreatePolicy(fixture, maximumInlineToolResultBytes: 100);
        var reference = fixture.CreateReference("rehydrate me", CreatedAtUtc);
        fixture.Workspace.TryWriteFile(reference.WorkspacePath, "rehydrate me");
        var rehydrationRequest = HarnessArtifactRehydrationRequest.Create(
            reference, HarnessArtifactRehydrationRequestSource.ToolRequest, 1_000_000);

        using (accessor.BeginScope(reporter))
        {
            HarnessToolResultOffloadTransform.Transform(CreateOffloadRequest(fixture, smallContent, policy, accessor));
            HarnessToolResultOffloadTransform.Transform(CreateOffloadRequest(fixture, largeContent, policy, accessor));
            fixture.Rehydration.Rehydrate(rehydrationRequest, RehydratedAtUtc, CancellationToken.None);
        }

        Assert.Equal(3, events.Count);
        var sequenceNumbers = events.Select(e => e.SequenceNumber).ToList();
        Assert.Equal(sequenceNumbers.OrderBy(s => s), sequenceNumbers);
        Assert.Equal(sequenceNumbers.Distinct().Count(), sequenceNumbers.Count);
        Assert.All(events, e => Assert.Equal(reporter.WorkflowId, e.WorkflowId));
    }

    // ================================================================================
    // Parent correlation: child and nested-child reporters
    // ================================================================================

    [Fact]
    public void Transform_RootChildAndNestedChildReporters_OffloadDecisionEvents_CarryAgentIdParentAgentIdDepthAndSharedGlobalSequence()
    {
        var events = new List<IProgressEvent>();
        var accessor = new ProgressReporterAccessor();
        var rootReporter = new ProgressReporter(
            "artifact-observability-offload-child-wf",
            [new CollectorSink(events)],
            new ProgressSequenceProvider(),
            agentId: "root-agent");
        var childReporter = rootReporter.CreateChild("child-agent");
        var nestedChildReporter = childReporter.CreateChild("nested-child-agent");

        using var fixture = HarnessArtifactTestFixture.Create();
        var policy = CreatePolicy(fixture, maximumInlineToolResultBytes: 100);

        using (accessor.BeginScope(rootReporter))
        {
            HarnessToolResultOffloadTransform.Transform(
                CreateOffloadRequest(fixture, new string('a', 10), policy, accessor));
        }
        using (accessor.BeginScope(childReporter))
        {
            HarnessToolResultOffloadTransform.Transform(
                CreateOffloadRequest(fixture, new string('b', 10), policy, accessor));
        }
        using (accessor.BeginScope(nestedChildReporter))
        {
            HarnessToolResultOffloadTransform.Transform(
                CreateOffloadRequest(fixture, new string('c', 10), policy, accessor));
        }

        var offloadEvents = events.OfType<HarnessArtifactOffloadDecisionEvent>().ToList();
        Assert.Equal(3, offloadEvents.Count);

        var rootEvent = offloadEvents[0];
        Assert.Equal("root-agent", rootEvent.AgentId);
        Assert.Null(rootEvent.ParentAgentId);
        Assert.Equal(0, rootEvent.Depth);

        var childEvent = offloadEvents[1];
        Assert.Equal("child-agent", childEvent.AgentId);
        Assert.Equal("root-agent", childEvent.ParentAgentId);
        Assert.Equal(1, childEvent.Depth);

        var nestedChildEvent = offloadEvents[2];
        Assert.Equal("nested-child-agent", nestedChildEvent.AgentId);
        Assert.Equal("child-agent", nestedChildEvent.ParentAgentId);
        Assert.Equal(2, nestedChildEvent.Depth);

        // Global sequence: the root, child, and nested-child reporters share the same underlying
        // sequence provider, so sequence numbers strictly increase across all three distinct
        // reporter instances rather than resetting per-reporter.
        Assert.True(rootEvent.SequenceNumber < childEvent.SequenceNumber);
        Assert.True(childEvent.SequenceNumber < nestedChildEvent.SequenceNumber);
        Assert.All(offloadEvents, e => Assert.Equal(rootReporter.WorkflowId, e.WorkflowId));
    }

    [Fact]
    public void Rehydrate_RootChildAndNestedChildReporters_RehydrationDecisionEvents_CarryAgentIdParentAgentIdDepthAndSharedGlobalSequence()
    {
        var events = new List<IProgressEvent>();
        var accessor = new ProgressReporterAccessor();
        var rootReporter = new ProgressReporter(
            "artifact-observability-rehydration-child-wf",
            [new CollectorSink(events)],
            new ProgressSequenceProvider(),
            agentId: "root-agent");
        var childReporter = rootReporter.CreateChild("child-agent");
        var nestedChildReporter = childReporter.CreateChild("nested-child-agent");

        using var fixture = HarnessArtifactTestFixture.Create(new FakeWorkspace(), accessor);
        var referenceForRoot = fixture.CreateReference("root-scoped content", CreatedAtUtc);
        fixture.Workspace.TryWriteFile(referenceForRoot.WorkspacePath, "root-scoped content");
        var referenceForChild = fixture.CreateReference("child-scoped content", CreatedAtUtc);
        fixture.Workspace.TryWriteFile(referenceForChild.WorkspacePath, "child-scoped content");
        var referenceForNestedChild = fixture.CreateReference("nested-child-scoped content", CreatedAtUtc);
        fixture.Workspace.TryWriteFile(referenceForNestedChild.WorkspacePath, "nested-child-scoped content");

        using (accessor.BeginScope(rootReporter))
        {
            fixture.Rehydration.Rehydrate(
                HarnessArtifactRehydrationRequest.Create(
                    referenceForRoot, HarnessArtifactRehydrationRequestSource.ToolRequest, 1_000_000),
                RehydratedAtUtc,
                CancellationToken.None);
        }
        using (accessor.BeginScope(childReporter))
        {
            fixture.Rehydration.Rehydrate(
                HarnessArtifactRehydrationRequest.Create(
                    referenceForChild, HarnessArtifactRehydrationRequestSource.ToolRequest, 1_000_000),
                RehydratedAtUtc,
                CancellationToken.None);
        }
        using (accessor.BeginScope(nestedChildReporter))
        {
            fixture.Rehydration.Rehydrate(
                HarnessArtifactRehydrationRequest.Create(
                    referenceForNestedChild, HarnessArtifactRehydrationRequestSource.ToolRequest, 1_000_000),
                RehydratedAtUtc,
                CancellationToken.None);
        }

        var rehydrationEvents = events.OfType<HarnessArtifactRehydrationDecisionEvent>().ToList();
        Assert.Equal(3, rehydrationEvents.Count);

        var rootEvent = rehydrationEvents[0];
        Assert.Equal("root-agent", rootEvent.AgentId);
        Assert.Null(rootEvent.ParentAgentId);
        Assert.Equal(0, rootEvent.Depth);

        var childEvent = rehydrationEvents[1];
        Assert.Equal("child-agent", childEvent.AgentId);
        Assert.Equal("root-agent", childEvent.ParentAgentId);
        Assert.Equal(1, childEvent.Depth);

        var nestedChildEvent = rehydrationEvents[2];
        Assert.Equal("nested-child-agent", nestedChildEvent.AgentId);
        Assert.Equal("child-agent", nestedChildEvent.ParentAgentId);
        Assert.Equal(2, nestedChildEvent.Depth);

        // Global sequence: shared sequence provider across the root, child, and nested-child
        // reporter instances.
        Assert.True(rootEvent.SequenceNumber < childEvent.SequenceNumber);
        Assert.True(childEvent.SequenceNumber < nestedChildEvent.SequenceNumber);
        Assert.All(rehydrationEvents, e => Assert.Equal(rootReporter.WorkflowId, e.WorkflowId));
    }

    // ================================================================================
    // Privacy: no raw/sensitive data anywhere in diagnostic or progress-event string properties
    // ================================================================================

    [Fact]
    public void OffloadDiagnostics_OffloadedOutcome_NeverContainRawContent_WorkspacePath_OwnerIds_OrExceptionMessage()
    {
        var (accessor, reporter, events) = CreateProgressHarness();
        var uniqueMarker = "UNIQUE-PAYLOAD-MARKER-" + Guid.NewGuid().ToString("N");
        using var fixture = HarnessArtifactTestFixture.Create();
        var content = uniqueMarker + new string('a', 200);
        var policy = CreatePolicy(fixture, maximumInlineToolResultBytes: 100);

        HarnessToolResultOffloadOutcome outcome;
        using (accessor.BeginScope(reporter))
        {
            outcome = HarnessToolResultOffloadTransform.Transform(CreateOffloadRequest(fixture, content, policy, accessor));
        }

        // The content is oversized relative to the threshold, so this exercises the Offloaded
        // outcome — the one offload outcome whose internal reference actually carries a workspace
        // path, making it the strongest case to prove that path never leaks into diagnostics.
        Assert.Equal(HarnessToolResultOffloadStatus.Offloaded, outcome.Status);
        Assert.NotNull(outcome.Reference);

        var evt = Assert.Single(events.OfType<HarnessArtifactOffloadDecisionEvent>());
        AssertNoSensitiveDataInStringProperties(
            evt,
            uniqueMarker,
            outcome.Reference!.WorkspacePath,
            HarnessArtifactTestFixture.DefaultUserId,
            HarnessArtifactTestFixture.DefaultOrchestrationId,
            HarnessArtifactTestFixture.DefaultSessionId);
    }

    [Fact]
    public void OffloadDiagnostics_WorkspaceWriteFailure_NeverContainsRawExceptionMessage()
    {
        var (accessor, reporter, events) = CreateProgressHarness();
        var uniqueExceptionMessage = "UNIQUE-EXCEPTION-MESSAGE-" + Guid.NewGuid().ToString("N");
        var workspace = new FakeWorkspace();
        using var fixture = HarnessArtifactTestFixture.Create(workspace);
        var content = new string('h', 500);
        workspace.WriteFileOverride = (_, _) =>
            WorkspaceResult<WriteFileResult>.Fail(new IOException(uniqueExceptionMessage));
        var policy = CreatePolicy(fixture, maximumInlineToolResultBytes: 10);

        HarnessToolResultOffloadOutcome outcome;
        using (accessor.BeginScope(reporter))
        {
            outcome = HarnessToolResultOffloadTransform.Transform(CreateOffloadRequest(fixture, content, policy, accessor));
        }

        Assert.Equal(HarnessToolResultOffloadStatus.Failed, outcome.Status);
        var evt = Assert.Single(events.OfType<HarnessArtifactOffloadDecisionEvent>());
        AssertNoSensitiveDataInStringProperties(evt, uniqueExceptionMessage, content);
    }

    [Fact]
    public void RehydrationDiagnostics_ResolvedOutcome_NeverContainsRawContent_WorkspacePath_OrOwnerIds()
    {
        var (accessor, reporter, events) = CreateProgressHarness();
        var uniqueMarker = "UNIQUE-REHYDRATION-MARKER-" + Guid.NewGuid().ToString("N");
        using var fixture = HarnessArtifactTestFixture.Create(new FakeWorkspace(), accessor);
        var content = uniqueMarker + " some recoverable content";
        var reference = fixture.CreateReference(content, CreatedAtUtc);
        fixture.Workspace.TryWriteFile(reference.WorkspacePath, content);
        var request = HarnessArtifactRehydrationRequest.Create(
            reference, HarnessArtifactRehydrationRequestSource.ToolRequest, 1_000_000);

        using (accessor.BeginScope(reporter))
        {
            fixture.Rehydration.Rehydrate(request, RehydratedAtUtc, CancellationToken.None);
        }

        var evt = Assert.Single(events.OfType<HarnessArtifactRehydrationDecisionEvent>());
        AssertNoSensitiveDataInStringProperties(
            evt,
            uniqueMarker,
            reference.WorkspacePath,
            HarnessArtifactTestFixture.DefaultUserId,
            HarnessArtifactTestFixture.DefaultOrchestrationId,
            HarnessArtifactTestFixture.DefaultSessionId);
    }

    // ================================================================================
    // Helpers
    // ================================================================================

    private static (IProgressReporterAccessor Accessor, IProgressReporter Reporter, List<IProgressEvent> Events) CreateProgressHarness()
    {
        var events = new List<IProgressEvent>();
        var accessor = new ProgressReporterAccessor();
        var reporter = new ProgressReporter(
            "artifact-observability-wf",
            [new CollectorSink(events)],
            new ProgressSequenceProvider());
        return (accessor, reporter, events);
    }

    private static HarnessToolResultOffloadPolicy CreatePolicy(
        HarnessArtifactTestFixture fixture,
        int maximumInlineToolResultBytes) =>
        HarnessToolResultOffloadPolicy.Create(
            maximumInlineToolResultBytes,
            fixture.SessionId,
            HarnessToolResultOffloadDescriptions.Default,
            checkpoint: null);

    private static HarnessToolResultOffloadRequest CreateOffloadRequest(
        HarnessArtifactTestFixture fixture,
        object? rawResult,
        HarnessToolResultOffloadPolicy policy,
        IProgressReporterAccessor? progressAccessor) =>
        new(
            rawResult,
            HarnessArtifactTestFixture.DefaultToolName,
            HarnessArtifactTestFixture.DefaultCallId,
            fixture.Binding,
            fixture.Accessor,
            policy,
            CreatedAtUtc,
            CancellationToken.None,
            progressAccessor);

    /// <summary>
    /// Reflects over every public string-typed property reachable from <paramref name="progressEvent"/>
    /// (its own properties plus its nested <c>Diagnostics</c> snapshot) and asserts none of them
    /// contain any of <paramref name="forbiddenValues"/>. Also asserts the same over the event's
    /// full string representation, covering any accidental inclusion via formatting.
    /// </summary>
    private static void AssertNoSensitiveDataInStringProperties(
        IProgressEvent progressEvent,
        params string[] forbiddenValues)
    {
        var stringValues = new List<string>();
        CollectStringPropertyValues(progressEvent, stringValues, depth: 0);

        foreach (var forbidden in forbiddenValues)
        {
            foreach (var value in stringValues)
            {
                Assert.DoesNotContain(forbidden, value, StringComparison.Ordinal);
            }

            Assert.DoesNotContain(forbidden, progressEvent.ToString(), StringComparison.Ordinal);
        }
    }

    private static void CollectStringPropertyValues(object? instance, List<string> into, int depth)
    {
        if (instance is null || depth > 3)
        {
            return;
        }

        var type = instance.GetType();
        if (type.Namespace is null || !type.Namespace.StartsWith("NexusLabs.Foundry", StringComparison.Ordinal))
        {
            return;
        }

        foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            object? value;
            try
            {
                value = property.GetValue(instance);
            }
            catch (TargetInvocationException)
            {
                continue;
            }

            if (value is string stringValue)
            {
                into.Add(stringValue);
            }
            else if (value is not null && value.GetType().Namespace?.StartsWith("NexusLabs.Foundry", StringComparison.Ordinal) == true)
            {
                CollectStringPropertyValues(value, into, depth + 1);
            }
        }
    }

    private sealed class CollectorSink(List<IProgressEvent> events) : IProgressSink
    {
        public ValueTask OnEventAsync(IProgressEvent progressEvent, CancellationToken cancellationToken)
        {
            events.Add(progressEvent);
            return ValueTask.CompletedTask;
        }
    }
}

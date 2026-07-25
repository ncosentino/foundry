using System.Text.Json;

using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

using NexusLabs.Foundry.MicrosoftAgentFramework.Context;
using NexusLabs.Foundry.MicrosoftAgentFramework.Diagnostics;
using NexusLabs.Foundry.MicrosoftAgentFramework.Harness;
using NexusLabs.Foundry.MicrosoftAgentFramework.Harness.Capabilities;
using NexusLabs.Foundry.MicrosoftAgentFramework.Harness.Context;

namespace NexusLabs.Foundry.MicrosoftAgentFramework.Tests.Harness;

/// <summary>
/// Integrated session/history proof, through an actual <see cref="InMemoryChatHistoryProvider"/> and the
/// full <see cref="HarnessProviderComposition"/> pipeline, that the compaction-only transient recovered
/// body never reaches the outer, per-service history persistence boundary — only the durable artifact
/// reference does — and that serialized/restored session state never carries the raw recovered body or a
/// workspace.
/// </summary>
public sealed class HarnessCompactionSessionIntegrationTests
{
    [Fact]
    public async Task Run_DurableProviderHistoryWithCompaction_PersistsOnlyDurableReferenceNeverRawRecoveredBody()
    {
        const string rawBody = "SECRET-RAW-RECOVERED-BODY";
        const string artifactContentSeed = "session-integration-artifact-content";
        const string recoveredEntryId = "recovered-entry-id";
        var digest = HarnessArtifactIdentity.ComputeDigest(artifactContentSeed);
        var referenceText = HarnessArtifactIdentity.BuildReferenceId(digest);
        var reference = HarnessCompactionTestFixture.SampleReference(
            artifactContentSeed, DateTimeOffset.UtcNow);
        var segment = HarnessArtifactRecoverableContextSegment.Create(
            reference, rawBody, DateTimeOffset.UtcNow);

        var function = AIFunctionFactory.Create(() => "tool-result", "G2Tool");
        using var services = HarnessCompositionTestFixture.CreateServices();
        var accessor = new AgentExecutionContextAccessor();
        var binding = HarnessCompositionTestFixture.CaptureBinding(accessor, out var scope);
        using (scope)
        {
            var historyProvider = new InMemoryChatHistoryProvider(
                new InMemoryChatHistoryProviderOptions());
            var leaf = new HarnessCompactionObservingChatClient(function.Name);
            var classifier = new HarnessScriptedMessageClassifier();

            // A generous policy, well below trigger: the augmented recoverable entry is neither evicted
            // nor reduced. The coordinated run scope this composed agent's outer RunAsync begins ensures
            // the real provider actually receives this stable body only on the first round it is selected
            // for; the second (tool-result) round's own selection of the exact same segment is filtered
            // back out because it was already marked delivered.
            var hybridProfile = HarnessHybridProfile.Create(
                HarnessCompactionTestFixture.CreatePolicy(
                    1000, 1, 5, 3, new HarnessConstantSizeContextEstimator(1)),
                HarnessScriptedUpstreamChatReducer.Echo(),
                classifier,
                baselineEntries => new HarnessMutableContextSnapshotProvider(
                    HarnessContextSnapshotAugmentation.WithRecoverableSegment(
                        baselineEntries, recoveredEntryId, segment)));

            var profile = HarnessCompactionSeamTestFixture.CreateCompactionEnabledHistoryProfile(
                HarnessToolLoopOwner.Harness,
                HarnessTelemetryOwner.Harness,
                HarnessHistoryPersistenceMode.DurableProvider);

            var request = HarnessCompositionTestFixture.CreateRequest(
                leaf,
                services,
                profile,
                HarnessCompositionTestFixture.CreateToolResolution(function),
                binding,
                accessor,
                metrics: null,
                historyProvider: HarnessCompositionTestFixture.CreateHistoryProviderPlugin(
                    HarnessHistoryPersistenceMode.DurableProvider,
                    historyProvider),
                planningProviders: null,
                approvalPlugin: null,
                skillsPlugin: null,
                progressAccessor: null,
                webSearchPlugin: null,
                offloadPlugin: null,
                hybridProfile);

            var result = new HarnessProviderComposition().Compose(request);
            Assert.Equal(HarnessProviderCompositionStatus.Success, result.Status);
            var agent = Assert.IsAssignableFrom<AIAgent>(result.Agent);

            var session = await agent.CreateSessionAsync(TestContext.Current.CancellationToken);

            // The incoming request/turn message is only the durable reference — never the raw body —
            // the same message the outer per-service history decorator will itself observe and persist.
            var response = await agent.RunAsync(
                referenceText, session, cancellationToken: TestContext.Current.CancellationToken);
            Assert.Equal("tool-result", response.GetText());

            // Two provider rounds reached the real provider (the initial call and the tool-result call),
            // but the raw recovered body was dispatched on exactly one of them — never both — even though
            // this run's SnapshotIntegration selected the exact same recoverable segment on every round.
            Assert.Equal(2, leaf.CallCount);
            Assert.Equal(
                1, leaf.ObservedCalls.Count(call => call.Any(message => message.Text == rawBody)));

            // The outer, per-service history provider persisted only the reference-bearing baseline
            // messages — never the raw recovered body or the compaction workspace/collaborators.
            var persisted = historyProvider.GetMessages(session);
            Assert.DoesNotContain(persisted, message => message.Text == rawBody);
            Assert.Contains(persisted, message => message.Text == referenceText);

            var serialized = await agent.SerializeSessionAsync(
                session, cancellationToken: TestContext.Current.CancellationToken);
            var rawJson = serialized.GetRawText();
            Assert.DoesNotContain(rawBody, rawJson, StringComparison.Ordinal);
            Assert.DoesNotContain("workspace", rawJson, StringComparison.OrdinalIgnoreCase);
            Assert.Contains(referenceText, rawJson, StringComparison.Ordinal);

            var restoredSession = await agent.DeserializeSessionAsync(
                serialized, cancellationToken: TestContext.Current.CancellationToken);
            var restoredMessages = historyProvider.GetMessages(restoredSession);
            Assert.DoesNotContain(restoredMessages, message => message.Text == rawBody);
            Assert.Contains(restoredMessages, message => message.Text == referenceText);
        }
    }

    [Fact]
    public async Task Run_DurableProviderHistoryWithCompaction_EvictsTransientBodyUnderPressureWhileReferenceSurvives()
    {
        const string rawBody = "SECRET-RAW-RECOVERED-BODY";
        const string artifactContentSeed = "session-integration-eviction-artifact-content";
        const string recoveredEntryId = "recovered-entry-id";
        var digest = HarnessArtifactIdentity.ComputeDigest(artifactContentSeed);
        var referenceText = HarnessArtifactIdentity.BuildReferenceId(digest);
        var reference = HarnessCompactionTestFixture.SampleReference(
            artifactContentSeed, DateTimeOffset.UtcNow);
        var segment = HarnessArtifactRecoverableContextSegment.Create(
            reference, rawBody, DateTimeOffset.UtcNow);

        var function = AIFunctionFactory.Create(() => "tool-result", "G2Tool");
        using var services = HarnessCompositionTestFixture.CreateServices();
        var accessor = new AgentExecutionContextAccessor();
        var binding = HarnessCompositionTestFixture.CaptureBinding(accessor, out var scope);
        using (scope)
        {
            var historyProvider = new InMemoryChatHistoryProvider(
                new InMemoryChatHistoryProviderOptions());
            var leaf = new HarnessCompactionObservingChatClient(function.Name);
            var classifier = new HarnessScriptedMessageClassifier();

            var sizesById = new Dictionary<string, int>
            {
                [recoveredEntryId] = 500,
            };

            // A tight policy well above the augmented entry's size: the assembler evicts the
            // recoverable entry (its matching durable reference already exists in baseline) before the
            // real provider ever sees it, while the reference itself — an ordinary baseline message —
            // is preserved and still reaches history untouched.
            var hybridProfile = HarnessHybridProfile.Create(
                HarnessCompactionTestFixture.CreatePolicy(
                    80, 20, 1, 3, new FallbackSizeContextEstimator(sizesById, fallbackSize: 1)),
                HarnessScriptedUpstreamChatReducer.Echo(),
                classifier,
                baselineEntries => new HarnessMutableContextSnapshotProvider(
                    HarnessContextSnapshotAugmentation.WithRecoverableSegment(
                        baselineEntries, recoveredEntryId, segment)));

            var profile = HarnessCompactionSeamTestFixture.CreateCompactionEnabledHistoryProfile(
                HarnessToolLoopOwner.Harness,
                HarnessTelemetryOwner.Harness,
                HarnessHistoryPersistenceMode.DurableProvider);

            var request = HarnessCompositionTestFixture.CreateRequest(
                leaf,
                services,
                profile,
                HarnessCompositionTestFixture.CreateToolResolution(function),
                binding,
                accessor,
                metrics: null,
                historyProvider: HarnessCompositionTestFixture.CreateHistoryProviderPlugin(
                    HarnessHistoryPersistenceMode.DurableProvider,
                    historyProvider),
                planningProviders: null,
                approvalPlugin: null,
                skillsPlugin: null,
                progressAccessor: null,
                webSearchPlugin: null,
                offloadPlugin: null,
                hybridProfile);

            var result = new HarnessProviderComposition().Compose(request);
            Assert.Equal(HarnessProviderCompositionStatus.Success, result.Status);
            var agent = Assert.IsAssignableFrom<AIAgent>(result.Agent);

            var session = await agent.CreateSessionAsync(TestContext.Current.CancellationToken);
            var response = await agent.RunAsync(
                referenceText, session, cancellationToken: TestContext.Current.CancellationToken);
            Assert.Equal("tool-result", response.GetText());

            // Evicted before ever reaching the real provider — the leaf never observed the raw body.
            Assert.DoesNotContain(
                leaf.ObservedCalls,
                call => call.Any(message => message.Text == rawBody));

            // The reference — an ordinary baseline message, never evictable itself — still reached the
            // real provider and is persisted in history exactly as the outer history decorator observed
            // it.
            var persisted = historyProvider.GetMessages(session);
            Assert.DoesNotContain(persisted, message => message.Text == rawBody);
            Assert.Contains(persisted, message => message.Text == referenceText);
        }
    }

    /// <summary>
    /// Test-only <see cref="IHarnessContextSizeEstimator"/> returning a large, explicitly configured size
    /// for a small set of known entry ids (used to force the augmented recoverable entry above the
    /// eviction threshold) and a small constant fallback for every other entry id encountered while
    /// running through the full agent pipeline, whose exact set of ordinary baseline entry ids is not
    /// otherwise known ahead of time.
    /// </summary>
    private sealed class FallbackSizeContextEstimator(
        IReadOnlyDictionary<string, int> sizesByEntryId, int fallbackSize) : IHarnessContextSizeEstimator
    {
        /// <summary>
        /// Always <see cref="HarnessContextMeasurementUnit.HostDefinedUnits"/>: this fixture's sizes
        /// are arbitrary configured values with no byte or token meaning.
        /// </summary>
        public HarnessContextMeasurementUnit MeasurementUnit => HarnessContextMeasurementUnit.HostDefinedUnits;

        public int EstimateSize(HarnessContextEntry entry)
        {
            ArgumentNullException.ThrowIfNull(entry);
            return sizesByEntryId.TryGetValue(entry.EntryId, out var size) ? size : fallbackSize;
        }
    }
}

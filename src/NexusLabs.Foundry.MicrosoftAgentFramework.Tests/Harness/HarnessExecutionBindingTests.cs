using NexusLabs.Foundry.MicrosoftAgentFramework.Context;
using NexusLabs.Foundry.MicrosoftAgentFramework.Harness;
using NexusLabs.Foundry.MicrosoftAgentFramework.Workspace;

namespace NexusLabs.Foundry.MicrosoftAgentFramework.Tests.Harness;

public sealed class HarnessExecutionBindingTests
{
    [Fact]
    public void Capture_MissingContext_FailsClosed()
    {
        var accessor = new AgentExecutionContextAccessor();

        var result = HarnessExecutionBinding.Capture(
            accessor,
            HarnessCompositionTestFixture.SessionId,
            requireWorkspace: true);

        Assert.Equal(HarnessExecutionBindingStatus.MissingContext, result.Status);
        Assert.Null(result.Binding);
    }

    [Fact]
    public void Capture_RequiredWorkspaceMissing_FailsClosed()
    {
        var accessor = new AgentExecutionContextAccessor();
        using var scope = accessor.BeginScope(
            new AgentExecutionContext("user-1", "orchestration-1"));

        var result = HarnessExecutionBinding.Capture(
            accessor,
            HarnessCompositionTestFixture.SessionId,
            requireWorkspace: true);

        Assert.Equal(HarnessExecutionBindingStatus.MissingWorkspace, result.Status);
    }

    [Fact]
    public void Validate_MatchingCurrentContext_IsValid()
    {
        var accessor = new AgentExecutionContextAccessor();
        var binding = HarnessCompositionTestFixture.CaptureBinding(
            accessor,
            out var scope);
        using (scope)
        {
            var result = binding.ValidateCurrent(
                accessor,
                HarnessCompositionTestFixture.SessionId);

            Assert.Equal(HarnessExecutionBindingStatus.Valid, result.Status);
        }
    }

    [Fact]
    public void Validate_ChangedIdentity_FailsClosed()
    {
        var accessor = new AgentExecutionContextAccessor();
        var binding = HarnessCompositionTestFixture.CaptureBinding(
            accessor,
            out var firstScope);
        firstScope.Dispose();
        using var secondScope = accessor.BeginScope(
            new AgentExecutionContext(
                "user-2",
                "orchestration-1",
                Workspace: binding.Workspace));

        var result = binding.ValidateCurrent(
            accessor,
            HarnessCompositionTestFixture.SessionId);

        Assert.Equal(HarnessExecutionBindingStatus.IdentityMismatch, result.Status);
    }

    [Fact]
    public void Validate_ChangedWorkspace_FailsClosed()
    {
        var accessor = new AgentExecutionContextAccessor();
        var binding = HarnessCompositionTestFixture.CaptureBinding(
            accessor,
            out var firstScope);
        firstScope.Dispose();
        using var secondScope = accessor.BeginScope(
            new AgentExecutionContext(
                "user-1",
                "orchestration-1",
                Workspace: new InMemoryWorkspace()));

        var result = binding.ValidateCurrent(
            accessor,
            HarnessCompositionTestFixture.SessionId);

        Assert.Equal(HarnessExecutionBindingStatus.WorkspaceMismatch, result.Status);
    }

    [Fact]
    public void Validate_ChangedSession_FailsClosed()
    {
        var accessor = new AgentExecutionContextAccessor();
        var binding = HarnessCompositionTestFixture.CaptureBinding(
            accessor,
            out var scope);
        using (scope)
        {
            var result = binding.ValidateCurrent(
                accessor,
                "different-session");

            Assert.Equal(HarnessExecutionBindingStatus.SessionMismatch, result.Status);
        }
    }
}

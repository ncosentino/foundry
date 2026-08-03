namespace NexusLabs.Foundry.MicrosoftAgentFramework.Tests;

/// <summary>
/// Covers the resolver every agent-name derivation goes through, so the policy it applies is pinned
/// in one place rather than re-asserted at each call site.
/// </summary>
public sealed class FoundryAgentNameTests
{
    [Fact]
    public void Resolve_AgentWithoutADeclaredName_UsesTheClassName()
    {
        Assert.Equal(
            nameof(LookupWriterAgent),
            FoundryAgentName.Resolve(typeof(LookupWriterAgent)));
    }

    [Fact]
    public void Resolve_AgentWithADeclaredName_UsesTheDeclaredName()
    {
        Assert.Equal("PublishedEditor", FoundryAgentName.Resolve(typeof(NamedEditorAgent)));
    }

    /// <remarks>
    /// A blank name is an authoring slip, not a request for a blank identity. Falling back keeps the
    /// agent addressable; the alternative is a name nobody can type.
    /// </remarks>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Resolve_BlankDeclaredName_FallsBackToTheClassName(string? declared)
    {
        var attribute = new FoundryAgentAttribute { Name = declared };

        Assert.Equal(
            nameof(LookupWriterAgent),
            FoundryAgentName.Resolve(attribute, typeof(LookupWriterAgent)));
    }

    /// <remarks>
    /// Registration and diagnostics ask for a name before anything has established that the type is
    /// a declared agent, so this must not throw ahead of the code that reports the real problem.
    /// </remarks>
    [Fact]
    public void Resolve_TypeWithoutTheAttribute_UsesTheClassName()
    {
        Assert.Equal(
            nameof(FoundryAgentNameTests),
            FoundryAgentName.Resolve(typeof(FoundryAgentNameTests)));
    }

    [Fact]
    public void Resolve_NullType_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => FoundryAgentName.Resolve(null!));
        Assert.Throws<ArgumentNullException>(
            () => FoundryAgentName.Resolve(new FoundryAgentAttribute(), null!));
    }
}

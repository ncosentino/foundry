namespace NexusLabs.Foundry.MicrosoftAgentFramework.Harness.Context;

/// <summary>The explicit, binary outcome of <see cref="HarnessCompactionVerifier"/>.</summary>
internal enum HarnessCompactionVerificationOutcome
{
    /// <summary>The proposed reduced entries satisfied every preservation and sequencing requirement.</summary>
    Accepted,

    /// <summary>The proposed reduced entries violated at least one preservation or sequencing requirement.</summary>
    Rejected,
}

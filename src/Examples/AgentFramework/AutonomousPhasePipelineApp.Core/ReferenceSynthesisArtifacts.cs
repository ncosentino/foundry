namespace AutonomousPhasePipelineApp.Core;

internal static class ReferenceSynthesisArtifacts
{
    internal const string Invalid =
        """
        {
          "summary": "Initial synthesis is missing a required field.",
          "evidence": ["research"]
        }
        """;

    internal const string Valid =
        """
        {
          "summary": "Synthesis completed from accepted artifacts.",
          "evidence": ["research", "required-specialist"],
          "recommendation": "Proceed with the synthetic release."
        }
        """;
}

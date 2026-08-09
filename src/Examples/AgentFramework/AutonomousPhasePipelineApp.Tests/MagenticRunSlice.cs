using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;

namespace AutonomousPhasePipelineApp.Tests;

internal sealed record MagenticRunSlice(
    List<ChatMessage>? Output,
    CheckpointInfo? Checkpoint,
    ExternalRequest? Request);

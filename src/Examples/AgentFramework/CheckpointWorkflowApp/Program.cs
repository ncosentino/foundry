using CheckpointWorkflowApp;

using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;

using NexusLabs.Foundry.MicrosoftAgentFramework.Workflows;

var producerClient = new CheckpointScriptedChatClient(
    ["validated research artifact"]);
var synthesisClient = new CheckpointScriptedChatClient(
    [
        "initial synthesis",
        "restored synthesis",
    ]);
CheckpointManager checkpointManager = CheckpointManager.CreateInMemory();
CheckpointInfo? acceptedArtifactCheckpoint = null;
bool acceptedArtifactBoundaryCompleted = false;
var initialOutputs = new List<string>();

await using StreamingRun run = await BuildWorkflow(
    producerClient,
    synthesisClient).StartCheckpointedAgentRunAsync(
        "Produce and synthesize the artifact.",
        checkpointManager,
        "checkpoint-workflow-example",
        CancellationToken.None);
await foreach (WorkflowEvent workflowEvent in run.WatchStreamAsync())
{
    if (workflowEvent is ExecutorCompletedEvent
        {
            ExecutorId: "accepted-artifact-boundary",
        })
    {
        acceptedArtifactBoundaryCompleted = true;
    }

    if (acceptedArtifactBoundaryCompleted &&
        workflowEvent is SuperStepCompletedEvent
        {
            CompletionInfo.Checkpoint: { } checkpoint,
        })
    {
        acceptedArtifactCheckpoint ??= checkpoint;
        acceptedArtifactBoundaryCompleted = false;
    }

    if (workflowEvent is AgentResponseUpdateEvent update)
    {
        initialOutputs.Add(update.Update.ToString());
    }

    if (workflowEvent is WorkflowErrorEvent error)
    {
        Console.WriteLine($"CheckpointWorkflowApp:initial-error:{error.Exception?.Message}");
        return 1;
    }
}

if (acceptedArtifactCheckpoint is null ||
    producerClient.CallCount != 1 ||
    synthesisClient.CallCount != 1 ||
    initialOutputs.Count(output => output == "initial synthesis") != 1)
{
    Console.WriteLine("CheckpointWorkflowApp:initial-run-invalid");
    return 1;
}

await run.RestoreCheckpointAsync(
    acceptedArtifactCheckpoint,
    CancellationToken.None);
var restoredOutputs = new List<string>();
await foreach (WorkflowEvent workflowEvent in run.WatchStreamAsync())
{
    if (workflowEvent is AgentResponseUpdateEvent update)
    {
        restoredOutputs.Add(update.Update.ToString());
    }

    if (workflowEvent is WorkflowErrorEvent error)
    {
        Console.WriteLine($"CheckpointWorkflowApp:restore-error:{error.Exception?.Message}");
        return 1;
    }
}

if (producerClient.CallCount != 1 ||
    synthesisClient.CallCount != 2 ||
    restoredOutputs.Count(output => output == "restored synthesis") != 1)
{
    Console.WriteLine(
        "CheckpointWorkflowApp:unexpected-replay:" +
        $"producer={producerClient.CallCount}:" +
        $"synthesis={synthesisClient.CallCount}:" +
        $"outputs={restoredOutputs.Count}");
    return 1;
}

Console.WriteLine(
    $"CheckpointWorkflowApp:restored:{acceptedArtifactCheckpoint.CheckpointId}");
Console.WriteLine("CheckpointWorkflowApp:completed");
return 0;

static Workflow BuildWorkflow(
    IChatClient producerClient,
    IChatClient synthesisClient)
{
    AIAgentHostOptions agentOptions = new()
    {
        ForwardIncomingMessages = true,
        ReassignOtherAgentsAsUsers = true,
    };
    ExecutorBinding producer = new AIAgentBinding(
        CreateAgent("artifact-producer", producerClient),
        agentOptions);
    ExecutorBinding acceptedArtifactBoundary =
        new ChatForwardingExecutor("accepted-artifact-boundary").BindExecutor();
    ExecutorBinding synthesis = new AIAgentBinding(
        CreateAgent("artifact-synthesis", synthesisClient),
        agentOptions);

    return new WorkflowBuilder(producer)
        .AddEdge(producer, acceptedArtifactBoundary)
        .AddEdge(acceptedArtifactBoundary, synthesis)
        .WithOutputFrom(synthesis)
        .WithName("checkpoint-workflow-example")
        .Build();
}

static AIAgent CreateAgent(
    string id,
    IChatClient chatClient) =>
    chatClient.AsAIAgent(
        new ChatClientAgentOptions
        {
            Id = id,
            Name = id,
            Description = $"{id} phase.",
        });

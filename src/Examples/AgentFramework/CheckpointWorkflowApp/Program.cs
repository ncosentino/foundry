using CheckpointWorkflowApp;

using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;

using NexusLabs.Foundry.MicrosoftAgentFramework.Workflows;

var producerClient = new CheckpointScriptedChatClient(
    ["validated research artifact"]);
var synthesisClient = new CheckpointScriptedChatClient(
    [
        new InvalidOperationException("simulated downstream failure"),
    ]);
CheckpointManager checkpointManager = CheckpointManager.CreateInMemory();
CheckpointInfo? acceptedArtifactCheckpoint = null;
bool failureObserved = false;
bool acceptedArtifactBoundaryCompleted = false;

await using (StreamingRun initialRun = await BuildWorkflow(
    producerClient,
    synthesisClient).StartCheckpointedAgentRunAsync(
        "Produce and synthesize the artifact.",
        checkpointManager,
        "checkpoint-workflow-example",
        CancellationToken.None))
{
    await foreach (WorkflowEvent workflowEvent in initialRun.WatchStreamAsync())
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

        if (workflowEvent is WorkflowErrorEvent or ExecutorFailedEvent)
        {
            failureObserved = true;
        }
    }
}

if (acceptedArtifactCheckpoint is null || !failureObserved)
{
    Console.WriteLine("CheckpointWorkflowApp:initial-run-did-not-reach-recovery-point");
    return 1;
}

var recoveredProducerClient = new CheckpointScriptedChatClient(
    [new InvalidOperationException("accepted producer phase replayed")]);
var recoveredSynthesisClient = new CheckpointScriptedChatClient(
    ["recovered synthesis"]);
var recoveredOutputs = new List<string>();
await using (StreamingRun recoveredRun = await BuildWorkflow(
    recoveredProducerClient,
    recoveredSynthesisClient).ResumeCheckpointedAgentRunAsync(
        acceptedArtifactCheckpoint,
        checkpointManager,
        CancellationToken.None))
{
    await foreach (WorkflowEvent workflowEvent in recoveredRun.WatchStreamAsync())
    {
        if (workflowEvent is AgentResponseUpdateEvent update)
        {
            recoveredOutputs.Add(update.Update.ToString());
        }

        if (workflowEvent is WorkflowErrorEvent error)
        {
            Console.WriteLine($"CheckpointWorkflowApp:resume-error:{error.Exception?.Message}");
            return 1;
        }
    }
}

int recoveredOutputCount = recoveredOutputs.Count(
    output => output == "recovered synthesis");
if (producerClient.CallCount != 1 ||
    synthesisClient.CallCount != 1 ||
    recoveredProducerClient.CallCount != 0 ||
    recoveredSynthesisClient.CallCount != 1 ||
    recoveredOutputCount != 1)
{
    Console.WriteLine(
        "CheckpointWorkflowApp:unexpected-replay:" +
        $"initial-producer={producerClient.CallCount}:" +
        $"initial-synthesis={synthesisClient.CallCount}:" +
        $"recovered-producer={recoveredProducerClient.CallCount}:" +
        $"recovered-synthesis={recoveredSynthesisClient.CallCount}:" +
        $"outputs={recoveredOutputCount}");
    return 1;
}

Console.WriteLine(
    $"CheckpointWorkflowApp:resumed:{acceptedArtifactCheckpoint.CheckpointId}");
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

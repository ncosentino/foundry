using NexusLabs.Foundry.MicrosoftAgentFramework;
using NexusLabs.Foundry.MicrosoftAgentFramework.Workflows;

namespace GroupChatAgentFrameworkApp.Agents;

/// <summary>
/// Final stage of the code-review group chat. Reads the assessments from all reviewers and
/// delivers a consensus verdict, then signals that the workflow can stop.
/// </summary>
/// <remarks>
/// The <see cref="AgentTerminationConditionAttribute"/> wires a
/// <see cref="KeywordTerminationCondition"/> into the <c>RoundRobinGroupChatManager</c>
/// at workflow-creation time. When a response contains "CONSENSUS: APPROVED", the group chat
/// stops before the next iteration begins — no extra code in Program.cs required.
/// <para>
/// The condition is evaluated after every participant's turn, not only this agent's. It works here
/// because only this agent is instructed to emit those verdict phrases; a reviewer that happened to
/// quote one would stop the chat too. A condition that must be restricted to one agent has to
/// compare <c>TerminationContext.AgentId</c> itself.
/// </para>
/// </remarks>
[FoundryAgent(
    Description = "Synthesises reviewer feedback and delivers a final consensus verdict.",
    Instructions = """
        You are the code-review coordinator. The other reviewers have already given their
        individual assessments. Your job is to synthesise their findings into a single verdict.

        Rules:
        - If ALL reviewers found no critical issues (they said "LGTM" or "no issues"), end
          your response with exactly: CONSENSUS: APPROVED
        - If ANY reviewer flagged unresolved issues, summarise the top items and end with
          exactly: CONSENSUS: CHANGES REQUIRED

        Be concise. One short paragraph of synthesis, then the verdict on its own line.
        """,
    FunctionTypes = new Type[0])]
[AgentGroupChatMember("code-review")]
[AgentTerminationCondition(typeof(KeywordTerminationCondition), "CONSENSUS: APPROVED")]
[AgentTerminationCondition(typeof(KeywordTerminationCondition), "CONSENSUS: CHANGES REQUIRED")]
public partial class ReviewOrchestratorAgent { }

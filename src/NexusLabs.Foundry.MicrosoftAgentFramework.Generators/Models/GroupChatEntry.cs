// Copyright (c) NexusLabs. All rights reserved.
// Licensed under the MIT License.

namespace NexusLabs.Foundry.MicrosoftAgentFramework.Generators;

internal readonly struct GroupChatEntry
{
    public GroupChatEntry(string agentTypeName, string groupName, int order = 0)
    {
        AgentTypeName = agentTypeName;
        GroupName = groupName;
        Order = order;
    }

    public string AgentTypeName { get; }
    public string GroupName { get; }
    public int Order { get; }
}

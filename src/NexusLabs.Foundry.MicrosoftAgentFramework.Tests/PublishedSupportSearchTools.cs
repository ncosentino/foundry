using System.ComponentModel;

using Microsoft.Extensions.AI;

namespace NexusLabs.Foundry.MicrosoftAgentFramework.Tests;

#pragma warning disable MEAI001

internal static class PublishedSupportSearchTools
{
    [AgentFunction]
    [AIFunctionName("shared_search")]
    [Description("Searches support records.")]
    public static string SearchSupport(
        [AIParameterName("search_query")]
        [Description("The support query.")] string query) =>
        query;
}

#pragma warning restore MEAI001

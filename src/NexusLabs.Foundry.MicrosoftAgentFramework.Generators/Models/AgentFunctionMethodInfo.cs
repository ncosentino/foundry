// Copyright (c) NexusLabs. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Immutable;

namespace NexusLabs.Foundry.MicrosoftAgentFramework.Generators;

internal readonly struct AgentFunctionMethodInfo
{
    public AgentFunctionMethodInfo(
        string methodName, string? publishedName, bool isAsync, bool isVoidLike,
        string? returnValueTypeFQN, string? returnJsonSchemaType,
        string? returnObjectSchemaJson,
        ImmutableArray<AgentFunctionParameterInfo> parameters,
        string description)
    {
        MethodName = methodName; PublishedName = publishedName;
        IsAsync = isAsync; IsVoidLike = isVoidLike;
        ReturnValueTypeFQN = returnValueTypeFQN; ReturnJsonSchemaType = returnJsonSchemaType;
        ReturnObjectSchemaJson = returnObjectSchemaJson;
        Parameters = parameters; Description = description;
    }

    public string MethodName { get; }
    public string? PublishedName { get; }
    public bool IsAsync { get; }
    public bool IsVoidLike { get; }
    public string? ReturnValueTypeFQN { get; }
    public string? ReturnJsonSchemaType { get; }
    public string? ReturnObjectSchemaJson { get; }
    public ImmutableArray<AgentFunctionParameterInfo> Parameters { get; }
    public string Description { get; }
}

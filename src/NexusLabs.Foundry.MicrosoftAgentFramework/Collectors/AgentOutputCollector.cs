using System.Collections.Concurrent;

namespace NexusLabs.Foundry.MicrosoftAgentFramework.Collectors;

/// <summary>
/// Default thread-safe implementation of <see cref="IAgentOutputCollector{T}"/>
/// backed by a <see cref="ConcurrentQueue{T}"/>.
/// </summary>
public sealed class AgentOutputCollector<T> : IAgentOutputCollector<T>
{
    private readonly ConcurrentQueue<T> _items = new();

    /// <inheritdoc />
    public void Add(T item) => _items.Enqueue(item);

    /// <inheritdoc />
    public IReadOnlyList<T> Items => _items.ToArray();

    /// <inheritdoc />
    public int Count => _items.Count;
}

namespace Sts2Sim.Core.Random;

internal sealed class KeyedRngTrace
{
    private readonly object _gate = new();
    private Node? _tail;
    private int _count;

    public KeyedRngTrace() { }

    private KeyedRngTrace(Node? tail, int count)
    {
        _tail = tail;
        _count = count;
    }

    public void Record(
        string streamName,
        string semanticKey,
        int drawOrdinal,
        string operation,
        string result)
    {
        lock (_gate)
        {
            _tail = new Node(
                new KeyedRngDraw(streamName, semanticKey, drawOrdinal, operation, result),
                _tail);
            _count++;
        }
    }

    public IReadOnlyList<KeyedRngDraw> Snapshot()
    {
        lock (_gate)
        {
            var draws = new KeyedRngDraw[_count];
            Node? node = _tail;
            for (int index = draws.Length - 1; index >= 0; index--)
            {
                draws[index] = node!.Draw;
                node = node.Previous;
            }
            return draws;
        }
    }

    public KeyedRngTrace CloneExact()
    {
        lock (_gate)
        {
            // Nodes are immutable. Sharing this point-in-time prefix makes combat-search
            // CloneExact O(1); subsequent records replace each trace's own tail independently.
            return new KeyedRngTrace(_tail, _count);
        }
    }

    private sealed record Node(KeyedRngDraw Draw, Node? Previous);
}

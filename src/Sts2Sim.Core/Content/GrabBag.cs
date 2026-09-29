using Sts2Sim.Core.Random;

namespace Sts2Sim.Core.Content;

/// <summary>A weighted draw bag using upstream full-pool rejection sampling.</summary>
public sealed class GrabBag<T>
{
    private readonly List<(T Item, double Weight)> _entries = new();
    private double _totalWeight;

    public bool Any() => _entries.Count > 0;

    public void Add(T item, double weight)
    {
        _entries.Add((item, weight));
        _totalWeight += weight;
    }

    public T? GrabAndRemove(Rng rng, Func<T, bool>? predicate = null)
    {
        if (predicate is not null && !_entries.Any(entry => predicate(entry.Item)))
        {
            return default;
        }

        int index;
        do
        {
            index = GrabIndex(rng);
        }
        while (predicate is not null && index >= 0 && !predicate(_entries[index].Item));

        if (index < 0)
        {
            return default;
        }

        (T Item, double Weight) selected = _entries[index];
        _totalWeight -= selected.Weight;
        _entries.RemoveAt(index);
        return selected.Item;
    }

    private int GrabIndex(Rng rng)
    {
        double roll = rng.NextDouble() * _totalWeight;
        double cumulativeWeight = 0;
        for (int index = 0; index < _entries.Count; index++)
        {
            cumulativeWeight += _entries[index].Weight;
            if (roll < cumulativeWeight)
            {
                return index;
            }
        }
        return -1;
    }
}

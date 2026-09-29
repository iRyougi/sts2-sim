using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Factories;
using Sts2Sim.Core.Random;

namespace Sts2Sim.Core.Tests.Factories;

public class RelicFactoryTests
{
    [Fact]
    public void RollRarity_MapsEachObservedThresholdBand()
    {
        var rng = new Rng(20260723u);
        var twin = new Rng(20260723u);
        var observed = new HashSet<RelicRarity>();

        for (int i = 0; i < 1_000; i++)
        {
            float roll = twin.NextFloat();
            RelicRarity expected = roll < 0.5f ? RelicRarity.Common
                : roll < 0.83f ? RelicRarity.Uncommon
                : RelicRarity.Rare;

            Assert.Equal(expected, RelicFactory.RollRarity(rng));
            observed.Add(expected);
        }

        Assert.Equal(new[] { RelicRarity.Common, RelicRarity.Uncommon, RelicRarity.Rare }, observed.Order());
    }

    [Fact]
    public void RollRarity_ProducesExpectedShares()
    {
        var rng = new Rng(20260724u);
        var counts = new Dictionary<RelicRarity, int>();

        for (int i = 0; i < 10_000; i++)
        {
            RelicRarity rarity = RelicFactory.RollRarity(rng);
            counts[rarity] = counts.GetValueOrDefault(rarity) + 1;
        }

        Assert.InRange(counts.GetValueOrDefault(RelicRarity.Common), 4_700, 5_300);
        Assert.InRange(counts.GetValueOrDefault(RelicRarity.Uncommon), 3_000, 3_600);
        Assert.InRange(counts.GetValueOrDefault(RelicRarity.Rare), 1_400, 2_000);
    }
}

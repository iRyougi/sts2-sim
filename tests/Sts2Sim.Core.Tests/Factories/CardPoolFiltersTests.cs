using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Factories;
using Sts2Sim.Core.Models;

namespace Sts2Sim.Core.Tests.Factories;

file sealed class PoolTestCard(CardRarity rarity, bool multiplayerOnly = false, bool canGenerate = true) : CardModel
{
    public override CardType Type => CardType.Attack;

    public override CardRarity Rarity => rarity;

    public override TargetType TargetType => TargetType.AnyEnemy;

    public override bool IsMultiplayerOnly => multiplayerOnly;

    public override bool CanBeGeneratedInCombat => canGenerate;

    protected override int CanonicalEnergyCost => 1;
}

public sealed class CardPoolFiltersTests
{
    [Fact]
    public void ForSinglePlayer_ExcludesOnlyMultiplayerCards()
    {
        CardModel eligible = new PoolTestCard(CardRarity.Common);
        CardModel multiplayerOnly = new PoolTestCard(CardRarity.Rare, multiplayerOnly: true);

        IReadOnlyList<CardModel> filtered = CardPoolFilters.ForSinglePlayer(new[] { eligible, multiplayerOnly }).ToList();

        Assert.Equal(new[] { eligible }, filtered);
    }

    [Fact]
    public void ForCombatGeneration_ExcludesIneligibleRaritiesAndDeduplicates()
    {
        CardModel eligible = new PoolTestCard(CardRarity.Common);
        CardModel[] filtered = CardPoolFilters.ForCombatGeneration(new CardModel[]
        {
            eligible, eligible,
            new PoolTestCard(CardRarity.Basic),
            new PoolTestCard(CardRarity.Ancient),
            new PoolTestCard(CardRarity.Event),
            new PoolTestCard(CardRarity.Rare, multiplayerOnly: true),
            new PoolTestCard(CardRarity.Uncommon, canGenerate: false),
        }).ToArray();

        Assert.Equal(new[] { eligible }, filtered);
    }
}

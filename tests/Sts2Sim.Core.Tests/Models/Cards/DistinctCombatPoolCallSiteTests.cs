using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Content;
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Rngs;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.CardPools;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Models.Potions;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.Models.Relics;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Tests.Models.Cards;

[Collection("ModelDb")]
public sealed class DistinctCombatPoolCallSiteTests : IDisposable
{
    public DistinctCombatPoolCallSiteTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(ContentRegistry.AllTypes);
    }

    public void Dispose() => ModelDb.ResetForTests();

    [Theory]
    [InlineData("Discovery", 3, false)]
    [InlineData("Quasar", 3, false)]
    [InlineData("Quasar", 3, true)]
    [InlineData("Toolbox", 3, false)]
    [InlineData("CosmicConcoction", 3, true)]
    [InlineData("OrangeDough", 2, false)]
    [InlineData("SpectrumShiftPower", 2, false)]
    [InlineData("BundleOfJoy", 3, false)]
    [InlineData("BundleOfJoy", 4, true)]
    [InlineData("JackOfAllTrades", 1, false)]
    [InlineData("JackOfAllTrades", 2, true)]
    [InlineData("Abundance", 3, false)]
    [InlineData("ManifestAuthority", 1, false)]
    [InlineData("Crossbow", 1, false)]
    public async Task CallSite_UsesNativeFullPoolShuffle(string name, int count, bool upgraded)
    {
        const string seed = "HSP7P798XTVC";
        var run = new RunState(seed, new Overgrowth());
        Player player = Player.CreateForNewRun(ModelDb.Character<Silent>(), run);
        run.AddPlayer(player);
        player.ResetCombatState();
        player.PlayerCombatState!.TurnNumber = 1;
        var combat = new CombatState(run);
        combat.AddPlayerCreature(player.Creature);
        var source = new ChoiceSource();
        combat.CardSelectionSource = source;
        IEnumerable<CardModel> nativePool = name is "Discovery" or "Abundance" or "Crossbow"
            ? player.Character.CardPool.GetUnlockedCards(player.UnlockState, false)
            : ColorlessCardPool.Instance.GetUnlockedCards(player.UnlockState, false);
        if (name == "Abundance") nativePool = nativePool.Where(c => c.Type == CardType.Power);
        if (name == "Crossbow") nativePool = nativePool.Where(c => c.Type == CardType.Attack);
        if (name == "JackOfAllTrades") nativePool = nativePool.Where(c => c is not JackOfAllTrades);
        // Native factory filters canonical candidates, then TakeRandom performs descending Fisher–Yates.
        var expected = nativePool.Where(c => c.CanBeGeneratedInCombat &&
            c.Rarity is CardRarity.Common or CardRarity.Uncommon or CardRarity.Rare).Distinct().ToArray();
        var oracle = run.Rng.CombatCardGeneration.CloneExact();
        for (int i = expected.Length - 1; i > 0; i--)
        {
            int j = oracle.NextInt(i + 1);
            (expected[i], expected[j]) = (expected[j], expected[i]);
        }
        var counters = Enum.GetValues<RunRngType>().ToDictionary(t => t, t => run.Rng.GetRng(t).Counter);
        AbstractModel model = (AbstractModel)ModelDb.Get(typeof(CardModel).Assembly.GetTypes()
            .Single(t => t.Name == name)).MutableClone();
        switch (model)
        {
            case CardModel card:
                card.AssignOwner(player);
                if (upgraded) card.Upgrade();
                await card.PlayAsync(null);
                break;
            case PotionModel potion:
                potion.AssignOwner(player);
                await potion.UseInternal(player.Creature);
                break;
            case SpectrumShiftPower power:
                power.ApplyInternal(player.Creature, count);
                await power.BeforeHandDraw(player);
                break;
            case RelicModel relic:
                relic.AssignOwner(player);
                if (relic is Toolbox toolbox) await toolbox.BeforeHandDraw(player);
                else await relic.AfterSideTurnStart(CombatSide.Player, new[] { player.Creature });
                break;
        }
        IReadOnlyList<CardModel> actual = model is Abundance abundance ? abundance.GeneratedCandidates
            : source.Candidates.Count > 0 ? source.Candidates : player.PlayerCombatState.Hand.Cards;
        Assert.Equal(expected.Take(count).Select(c => c.Id), actual.Select(c => c.Id));
        Assert.All(actual, c => Assert.Same(player, c.Owner));
        Assert.True(run.Rng.CombatCardGeneration.Counter == expected.Length - 1,
            $"{name}, seed={seed}: expected {expected.Length - 1} full-pool draws, got {run.Rng.CombatCardGeneration.Counter}");
        Assert.Equal(oracle.NextInt(), run.Rng.CombatCardGeneration.NextInt());
        foreach (var (type, before) in counters)
            if (type != RunRngType.CombatCardGeneration) Assert.Equal(before, run.Rng.GetRng(type).Counter);
    }

    private sealed class ChoiceSource : ICardSelectionDecisionSource
    {
        public IReadOnlyList<CardModel> Candidates { get; private set; } = Array.Empty<CardModel>();
        public Task<IReadOnlyList<CardModel>> ChooseCardsAsync(CardSelectionRequest request)
        {
            Candidates = request.Candidates;
            return Task.FromResult<IReadOnlyList<CardModel>>(request.Candidates.Take(1).ToArray());
        }
    }
}

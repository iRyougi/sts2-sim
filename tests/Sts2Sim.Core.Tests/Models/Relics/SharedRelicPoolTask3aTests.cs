using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Content;
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Models.Enchantments;
using Sts2Sim.Core.Models.Monsters;
using Sts2Sim.Core.Models.RelicPools;
using Sts2Sim.Core.Models.Relics;
using Sts2Sim.Core.Rooms;
using Sts2Sim.Core.Runs;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Tests.Models.Relics;

[Collection("ModelDb")]
public sealed class SharedRelicPoolTask3aTests : IDisposable
{
    public SharedRelicPoolTask3aTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(ContentRegistry.AllTypes);
    }

    public void Dispose() => ModelDb.ResetForTests();

    [Theory]
    [InlineData("FrozenEgg")]
    [InlineData("GnarledHammer")]
    [InlineData("LavaLamp")]
    [InlineData("MoltenEgg")]
    [InlineData("ToxicEgg")]
    public async Task SharedRelics_ApplyTheirAuthoritativeGameplayEffect(string relicName)
    {
        Type relicType = typeof(RelicModel).Assembly.GetType(
            $"Sts2Sim.Core.Models.Relics.{relicName}")!;
        Assert.NotNull(relicType);
        RelicModel canonical = (RelicModel)ModelDb.Get(relicType);
        (RunState runState, Player player) = CreatePlayer(relicName);
        NonLeadingSelectionSource? selectionSource = null;
        CardModel[] eligibleCards = [];
        if (relicName == "GnarledHammer")
        {
            selectionSource = new NonLeadingSelectionSource();
            runState.ConfigureCardSelectionSource(selectionSource);
            var sharp = (Sharp)ModelDb.Get(typeof(Sharp));
            eligibleCards = player.Deck.Cards.Where(sharp.CanEnchant).ToArray();
        }

        await RelicCmd.Obtain(canonical, player);
        RelicModel relic = Assert.Single(player.Relics, candidate => candidate.GetType() == relicType);

        switch (relicName)
        {
            case "FrozenEgg":
                Assert.True(ModifyReward(relic, runState, player, CardType.Power).IsUpgraded);
                break;
            case "GnarledHammer":
                CardSelectionRequest request = Assert.Single(selectionSource!.Requests);
                Assert.Equal(eligibleCards, request.Candidates);
                Assert.Equal((0, 3), (request.MinCount, request.MaxCount));
                Assert.Same(relic, request.Source);
                Assert.False(request.Cancelable);
                CardModel[] sharpCards = player.Deck.Cards
                    .Where(card => card.Enchantments.OfType<Sharp>().Any())
                    .ToArray();
                Assert.Equal(selectionSource.Selected, sharpCards);
                Assert.DoesNotContain(sharpCards, card => ReferenceEquals(card, eligibleCards[0]));
                Assert.All(sharpCards, card =>
                    Assert.Equal(3m, Assert.Single(card.Enchantments.OfType<Sharp>()).Magnitude));
                break;
            case "LavaLamp":
                var room = new CombatRoom(
                    () => (MonsterModel)ModelDb.Get(typeof(WanderingGrunt)).MutableClone());
                runState.PushRoom(room);
                RelicModel lamp = relic;
                Assert.True(ModifyReward(lamp, runState, player, CardType.Attack).IsUpgraded);
                await lamp.AfterDamageReceived(
                    player.Creature,
                    new DamageResult(player.Creature, default(ValueProp)) { UnblockedDamage = 1 },
                    default(ValueProp),
                    null,
                    null);
                Assert.Null(lamp.TryModifyCardRewardOptionLate(
                    runState,
                    player,
                    CreateCard(CardType.Attack, player)));
                break;
            case "MoltenEgg":
                Assert.True(ModifyReward(relic, runState, player, CardType.Attack).IsUpgraded);
                break;
            case "ToxicEgg":
                Assert.True(ModifyReward(relic, runState, player, CardType.Skill).IsUpgraded);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(relicName), relicName, null);
        }

        Type[] poolTypes = SharedRelicPool.Instance.AllRelics.Select(relic => relic.GetType()).ToArray();
        Assert.Equal(
            [typeof(FestivePopper), typeof(FresnelLens), typeof(FrozenEgg), typeof(GamblingChip)],
            poolTypes.Skip(Array.IndexOf(poolTypes, typeof(FestivePopper))).Take(4));
        Assert.Equal(
            [typeof(TinyMailbox), typeof(Toolbox), typeof(ToxicEgg), typeof(TungstenRod)],
            poolTypes.Skip(Array.IndexOf(poolTypes, typeof(TinyMailbox))).Take(4));
        Assert.Contains(poolTypes, type => type == typeof(LoomingFruit));
        Assert.Contains(poolTypes, type => type == typeof(VeryHotCocoa));
    }

    private static (RunState RunState, Player Player) CreatePlayer(string seed)
    {
        var runState = new RunState($"task-3a-{seed}", new Overgrowth());
        Player player = Player.CreateForNewRun(ModelDb.Character<Regent>(), runState);
        runState.AddPlayer(player);
        return (runState, player);
    }

    private static CardModel ModifyReward(
        RelicModel relic,
        RunState runState,
        Player player,
        CardType cardType) =>
        Assert.IsAssignableFrom<CardModel>(relic.TryModifyCardRewardOptionLate(
            runState,
            player,
            CreateCard(cardType, player)));

    private static CardModel CreateCard(CardType cardType, Player player)
    {
        CardModel canonical = ModelDb.All<CardModel>()
            .First(card => card.Type == cardType && card.IsUpgradable);
        var card = (CardModel)canonical.MutableClone();
        card.AssignOwner(player);
        return card;
    }

    private sealed class NonLeadingSelectionSource : ICardSelectionDecisionSource
    {
        public List<CardSelectionRequest> Requests { get; } = [];
        public IReadOnlyList<CardModel> Selected { get; private set; } = [];

        public Task<IReadOnlyList<CardModel>> ChooseCardsAsync(CardSelectionRequest request)
        {
            Requests.Add(request);
            Selected = request.Candidates.Skip(1).Take(2).ToArray();
            return Task.FromResult(Selected);
        }
    }
}

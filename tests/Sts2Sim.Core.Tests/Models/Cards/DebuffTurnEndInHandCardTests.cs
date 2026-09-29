using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Content;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Powers;
using Sts2Sim.Core.Hooks;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Models.Powers;

namespace Sts2Sim.Core.Tests.Models.Cards;

[Collection("ModelDb")]
public sealed class DebuffTurnEndInHandCardTests : IDisposable
{
    public DebuffTurnEndInHandCardTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(ContentRegistry.AllTypes.Concat(new[] { typeof(Task11NoDamageMonster) }));
    }

    public void Dispose() => ModelDb.ResetForTests();

    public static TheoryData<Type, Type> Cards => new()
    {
        { typeof(Doubt), typeof(WeakPower) },
        { typeof(Shame), typeof(FrailPower) },
    };

    [Theory]
    [MemberData(nameof(Cards))]
    public void Metadata_MatchesAuthoritativeSource(Type cardType, Type powerType)
    {
        CardModel card = (CardModel)ModelDb.Get(cardType);

        Assert.Equal(CardType.Curse, card.Type);
        Assert.Equal(CardRarity.Curse, card.Rarity);
        Assert.Equal(TargetType.None, card.TargetType);
        Assert.Equal(-1, card.EnergyCost);
        Assert.Equal(0, card.MaxUpgradeLevel);
        Assert.Equal(new[] { CardKeyword.Unplayable }, card.Keywords);
        Assert.True(card.CanBeGeneratedByModifiers);
        Assert.True(card.CanBeGeneratedInCombat);
        Assert.True(typeof(PowerModel).IsAssignableFrom(powerType));
    }

    [Theory]
    [MemberData(nameof(Cards))]
    public async Task EndPlayerTurn_NewDebuffSurvivesSameTurnDurationTick(
        Type cardType,
        Type powerType)
    {
        (Player player, var room) =
            await Task11CombatTestSupport.CreateCombatAsync<Task11NoDamageMonster>(
                $"task8-new-{cardType.Name}");
        CardModel card = AddToHand(player, cardType);

        await room.Engine.EndPlayerTurnAsync();

        PowerModel power = Assert.Single(player.Creature.Powers, candidate => candidate.GetType() == powerType);
        Assert.Equal(1, power.Amount);
        Assert.False(power.SkipNextDurationTick);
        Assert.Contains(card, player.PlayerCombatState!.DiscardPile.Cards);
    }

    [Theory]
    [MemberData(nameof(Cards))]
    public async Task EndPlayerTurn_ExistingDebuffStacksThenTicksWithoutResettingSkipFlag(
        Type cardType,
        Type powerType)
    {
        (Player player, var room) =
            await Task11CombatTestSupport.CreateCombatAsync<Task11NoDamageMonster>(
                $"task8-existing-{cardType.Name}");
        PowerModel existing = (await PowerCmd.Apply(
            room.Engine.State,
            powerType,
            player.Creature,
            1m,
            null,
            null))!;
        existing.SkipNextDurationTick = false;
        AddToHand(player, cardType);

        await room.Engine.EndPlayerTurnAsync();

        PowerModel power = Assert.Single(player.Creature.Powers, candidate => candidate.GetType() == powerType);
        Assert.Same(existing, power);
        Assert.Equal(1, power.Amount);
        Assert.False(power.SkipNextDurationTick);
    }

    [Theory]
    [MemberData(nameof(Cards))]
    public async Task TurnEndEffect_RequiresPlayerSideAndCardInHand(
        Type cardType,
        Type powerType)
    {
        (Player player, var room) =
            await Task11CombatTestSupport.CreateCombatAsync<Task11NoDamageMonster>(
                $"task8-filter-{cardType.Name}");
        CardModel card = AddToHand(player, cardType);
        CardPileCmd.Add(card, PileType.Discard);

        await Hook.BeforeSideTurnEnd(
            room.Engine.State,
            CombatSide.Player,
            room.Engine.State.Allies);

        Assert.DoesNotContain(
            player.Creature.Powers,
            candidate => candidate.GetType() == powerType);

        CardPileCmd.Add(card, PileType.Hand);
        await Hook.BeforeSideTurnEnd(
            room.Engine.State,
            CombatSide.Enemy,
            room.Engine.State.Enemies);

        Assert.DoesNotContain(
            player.Creature.Powers,
            candidate => candidate.GetType() == powerType);
    }

    private static CardModel AddToHand(Player player, Type cardType)
    {
        var card = (CardModel)((CardModel)ModelDb.Get(cardType)).MutableClone();
        card.AssignOwner(player);
        player.PlayerCombatState!.Hand.AddInternal(card);
        return card;
    }
}

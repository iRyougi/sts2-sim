using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Models.Monsters;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.Models.Relics;
using Sts2Sim.Core.Rooms;
using Sts2Sim.Core.Runs;
using Sts2Sim.Core.Tests.Models.Cards;

namespace Sts2Sim.Core.Tests.Combat;

[Collection("ModelDb")]
public sealed class CardKeywordEndOfTurnTests : IDisposable
{
    public CardKeywordEndOfTurnTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(new[]
        {
            typeof(Regent), typeof(StrikeRegent), typeof(DefendRegent), typeof(FallingStar), typeof(Venerate),
            typeof(WeakPower), typeof(VulnerablePower), typeof(DivineRight), typeof(WanderingGrunt),
            typeof(NormalKeywordTestCard), typeof(RetainKeywordTestCard), typeof(EtherealKeywordTestCard),
        });
    }

    public void Dispose() => ModelDb.ResetForTests();

    [Fact]
    public async Task EndPlayerTurn_HandlesRetainEtherealAndNormalCards()
    {
        var runState = new RunState("keyword-end-turn", new Overgrowth());
        Player player = Player.CreateForNewRun(ModelDb.Character<Regent>(), runState);
        runState.AddPlayer(player);
        var room = new CombatRoom(() => (WanderingGrunt)ModelDb.Monster<WanderingGrunt>().MutableClone());
        await room.Enter(runState);
        RetainKeywordTestCard retain = AddToHand<RetainKeywordTestCard>(player);
        EtherealKeywordTestCard ethereal = AddToHand<EtherealKeywordTestCard>(player);
        NormalKeywordTestCard normal = AddToHand<NormalKeywordTestCard>(player);

        await room.Engine.EndPlayerTurnAsync();

        Assert.Contains(retain, player.PlayerCombatState!.Hand.Cards);
        Assert.Contains(ethereal, player.PlayerCombatState.ExhaustPile.Cards);
        Assert.Contains(normal, player.PlayerCombatState.DiscardPile.Cards);
    }

    private static TCard AddToHand<TCard>(Player player)
        where TCard : CardModel
    {
        var card = (TCard)ModelDb.Card<TCard>().MutableClone();
        card.AssignOwner(player);
        player.PlayerCombatState!.Hand.AddInternal(card);
        return card;
    }
}

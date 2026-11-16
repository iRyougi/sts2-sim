using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Events;
using Sts2Sim.Core.Factories;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Models.Events;

/// <summary>v0.113.1 Underdocks event (#927). 偏离 #148：不移植 DynamicVars 容器，费用与诅咒张数用玩法常量。</summary>
public sealed class WaterfallOfObviousSecrets : EventModel
{
    private const int NegotiateCost = 70;
    private const int BeatUpCurseCount = 2;

    protected override VisibleDecisionMetadata BuildVisibleState() => new([], [
        new("ENTER", []),
        new("BE_NICE", [VisibleValue.Id("Card", ModelDb.GetId<DingyDagger>().Entry), VisibleValue.Int("CardCount", 1)])
            { Contents = [VisibleContentProjection.ForCard(ModelDb.Card<DingyDagger>())] },
        new("BEAT_UP", [VisibleValue.Int("RelicCount", 1), VisibleValue.Condition("Relic", "Random"),
            VisibleValue.Id("Curse", ModelDb.GetId<Guilty>().Entry), VisibleValue.Int("CurseCount", BeatUpCurseCount)])
            { Contents = [VisibleContentProjection.ForCard(ModelDb.Card<Guilty>())] },
        new("NEGOTIATE", [VisibleValue.Int("Gold", NegotiateCost), VisibleValue.Int("CardCount", 1),
            VisibleValue.Scope("CardPool", "Character"), VisibleValue.Scope("CardRarity", "Rare")]),
        new("NEGOTIATE_LOCKED", [VisibleValue.Int("Gold", NegotiateCost)]),
    ]);

    protected override IReadOnlyList<EventOption> GenerateInitialOptions() =>
        [new EventOption("ENTER", EnterWaterfall)];

    private Task EnterWaterfall()
    {
        bool canNegotiate = Owner.Gold >= NegotiateCost;
        SetOptions([
            new EventOption("BE_NICE", BeNice),
            new EventOption("BEAT_UP", BeatUp),
            canNegotiate ? new EventOption("NEGOTIATE", Negotiate) : new EventOption("NEGOTIATE_LOCKED", null),
        ]);
        return Task.CompletedTask;
    }

    private async Task BeNice()
    {
        var dagger = (DingyDagger)ModelDb.Card<DingyDagger>().MutableClone();
        dagger.AssignOwner(Owner);
        await CardPileCmd.AddToDeck(dagger);
        Finish();
    }

    private async Task BeatUp()
    {
        RelicModel relic = RelicFactory.PullNextRelicFromFront(Owner, RelicFactory.RollRarity(Rng));
        await RelicCmd.Obtain(relic, Owner);
        await CardPileCmd.AddCursesToDeck(Enumerable.Repeat<CardModel>(ModelDb.Card<Guilty>(), BeatUpCurseCount), Owner);
        Finish();
    }

    private async Task Negotiate()
    {
        await PlayerCmd.LoseGold(NegotiateCost, Owner);
        CardCreationOptions options = CardCreationOptions.ForNonCombatWithUniformOdds(
            [Owner.Character.CardPool], card => card.Rarity == CardRarity.Rare);
        IReadOnlyList<CardModel> cards = CardFactory.CreateForReward(Owner, 1, options);
        if (cards.Count > 0) await CardPileCmd.AddToDeck(cards[0]);
        Finish();
    }
}

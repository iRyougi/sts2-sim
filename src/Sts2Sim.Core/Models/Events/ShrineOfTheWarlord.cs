using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Events;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Models.Relics;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Models.Events;

/// <summary>v0.113.1 Glory event (#927): one of two Mighty cards, or Mighty Wings plus Clumsy.</summary>
public sealed class ShrineOfTheWarlord : EventModel
{
    protected override VisibleDecisionMetadata BuildVisibleState() => new([], [
        new("TAKE_WEAPON", [VisibleValue.Id("Card", ModelDb.GetId<MightyWeapon>().Entry), VisibleValue.Int("CardCount", 1)])
            { Contents = [VisibleContentProjection.ForCard(ModelDb.Card<MightyWeapon>())] },
        new("TAKE_SHIELD", [VisibleValue.Id("Card", ModelDb.GetId<MightyShield>().Entry), VisibleValue.Int("CardCount", 1)])
            { Contents = [VisibleContentProjection.ForCard(ModelDb.Card<MightyShield>())] },
        new("TAKE_WINGS", [VisibleValue.Id("Relic", ModelDb.GetId<MightyWings>().Entry), VisibleValue.Int("RelicCount", 1),
            VisibleValue.Id("Curse", ModelDb.GetId<Clumsy>().Entry), VisibleValue.Int("CurseCount", 1)])
            { Contents = [VisibleContentProjection.ForRelic(ModelDb.Relic<MightyWings>()),
                VisibleContentProjection.ForCard(ModelDb.Card<Clumsy>())] },
    ]);

    /// <summary>Native <c>!(runState.Act.Ancient is Tanx)</c>; roomless facades have no Ancient.</summary>
    public override bool IsAllowed(IRunState runState) =>
        runState is not RunState run || run.CurrentAncientEventType != typeof(Tanx);

    protected override IReadOnlyList<EventOption> GenerateInitialOptions() =>
    [
        new EventOption("TAKE_WEAPON", AddCard<MightyWeapon>),
        new EventOption("TAKE_SHIELD", AddCard<MightyShield>),
        new EventOption("TAKE_WINGS", TakeWings),
    ];

    private async Task AddCard<T>() where T : CardModel
    {
        var card = (CardModel)ModelDb.Card<T>().MutableClone();
        card.AssignOwner(Owner);
        await CardPileCmd.AddToDeck(card);
        Finish();
    }

    private async Task TakeWings()
    {
        await CardPileCmd.AddCursesToDeck([ModelDb.Card<Clumsy>()], Owner);
        await RelicCmd.Obtain(ModelDb.Relic<MightyWings>(), Owner);
        Finish();
    }
}

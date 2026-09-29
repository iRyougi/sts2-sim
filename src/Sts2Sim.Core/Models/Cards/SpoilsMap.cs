using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Map;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Models.Cards;

public sealed class SpoilsMap : CardModel
{
    public override CardType Type => CardType.Quest;
    public override CardRarity Rarity => CardRarity.Quest;
    public override TargetType TargetType => TargetType.Self;
    public override int MaxUpgradeLevel => 0;
    protected override int CanonicalEnergyCost => -1;
    protected override IReadOnlyCollection<CardKeyword> CanonicalKeywords => [CardKeyword.Unplayable];
    public int SpoilsActIndex { get; private set; } = 1;
    public MapCoord? SpoilsCoord { get; private set; }

    public override ActMap ModifyGeneratedMap(IRunState runState, ActMap map, int actIndex) =>
        actIndex == SpoilsActIndex && Pile?.Type == PileType.Deck && runState is RunState run
            ? new SpoilsActMap(run) : map;

    public override ActMap ModifyGeneratedMapLate(IRunState runState, ActMap map, int actIndex)
    {
        if (actIndex == SpoilsActIndex && Pile?.Type == PileType.Deck)
            SpoilsCoord = map.GetAllMapPoints().FirstOrDefault(p => p.PointType == MapPointType.Treasure)?.coord;
        return map;
    }

    public override Task AfterMapGenerated(ActMap map, int actIndex)
    {
        if (actIndex == SpoilsActIndex && Pile?.Type == PileType.Deck && SpoilsCoord is { } coord)
            map.GetPoint(coord)?.AddQuest(this);
        return Task.CompletedTask;
    }

    public override Task BeforeCardRemoved(CardModel card)
    {
        if (ReferenceEquals(card, this) && Owner.RunState is RunState run && run.CurrentActIndex == SpoilsActIndex)
        {
            if (SpoilsCoord is { } coord) run.Map.GetPoint(coord)?.RemoveQuest(this);
        }
        return Task.CompletedTask;
    }

    public async Task<int> OnQuestComplete()
    {
        await PlayerCmd.GainGold(600, Owner);
        PlayerCmd.CompleteQuest(this);
        await CardPileCmd.RemoveFromDeck(Owner, this);
        return 600;
    }

    internal override void AppendCombatStateDescription(
        ref Combat.StateDescription.CombatStateDescriptionBuilder builder,
        Combat.StateDescription.CombatStateDescriptionContext context)
    {
        builder.Append(SpoilsActIndex);
        builder.Append(SpoilsCoord.HasValue);
        if (SpoilsCoord is { } coord)
        {
            builder.Append(coord.col);
            builder.Append(coord.row);
        }
    }
}

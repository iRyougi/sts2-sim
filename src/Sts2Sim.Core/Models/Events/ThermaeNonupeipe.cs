using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Events;
using Sts2Sim.Core.Helpers;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Models.Relics;
using Sts2Sim.Core.Runs;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Events;

/// <summary>v0.113.1 Glory event (#927). The drink relic is rolled in CalculateVars and named on the first page.
/// 偏离 #148：不移植 DynamicVars 容器，价格/伤害/诅咒张数用玩法常量。</summary>
public sealed class ThermaeNonupeipe : EventModel
{
    private const int ThermalBathsCost = 88;
    private const decimal DrinkHpLoss = 31m;
    private const int DrinkGuiltyCount = 3;

    private RelicModel? _drinkRelic;

    public RelicModel DrinkRelic => _drinkRelic ?? throw new InvalidOperationException("Drink relic has not been rolled.");

    protected override VisibleDecisionMetadata BuildVisibleState() => new([], [
        new("THERMAL_BATHS", [VisibleValue.Int("Gold", ThermalBathsCost), VisibleValue.Id("Relic", ModelDb.GetId<BathTowel>().Entry),
            VisibleValue.Int("RelicCount", 1)])
            { Contents = [VisibleContentProjection.ForRelic(ModelDb.Relic<BathTowel>())] },
        new("PRIVATE_OASIS", [VisibleValue.Int("Gold", Owner.Gold), VisibleValue.Scope("GoldLoss", "All"),
            VisibleValue.Id("Relic", ModelDb.GetId<BathRobes>().Entry), VisibleValue.Int("RelicCount", 1)])
            { Contents = [VisibleContentProjection.ForRelic(ModelDb.Relic<BathRobes>())] },
        new("DRINK_WATERS", [VisibleValue.Int("HpLoss", (int)DrinkHpLoss), VisibleValue.Id("Curse", ModelDb.GetId<Guilty>().Entry),
            VisibleValue.Int("CurseCount", DrinkGuiltyCount), VisibleValue.Id("Relic", DrinkRelic.Id.Entry), VisibleValue.Int("RelicCount", 1)])
            { Contents = [VisibleContentProjection.ForRelic(DrinkRelic), VisibleContentProjection.ForCard(ModelDb.Card<Guilty>())] },
    ]);

    public override bool IsAllowed(IRunState runState) =>
        runState.Players.All(player => player.Gold > 175 &&
                                       (decimal)player.Creature.CurrentHp <= player.Creature.MaxHp * 0.75m) &&
        (runState is not RunState run || run.CurrentAncientEventType != typeof(Nonupeipe));

    protected override void CalculateVars()
    {
        IEnumerable<RelicModel> items = ((Nonupeipe)ModelDb.Get(typeof(Nonupeipe))).AllPossibleOptions
            .Where(relic => relic is not LoomingFruit);
        _drinkRelic = Rng.NextItem(items) ?? throw new InvalidOperationException("Nonupeipe has no drink relic candidate.");
    }

    protected override IReadOnlyList<EventOption> GenerateInitialOptions() =>
    [
        new EventOption("THERMAL_BATHS", ThermalBaths),
        new EventOption("PRIVATE_OASIS", PrivateOasis),
        new EventOption("DRINK_WATERS", DrinkWaters),
    ];

    private async Task ThermalBaths()
    {
        await PlayerCmd.LoseGold(ThermalBathsCost, Owner);
        await RelicCmd.Obtain(ModelDb.Relic<BathTowel>(), Owner);
        Finish();
    }

    private async Task PrivateOasis()
    {
        await PlayerCmd.LoseGold(Owner.Gold, Owner);
        await RelicCmd.Obtain(ModelDb.Relic<BathRobes>(), Owner);
        Finish();
    }

    private async Task DrinkWaters()
    {
        await CreatureCmd.Damage(RunState, Owner.Creature, DrinkHpLoss, ValueProp.Unblockable | ValueProp.Unpowered);
        await CardPileCmd.AddCursesToDeck(Enumerable.Repeat<CardModel>(ModelDb.Card<Guilty>(), DrinkGuiltyCount), Owner);
        await RelicCmd.Obtain(DrinkRelic, Owner);
        Finish();
    }
}

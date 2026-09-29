using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Events;
using Sts2Sim.Core.Models.Enchantments;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Models.Events;

public sealed class SpiralingWhirlpool : EventModel
{
    private decimal _heal;

    public override bool IsAllowed(IRunState runState) =>
        runState.Players.All(player => player.Deck.Cards.Any(
            ModelDb.GetById<Spiral>(ModelDb.GetId<Spiral>()).CanEnchant));

    protected override void CalculateVars() => _heal = Owner.Creature.MaxHp * 0.33m;

    protected override IReadOnlyList<EventOption> GenerateInitialOptions() =>
    [
        new("OBSERVE", ObserveTheSpiral),
        new("DRINK", Drink),
    ];

    private async Task ObserveTheSpiral()
    {
        Spiral spiral = ModelDb.GetById<Spiral>(ModelDb.GetId<Spiral>());
        CardModel? selected = (await CardSelectCmd.SelectCardsAsync(
            RunState,
            Owner,
            Owner.Deck.Cards.Where(spiral.CanEnchant),
            1,
            1,
            this)).FirstOrDefault();
        if (selected is not null) await CardCmd.Enchant<Spiral>(selected, 1m);
        Finish();
    }

    private async Task Drink()
    {
        await CreatureCmd.Heal(Owner.Creature, _heal);
        Finish();
    }
}

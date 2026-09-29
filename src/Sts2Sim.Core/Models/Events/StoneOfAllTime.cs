using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Events;
using Sts2Sim.Core.Models.Enchantments;
using Sts2Sim.Core.Runs;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Events;

public sealed class StoneOfAllTime : EventModel
{
    private PotionModel? _potion;
    protected override bool LocksPotions => true;
    public override bool IsAllowed(IRunState runState) => runState is RunState { CurrentActIndex: 1 } &&
        runState.Players.All(p => p.PotionSlots.Any(potion => potion is not null));

    protected override IReadOnlyList<EventOption> GenerateInitialOptions()
    {
        _potion = Rng.NextItem(Owner.PotionSlots.OfType<PotionModel>());
        bool canPush = Owner.Deck.Cards.Any(ModelDb.GetById<Vigorous>(ModelDb.GetId<Vigorous>()).CanEnchant);
        return [new(_potion is null ? "LIFT_LOCKED" : "LIFT", _potion is null ? null : Lift),
            new(canPush ? "PUSH" : "PUSH_LOCKED", canPush ? Push : null)];
    }

    private async Task Lift()
    {
        await PotionCmd.DiscardForEvent(_potion!);
        await CreatureCmd.GainMaxHp(Owner.Creature, 10);
        Rng.NextInt(100);
        Finish();
    }

    private async Task Push()
    {
        await CreatureCmd.Damage(RunState, Owner.Creature, 6, ValueProp.Unblockable | ValueProp.Unpowered);
        var enchantment = ModelDb.GetById<Vigorous>(ModelDb.GetId<Vigorous>());
        foreach (var card in await CardSelectCmd.SelectCardsAsync(RunState, Owner,
                     Owner.Deck.Cards.Where(enchantment.CanEnchant), 1, 1, this))
            await CardCmd.Enchant<Vigorous>(card, 8);
        Rng.NextInt(100);
        Finish();
    }
}

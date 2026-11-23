using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Events;
using Sts2Sim.Core.Models.Enchantments;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Models.Events;

public sealed class Symbiote : EventModel
{
    protected override VisibleDecisionMetadata BuildVisibleState() => new([], [
        new("APPROACH", [VisibleValue.Id("EnchantmentId", "CORRUPTED"), VisibleValue.Scope("CardType", "Attack"), VisibleValue.Int("SelectCount", 1), VisibleValue.Int("DamageBonusPercent", 50), VisibleValue.Int("EnchantmentHpLoss", 2)]),
        new("APPROACH_LOCKED", [VisibleValue.Condition("RequiredCardType", "Attack")]),
        new("KILL_WITH_FIRE", [VisibleValue.Int("TransformSelectCount", 1)]),
    ]);

    public override bool IsAllowed(IRunState runState) => runState is RunState { CurrentActIndex: > 0 };

    protected override IReadOnlyList<EventOption> GenerateInitialOptions()
    {
        bool canEnchant = Owner.Deck.Cards.Any(ModelDb.GetById<Corrupted>(ModelDb.GetId<Corrupted>()).CanEnchant);
        return [new(canEnchant ? "APPROACH" : "APPROACH_LOCKED", canEnchant ? Approach : null),
            new("KILL_WITH_FIRE", KillWithFire)];
    }

    private async Task Approach()
    {
        var enchantment = ModelDb.GetById<Corrupted>(ModelDb.GetId<Corrupted>());
        foreach (var card in await CardSelectCmd.SelectCardsAsync(RunState, Owner,
                     Owner.Deck.Cards.Where(enchantment.CanEnchant), 1, 1, this))
            await CardCmd.Enchant<Corrupted>(card, 1);
        Finish();
    }

    private async Task KillWithFire()
    {
        foreach (var card in await CardSelectCmd.SelectCardsAsync(RunState, Owner,
                     Owner.Deck.Cards.Where(c => c.IsTransformable), 1, 1, this))
            await CardCmd.TransformToRandom(card, Rng, RunState);
        Finish();
    }
}

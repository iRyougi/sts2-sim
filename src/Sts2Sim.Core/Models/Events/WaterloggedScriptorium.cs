using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Events;
using Sts2Sim.Core.Models.Enchantments;
using Sts2Sim.Core.Runs;
using Sts2Sim.Core.Entities.Gold;

namespace Sts2Sim.Core.Models.Events;

public sealed class WaterloggedScriptorium : EventModel
{
    public override bool IsAllowed(IRunState runState) =>
        runState.Players.All(player => player.Gold >= 55);

    protected override IReadOnlyList<EventOption> GenerateInitialOptions() =>
    [
        new EventOption("BLOODY_INK", BloodyInkAsync),
        new EventOption("TENTACLE_QUILL", Owner.Gold >= 55 ? TentacleQuillAsync : null),
        new EventOption("PRICKLY_SPONGE", Owner.Gold >= 99 ? PricklySpongeAsync : null),
    ];

    private async Task BloodyInkAsync()
    {
        await CreatureCmd.GainMaxHp(Owner.Creature, 6m);
        Finish();
    }

    private async Task TentacleQuillAsync()
    {
        await PlayerCmd.LoseGold(55m, Owner, GoldLossType.Spent);
        await EnchantCardsAsync(1);
        Finish();
    }

    private async Task PricklySpongeAsync()
    {
        await PlayerCmd.LoseGold(99m, Owner, GoldLossType.Spent);
        await EnchantCardsAsync(2);
        Finish();
    }

    private async Task EnchantCardsAsync(int count)
    {
        Steady steady = ModelDb.GetById<Steady>(ModelDb.GetId<Steady>());
        IReadOnlyList<CardModel> selected = await CardSelectCmd.SelectCardsAsync(
            RunState,
            Owner,
            Owner.Deck.Cards.Where(steady.CanEnchant),
            count,
            count,
            this);
        foreach (CardModel card in selected)
            await CardCmd.Enchant<Steady>(card, 1m);
    }

}

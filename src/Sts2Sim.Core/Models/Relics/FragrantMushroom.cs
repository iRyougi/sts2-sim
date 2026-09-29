using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Helpers;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Relics;

/// <summary>Event relic: lose 15 HP, then randomly upgrade up to two deck cards.</summary>
public sealed class FragrantMushroom : RelicModel
{
    public override RelicRarity Rarity => RelicRarity.Event;

    public override bool HasUponPickupEffect => true;

    public override async Task AfterObtained()
    {
        await CreatureCmd.Damage(
            Owner.RunState,
            Owner.Creature,
            15m,
            ValueProp.Unblockable | ValueProp.Unpowered);
        List<CardModel> candidates = Owner.Deck.Cards.Where(card => card.IsUpgradable).ToList();
        candidates.StableShuffle(Owner.RunState.Rng.Niche);
        foreach (CardModel card in candidates.Take(2))
        {
            CardCmd.Upgrade(card);
        }
    }
}

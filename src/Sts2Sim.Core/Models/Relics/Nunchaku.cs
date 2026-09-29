using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Relics;

namespace Sts2Sim.Core.Models.Relics;

public sealed class Nunchaku : RelicModel
{
    private int _attacksPlayed;

    public override RelicRarity Rarity => RelicRarity.Uncommon;

    public override Task AfterCardPlayed(CardPlay cardPlay)
    {
        if (cardPlay.Player != Owner ||
            cardPlay.Card.Type != CardType.Attack)
        {
            return Task.CompletedTask;
        }

        _attacksPlayed++;
        if (_attacksPlayed % 10 != 0)
        {
            return Task.CompletedTask;
        }

        Owner.PlayerCombatState!.GainEnergy(1m);
        return Task.CompletedTask;
    }

    internal override void AppendCombatStateDescription(
        ref global::Sts2Sim.Core.Combat.StateDescription.CombatStateDescriptionBuilder builder,
        global::Sts2Sim.Core.Combat.StateDescription.CombatStateDescriptionContext context)
    {
        builder.Append(_attacksPlayed);
    }
}

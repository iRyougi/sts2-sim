using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Relics;

namespace Sts2Sim.Core.Models.Relics;

public sealed class Nunchaku : RelicModel
{
    private const int AttacksThreshold = 10; // CardsVar(10)

    private int _attacksPlayed;

    public override RelicRarity Rarity => RelicRarity.Uncommon;

    /// <summary>Source [SavedProperty] setter recompute on load (field restore bypasses the setter).</summary>
    internal override void RefreshStatusAfterLoad() =>
        Status = _attacksPlayed % AttacksThreshold == AttacksThreshold - 1 ? RelicStatus.Active : RelicStatus.Normal;

    public override Task AfterCardPlayed(CardPlay cardPlay)
    {
        if (cardPlay.Player != Owner ||
            cardPlay.Card.Type != CardType.Attack)
        {
            return Task.CompletedTask;
        }

        _attacksPlayed++;
        // Nunchaku.UpdateDisplay via the AttacksPlayed setter (IsActivating is animation only).
        Status = _attacksPlayed % AttacksThreshold == AttacksThreshold - 1 ? RelicStatus.Active : RelicStatus.Normal;
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

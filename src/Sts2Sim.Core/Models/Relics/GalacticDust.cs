using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Relics;

public sealed class GalacticDust : RelicModel
{
    private int _starsSpent;

    public override RelicRarity Rarity => RelicRarity.Uncommon;

    public override async Task AfterStarsSpent(int amount, Player spender)
    {
        if (spender != Owner)
        {
            return;
        }

        _starsSpent += amount;
        int triggers = _starsSpent / 10;
        if (triggers == 0)
        {
            return;
        }

        _starsSpent %= 10;
        await CreatureCmd.GainBlock(
            Owner.Creature.CombatState!,
            Owner.Creature,
            10m * triggers,
            ValueProp.Unpowered,
            null,
            null);
    }

    internal override void AppendCombatStateDescription(
        ref global::Sts2Sim.Core.Combat.StateDescription.CombatStateDescriptionBuilder builder,
        global::Sts2Sim.Core.Combat.StateDescription.CombatStateDescriptionContext context)
    {
        builder.Append(_starsSpent);
    }
}

using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Powers;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Powers;

public sealed class BlackHolePower : PowerModel
{
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override async Task AfterStarsGained(int amount, Player gainer)
    {
        if (amount <= 0 || gainer.Creature != Owner)
        {
            return;
        }

        await DamageAllEnemies(null);
    }

    public override async Task AfterCardPlayed(CardPlay cardPlay)
    {
        if (cardPlay.Player.Creature != Owner ||
            cardPlay.Resources.StarsSpent <= 0 ||
            !cardPlay.IsLastInSeries)
        {
            return;
        }

        await DamageAllEnemies(cardPlay);
    }

    private Task DamageAllEnemies(CardPlay? cardPlay) => CreatureCmd.Damage(
        Owner.CombatState!,
        Owner.CombatState!.GetOpponentsOf(Owner),
        Amount,
        ValueProp.Unpowered,
        Owner,
        null,
        cardPlay);
}

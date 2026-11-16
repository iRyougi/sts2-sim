using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Powers;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Powers;

/// <summary>每回合首次打出 Wah 牌后对全部可命中敌人造成伤害（原版 <c>ViolentChordPower</c>）。</summary>
public sealed class ViolentChordPower : PowerModel
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override async Task AfterCardPlayed(CardPlay cardPlay)
    {
        if (ReferenceEquals(cardPlay.Card.Owner, Owner.Player) &&
            cardPlay.Card.Tags.Contains(CardTag.Wah) &&
            MelodyCmd.IsFirstPlayThisTurn(cardPlay, CardTag.Wah) &&
            Owner.CombatState is { } combatState)
        {
            Creature[] enemies = combatState.Enemies.Where(c => c.IsHittable).ToArray();
            if (enemies.Length > 0)
            {
                await CreatureCmd.Damage(combatState, enemies, Amount, ValueProp.Unpowered, Owner, null, null);
            }
        }
    }
}

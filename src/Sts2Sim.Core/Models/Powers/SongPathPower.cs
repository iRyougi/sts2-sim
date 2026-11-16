using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Powers;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Powers;

/// <summary>每回合首次打出 Cho 牌后获得格挡（原版 <c>SongPathPower</c>）。</summary>
public sealed class SongPathPower : PowerModel
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override async Task AfterCardPlayed(CardPlay cardPlay)
    {
        if (ReferenceEquals(cardPlay.Card.Owner, Owner.Player) &&
            cardPlay.Card.Tags.Contains(CardTag.Cho) &&
            MelodyCmd.IsFirstPlayThisTurn(cardPlay, CardTag.Cho))
        {
            await CreatureCmd.GainBlock(Owner.CombatState!, Owner, Amount, ValueProp.Unpowered, null, null);
        }
    }
}

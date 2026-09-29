using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Rooms;
using Sts2Sim.Core.Combat.StateDescription;

namespace Sts2Sim.Core.Models.Relics;

public sealed class VelvetChoker : RelicModel
{
    private int _cardsPlayedThisTurn;
    public override RelicRarity Rarity => RelicRarity.Ancient;
    public override decimal ModifyMaxEnergy(Player player, decimal amount) => player == Owner ? amount + 1m : amount;
    public override bool ShouldPlay(CardModel card, bool isAutoPlay) => card.Owner != Owner || _cardsPlayedThisTurn < 6;
    public override Task AfterCardPlayed(CardPlay cardPlay)
    {
        if (cardPlay.Card.Owner == Owner) _cardsPlayedThisTurn++;
        return Task.CompletedTask;
    }
    public override Task AfterRoomEntered(AbstractRoom room)
    {
        if (room is CombatRoom) _cardsPlayedThisTurn = 0;
        return Task.CompletedTask;
    }
    public override Task AfterCombatEnd()
    {
        _cardsPlayedThisTurn = 0;
        return Task.CompletedTask;
    }
    public override Task BeforeSideTurnStart(CombatSide side, IReadOnlyList<Creature> participants)
    {
        if (participants.Contains(Owner.Creature)) _cardsPlayedThisTurn = 0;
        return Task.CompletedTask;
    }
    internal override void AppendCombatStateDescription(
        ref CombatStateDescriptionBuilder builder, CombatStateDescriptionContext context) =>
        builder.Append(_cardsPlayedThisTurn);
}

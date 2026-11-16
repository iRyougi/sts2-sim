using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Combat.StateDescription;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Powers;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Powers;

/// <summary>每回合首次集齐 Wah/Cho/Ki 三种旋律后获得格挡（原版 <c>LullabyPower</c>）。</summary>
public sealed class LullabyPower : PowerModel
{
    private bool _proccdThisTurn;

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override Task AfterApplied(Creature? applier, CardModel? cardSource)
    {
        if (Owner.Player is { } player &&
            MelodyCmd.PlayedChoThisTurn(player) &&
            MelodyCmd.PlayedWahThisTurn(player) &&
            MelodyCmd.PlayedKiThisTurn(player))
        {
            _proccdThisTurn = true;
        }
        return Task.CompletedTask;
    }

    public override Task BeforeSideTurnStart(CombatSide side, IReadOnlyList<Creature> participants)
    {
        if (participants.Contains(Owner))
        {
            _proccdThisTurn = false;
        }
        return Task.CompletedTask;
    }

    public override async Task AfterCardPlayed(CardPlay cardPlay)
    {
        if (ReferenceEquals(cardPlay.Card.Owner.Creature, Owner) &&
            Owner.CombatState?.IsLiveCombat() == true &&
            !_proccdThisTurn &&
            Owner.Player is { } player &&
            MelodyCmd.PlayedChoThisTurn(player) &&
            MelodyCmd.PlayedWahThisTurn(player) &&
            MelodyCmd.PlayedKiThisTurn(player))
        {
            _proccdThisTurn = true;
            await CreatureCmd.GainBlock(Owner.CombatState!, Owner, Amount, ValueProp.Unpowered, null, null);
        }
    }

    internal override void RestoreCombatCloneReferencesFrom(
        PowerModel source,
        IReadOnlyDictionary<CardModel, CardModel> cardMap,
        IReadOnlyDictionary<Creature, Creature> creatureMap)
    {
        base.RestoreCombatCloneReferencesFrom(source, cardMap, creatureMap);
        _proccdThisTurn = ((LullabyPower)source)._proccdThisTurn;
    }

    internal override void AppendCombatStateDescription(
        ref CombatStateDescriptionBuilder builder,
        CombatStateDescriptionContext context)
    {
        builder.Append(_proccdThisTurn);
    }
}

using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Models.Powers;

namespace Sts2Sim.Core.Models.Relics;

public sealed class RainbowRing : RelicModel
{
    private bool _playedAttack;
    private bool _playedSkill;
    private bool _playedPower;
    private bool _triggeredThisTurn;

    public override RelicRarity Rarity => RelicRarity.Rare;

    public override Task BeforeSideTurnStart(
        CombatSide side,
        IReadOnlyList<Creature> participants)
    {
        if (participants.Contains(Owner.Creature))
        {
            _playedAttack = false;
            _playedSkill = false;
            _playedPower = false;
            _triggeredThisTurn = false;
        }

        return Task.CompletedTask;
    }

    public override async Task AfterCardPlayed(CardPlay cardPlay)
    {
        if (cardPlay.Player != Owner || _triggeredThisTurn)
        {
            return;
        }

        switch (cardPlay.Card.Type)
        {
            case CardType.Attack:
                _playedAttack = true;
                break;
            case CardType.Skill:
                _playedSkill = true;
                break;
            case CardType.Power:
                _playedPower = true;
                break;
            default:
                return;
        }

        if (!_playedAttack || !_playedSkill || !_playedPower)
        {
            return;
        }

        _triggeredThisTurn = true;
        var combatState = Owner.Creature.CombatState!;
        await PowerCmd.Apply<StrengthPower>(combatState, Owner.Creature, 1m, Owner.Creature, null);
        await PowerCmd.Apply<DexterityPower>(combatState, Owner.Creature, 1m, Owner.Creature, null);
    }

    public override Task AfterCombatEnd()
    {
        _playedAttack = false;
        _playedSkill = false;
        _playedPower = false;
        _triggeredThisTurn = false;
        return Task.CompletedTask;
    }

    internal override void AppendCombatStateDescription(
        ref global::Sts2Sim.Core.Combat.StateDescription.CombatStateDescriptionBuilder builder,
        global::Sts2Sim.Core.Combat.StateDescription.CombatStateDescriptionContext context)
    {
        builder.Append(_playedAttack);
        builder.Append(_playedSkill);
        builder.Append(_playedPower);
        builder.Append(_triggeredThisTurn);
    }
}
